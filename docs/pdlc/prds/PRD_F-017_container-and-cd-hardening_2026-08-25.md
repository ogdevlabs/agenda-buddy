# PRD: Container and CI/CD Hardening

**Date:** 2026-08-25
**Status:** Approved
**Feature slug:** container-and-cd-hardening
**Episode:** _Will be assigned after delivery_

---

## Overview

Three Dockerfiles in this repository publish `net10.0` builds onto a `dotnet/runtime:8.0` base and cannot run, and CONSTITUTION §7's mandatory security scan has never executed automatically — both gaps invisible because CI builds no container image and has no scanner. A third defect surfaced at Design, verified live: **every** service, not just the three broken ones, currently fails to `dotnet publish` at all, because `EventAndCommands` (a class library referenced by all seven services) ships its own `appsettings.json` with `CopyToOutputDirectory: Always`, colliding with each service's own file. This feature deletes the three broken, unnecessary images, fixes the publish conflict, wires an automated dependency-audit + secret-scan + container-image-scan gate into CI, and adds a build+scan job for the seven services that remain — using .NET SDK container support rather than the existing hand-written Dockerfiles (see the Architecture note below), and closing the exact class of gap that let a live database credential ship to git history undetected (`ISSUE-002`).

---

## Problem Statement

F-011's .NET 10 upgrade missed the final-stage base image on `Library/Dockerfile`, `Kafka/Dockerfile`, and `EventAndCommands/Dockerfile` — all three still publish onto `dotnet/runtime:8.0`, which cannot run a `net10.0` assembly. Nothing caught it because no CI job builds a container image at all. The same blind spot hid a second, more severe defect: `EventAndCommands.csproj`'s own `appsettings.json` is marked `CopyToOutputDirectory: Always`, so `dotnet publish` on **any** of the seven services fails outright with `NETSDK1152` (a file-conflict error) — verified live by running `docker build` against `Booking/Dockerfile`, which failed at its `dotnet publish` step with exactly this error. Independently, CONSTITUTION §7 marks "Security scan (dependency audit + secret scan)" as always required and cannot be unchecked, yet the maintainer has satisfied it "by hand" at every ship gate since F-013 (five documented occurrences in STATE.md's Guardrail Log) — a real cost of that gap already materialized when a MongoDB Atlas credential was committed to 17 tracked files and remains valid in git history to this day (`ISSUE-002`, still open, P0).

**Architecture note (found at Design):** this project's actual cloud deployment path (`AppHostWiring.cs`'s `DeploymentTarget.Cloud`, targeting Azure Container Apps via `azd`/`aspire deploy`) builds container images directly from each service's project file using .NET SDK container support — it never reads the hand-written Dockerfiles. Those Dockerfiles only serve the already-broken legacy `docker compose up` path (1 of 7 services wired in). Wave 3 (the new image-build-and-scan CI job) therefore targets SDK container support, not the Dockerfiles — securing the artifact this project would actually ship, not a disconnected legacy one.

---

## Target User

Entirely internal/operational — this feature has no end-user-facing component. The two beneficiaries are the maintainer (running `docker compose` or cutting a release image locally) and the CI pipeline itself, which today has zero Docker or security-scan awareness (`INTENT.md` defines no persona for this; confirmed at Discover).

---

## Requirements

1. The system MUST delete `Library/Dockerfile`, `Kafka/Dockerfile`, and `EventAndCommands/Dockerfile`, and remove the `events`, `kafka-library`, and `common-library` service blocks from `docker-compose.yml` and `docker-compose.override.yml`.
2. The system MUST add a structural regression test (in the style of `AppHostWiringTest.NoServiceBindsAHardcodedHostPort`) asserting the three files/service blocks in Requirement 1 do not exist, generalized to fail on any Dockerfile whose final-stage base image's runtime major version does not match its build stage's SDK major version — so this defect class cannot recur under a different filename.
3. The system MUST remove the `CopyToOutputDirectory` metadata from `EventAndCommands.csproj`'s own `appsettings.json` item, so `dotnet publish` no longer fails with `NETSDK1152` for any of the seven services that reference it — verified live during Design (fix confirmed end-to-end with `dotnet publish -t:PublishContainer`).
4. The system MUST add a CI step that runs `dotnet list package --vulnerable --include-transitive` on every pull request and fails on any new HIGH/CRITICAL finding, carrying forward the existing `ADR-030` `NU1903` suppression for the accepted SSH.NET exception so the gate does not fail on a known, already-accepted risk on its own first run.
5. The system MUST add a CI step that runs gitleaks against the full pull request diff history (not just the working tree) and fails on any detected secret.
6. The system MUST prove, via a canary test, that the configured gitleaks ruleset detects a fixture matching the shape of the previously-leaked Atlas credential (not the real value) — adding a custom rule if the default ruleset misses it.
7. The system MUST add a new CI job that builds each of the seven remaining services using .NET SDK container support (`dotnet publish -t:PublishContainer`, no Dockerfile) as a GitHub Actions matrix (one entry per service), and scans each built image with Trivy. This targets the artifact this project's actual Aspire/`azd` deployment path produces, not the hand-written Dockerfiles, which serve only the already-broken legacy Compose path.
8. The Trivy scan MUST fail the build on HIGH/CRITICAL findings introduced by this project's own dependencies, and MUST only warn (not fail) on findings inherited from the base image itself (`mcr.microsoft.com/dotnet/aspnet:10.0`).
9. The new image-build job MUST trigger on changes to any of the seven service directories, their `.csproj` files, or `docker-compose*.yml`, and this path filter MUST be verified as actually consumed by an `if:` condition — this pipeline has a precedent dead-filter bug (the `library` output is computed and never consumed) that must not repeat.
10. The new image-build job MUST carry `timeout-minutes: 10`. The system MUST NOT modify the five existing CI jobs, none of which currently has a timeout.
11. The system MUST add `.github/dependabot.yml`, and MUST verify live (not just by file presence) that a Dependabot pull request actually opens after this feature merges to `main`.
12. The new image-build job MUST NOT push any image to a registry, and MUST NOT run a built image (`docker run`) or perform any container health/smoke check.

---

## Assumptions

- The `NU1903` package-scoped suppression mechanism already used to resolve `ADR-030`'s SSH.NET finding by hand extends cleanly to an automated CI step with no further change.
- `ubuntu-latest` GitHub Actions runners provide a working Docker daemon sufficient for .NET SDK container support to build and load an image locally — unverified in CI specifically (verified on this development machine), flagged as a known risk below.
- gitleaks' default ruleset either already recognizes a MongoDB/Atlas-style connection string as a secret, or can be extended with one custom rule at negligible cost.
- This feature's lifetime requires no container registry — cloud deployment stays deferred by `ADR-035` for the duration.
- Adding `.github/dependabot.yml` alone is sufficient to activate Dependabot on this repository, with no separate repository-settings change required — to be verified live per Requirement 11.
- Removing `EventAndCommands.csproj`'s `appsettings.json`-copy metadata has no other consumer relying on that copied file being present in its own output directory — `ConfigurationLoader.LoadConfiguration()` reads "appsettings.json" from its executing assembly's directory, which will still resolve to each consuming service's own `appsettings.json` once the duplicate copy is removed.

---

## Acceptance Criteria

1. `Library/Dockerfile`, `Kafka/Dockerfile`, and `EventAndCommands/Dockerfile` do not exist in the repository. 🧪 test-first
2. `docker-compose.yml` and `docker-compose.override.yml` contain no `events`, `kafka-library`, or `common-library` service blocks. 🧪 test-first
3. A structural test fails if any Dockerfile's final `FROM` stage's runtime major version differs from its build stage's SDK major version. 🧪 test-first
4. `dotnet publish -t:PublishContainer` succeeds for each of the seven services with no `NETSDK1152` (or equivalent content-conflict) error. 🧪 test-first
5. CI runs `dotnet list package --vulnerable --include-transitive` on every pull request and fails on any new HIGH/CRITICAL finding not covered by the existing `ADR-030` (`NU1903`) suppression. 🧪 test-first
6. CI runs gitleaks against the full pull request diff history and fails on any detected secret. 🧪 test-first
7. A canary test proves the configured gitleaks ruleset detects a fixture matching the shape of the previously-leaked Atlas credential; a custom rule is added if the default ruleset misses it. 🧪 test-first
8. A new CI job builds each of the seven remaining services via .NET SDK container support as a matrix (one entry per service) and fails if any entry's publish/container-build fails. 🧪 test-first
9. Each built image is scanned by Trivy; the job fails on HIGH/CRITICAL findings introduced by this project's own dependencies, and warns without failing on findings inherited from the base image itself. 🧪 test-first
10. The new image-build job's path filter triggers on a live test PR that edits one service's source or `.csproj`, confirmed by observing the job actually run. 🧪 test-first
11. The new image-build job carries `timeout-minutes: 10`; none of the five pre-existing jobs are modified. 🧪 test-first
12. `.github/dependabot.yml` exists, and a live Dependabot pull request is confirmed to open after this feature merges to `main`. 🧪 test-first
13. The new image-build job does not execute `docker run` or any container health/smoke check against a built image. 🧪 test-first
14. `[security] (T-001)` Every `uses:` line referencing `gitleaks-action` or the Trivy action in `dotnet.yml` matches a 40-character hex commit SHA, not a tag or branch name. 🧪 test-first
15. `[security] (T-002)` Given the gitleaks canary test runs against the Atlas-credential-shaped fixture, the job's captured log output contains the fixture's file path and line number but never the fixture's literal secret string. 🧪 test-first

*(ACs 14-15 added post-Define at the Design threat-modeling gate, Step 10.5/14.5 — logged as a PRD addendum, not a Define-gate reopen. See `threat-model.md` T-001/T-002 and `DECISIONS.md`'s threat-derived-AC log entry.)*

---

## User Stories

**F-017-US-01: Broken container images are removed**
*Acceptance criteria: 1, 2, 3*
Given the `Library`, `Kafka`, and `EventAndCommands` projects are class libraries with no entry point
When this feature ships
Then their Dockerfiles and Compose service blocks no longer exist, and a regression test prevents any future Dockerfile from repeating the base-image-version-mismatch pattern under a different name

**F-017-US-02: Every service can actually be published and containerized**
*Acceptance criteria: 4*
Given `EventAndCommands.csproj` no longer copies its own `appsettings.json` into every consumer's output
When any of the seven services is published with `dotnet publish -t:PublishContainer`
Then the publish succeeds with no file-conflict error, where today it fails for all seven

**F-017-US-03: Dependency and secret scanning run automatically**
*Acceptance criteria: 5, 6, 7*
Given a pull request is opened against `main`
When CI runs
Then a dependency-vulnerability audit and a secret scan both execute automatically, and a canary test proves the secret scanner would have caught the class of credential leak this project already experienced

**F-017-US-04: Container images build via SDK container support and get scanned in CI**
*Acceptance criteria: 8, 9, 10, 11, 13*
Given a pull request touches a service's source or `.csproj`
When CI runs
Then each of the seven remaining services builds a container image via .NET SDK container support in a parallel matrix and is scanned by Trivy, failing only on project-introduced HIGH/CRITICAL findings, with a 10-minute timeout and no runtime execution of the built image

**F-017-US-05: Dependency updates arrive automatically**
*Acceptance criteria: 12*
Given this feature has merged to `main`
When Dependabot's schedule next runs
Then a pull request proposing a dependency update is opened without manual initiation

---

## Testing Approach: Test-Driven Development (TDD)

**Tests are written first.** During Construction (`/build`), for **every acceptance criterion above**, a **failing test is written and run before any implementation code** — the Red → Green → Refactor cycle:

1. **Red** — write the smallest failing test that pins the acceptance criterion, named with the Given/When/Then language from the matching user story. Run it; confirm it fails for the right reason (logic not implemented — not a syntax/import error).
2. **Green** — write the minimum implementation that makes the test pass. Run the test and the full suite; no regressions.
3. **Refactor** — clean up without changing behavior; suite stays green.

The build loop enforces this at a mandatory **TDD gate** (build Step 9a-bis): implementation code for a criterion may not be written until a failing test for it exists. The only exceptions are pure scaffolding, config-only, and infrastructure-only work — and even those require an **explicit human TDD override**. There is no silent skip. (TDD can be disabled only by editing `CONSTITUTION.md` § Test Gates — the Constitution always wins.)

**Test layers** for this feature: **Unit** (the structural regression tests for Dockerfile/Compose absence and base-image-version matching, added to `AgendaBuddy.AppHost.Tests` alongside `AppHostWiringTest`) plus a **live CI verification pass** for ACs 10 and 12 — this feature has no HTTP surface, so "Integration" in the usual sense does not apply; the equivalent rigor is a real PR that exercises the new jobs, not a mocked one.

---

## Non-Functional Requirements

- The new CI jobs run independently and in parallel with the existing `build-and-test` job — they MUST NOT increase its runtime.
- No secret value may appear in CI logs even when a scan step fails — scanners report file:line, never the secret content.
- The dependency-audit and secret-scan steps MUST run on every pull request to `main`, not only on merge, matching CONSTITUTION §7's "always required" intent.
- No new runtime dependency is introduced for the seven services themselves — every addition here is CI/build-time tooling only.

---

## Out of Scope

- Gateway containerization — the Gateway (F-015's eighth AppHost resource) postdates this feature's original charter (written 2026-08-15; Gateway shipped 2026-08-24) and is not addressed here.
- Fixing or removing the seven remaining hand-written service Dockerfiles, or their smaller hygiene defects (`EXPOSE`/port mismatch against `appsettings.json`, missing `HEALTHCHECK`, restore-before-`COPY` defeating layer caching, unpinned base-image digests). They incidentally stop failing at `dotnet publish` once Requirement 3 lands (the fix is shared), but this feature does not build, scan, or otherwise rely on them — Requirement 7 targets SDK container support instead.
- Retroactively adding `timeout-minutes` to the five existing, currently-untimed CI jobs.
- Any container registry push, image tagging, or versioning scheme — none exists, and `ADR-035` defers cloud deployment.
- Any runtime smoke test (`docker run`) or health check of a built image.
- Repairing the legacy `docker compose up` full-stack path — only 1 of 7 API services is wired into Compose today; this feature does not fix that topology.

---

## Known Risks

- An external outage of Trivy's CVE database or the gitleaks GitHub Action could block an unrelated pull request on a required gate. Accepted — the same class of risk as any external CI dependency; no mitigation planned.
- Pull requests already open when this feature merges will gain new required checks they were opened without. Accepted as a one-time, self-resolving transition cost (rebase or re-trigger resolves it).
- `ubuntu-latest` runners' Docker daemon availability for .NET SDK container support is assumed, verified only on this development machine, not yet in GitHub's actual runner image. If wrong, Construction adds an explicit Docker-setup step — a cheap, well-known fix.
- Trivy's CVE database is not cached between CI runs, so every service-touching PR re-downloads it. Accepted for now; revisit if it becomes a speed or rate-limit problem.

---

## Standards Alignment

_The Nordstrom Standards Readiness gate does not apply to this project (`ADR-042`, `CONSTITUTION.md` §9) — Agenda Buddy is a personal `fererelabs` project, not a Nordstrom enterprise engagement. This section is intentionally omitted from assessment at both Define (Step 6.5) and Plan (Step 17.5); no future gate call site should re-attempt it._

---

## Design Docs

- Architecture: [ARCHITECTURE.md](../design/container-and-cd-hardening/ARCHITECTURE.md)
- Data model: [data-model.md](../design/container-and-cd-hardening/data-model.md) — no changes
- API contracts: [api-contracts.md](../design/container-and-cd-hardening/api-contracts.md) — no changes
- Threat model: [threat-model.md](../design/container-and-cd-hardening/threat-model.md) — Full triage, 6 threats, 2 mitigate-now
- UX review: [ux-review.md](../design/container-and-cd-hardening/ux-review.md) — Skip triage, no UI surface
- Additional: —

---

## Related Episodes

- [EPISODE_aspire-wiring_2026-08-17.md](../episodes/EPISODE_aspire-wiring_2026-08-17.md) — introduced `AppHostWiringTest`, the structural-test pattern this feature's Requirement 2 / AC 1–3 directly reuses.

---

## Approval

**Approved by:** ogdevlabs
**Date approved:** 2026-08-25
**Notes:** Revised and re-approved at Design after two real findings: the Aspire/azd deployment path builds its own container images and never reads the hand-written Dockerfiles (re-scoping Requirement 7 from Dockerfile-based to SDK-container-based), and a live-verified `NETSDK1152` publish conflict affecting all seven services (new Requirement 3).
