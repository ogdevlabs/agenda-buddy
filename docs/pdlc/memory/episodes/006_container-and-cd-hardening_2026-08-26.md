# Episode 006: Container and CI/CD Hardening

**Episode ID:** 006
**Feature name:** Container and CI/CD Hardening
**Feature slug:** container-and-cd-hardening
**Date delivered:** 2026-08-26
**Phase delivered in:** Construction
**Status:** Draft

---

## What Was Built

This episode closed the container and CI/CD hardening gap CONSTITUTION §7 had marked mandatory-but-unimplemented since F-013: an automated dependency-audit + secret-scan gate, and a container-image build+scan gate, neither of which had ever run in this project's CI before. It deleted three broken class-library Dockerfiles (`Library`, `Kafka`, `EventAndCommands`) that published a `net10.0` build onto a `dotnet/runtime:8.0` base and could never run, plus their no-op Compose service blocks, and fixed a second, more severe defect found live during Design: `EventAndCommands.csproj`'s own `appsettings.json` collided with every one of the seven services' own file at publish time, blocking `dotnet publish -t:PublishContainer` for all seven, not just the three broken ones. Two new CI jobs were added to `.github/workflows/dotnet.yml`: `security-scan` (dependency audit + gitleaks secret scan, deliberately unconditional — `if: always()` — after a Party Review finding showed the original path-filter gate would have missed the exact class of leak `ISSUE-002` already experienced) and `docker-build-and-scan` (a 7-service matrix building each remaining service via .NET SDK container support, scanned by Trivy with a severity gate that fails on project-introduced findings and warns on base-image-inherited ones). A canary test empirically proves gitleaks both detects an Atlas-credential-shaped secret and redacts it from CI logs — closing the exact detection gap that let the real Atlas credential ship undetected. Both new third-party GitHub Actions are pinned to full commit SHAs, not mutable tags.

---

## Links

- **PRD:** [PRD_F-017_container-and-cd-hardening_2026-08-25.md](../../prds/PRD_F-017_container-and-cd-hardening_2026-08-25.md)
- **PR:** _not yet opened_
- **Review file:** [REVIEW_container-and-cd-hardening_2026-08-26.md](../../reviews/REVIEW_container-and-cd-hardening_2026-08-26.md)
- **Design docs:** [ARCHITECTURE.md](../../design/container-and-cd-hardening/ARCHITECTURE.md) | [threat-model.md](../../design/container-and-cd-hardening/threat-model.md)

---

## Key Decisions & Rationale

1. Targeted .NET SDK container support (`dotnet publish -t:PublishContainer`), not the seven hand-written service Dockerfiles, for the image-build-and-scan job — this project's actual Aspire/`azd` deployment path never reads those Dockerfiles at all; they serve only the already-broken legacy `docker compose up` path (1 of 7 services wired in). Building and scanning the Dockerfiles would have secured an artifact this project would never ship.
2. `security-scan` runs unconditionally (`if: always()`), not gated on any path filter — found necessary at the Party Review, not planned at Design. The original `api`-filter gate would have skipped scanning on a docs/scripts/Gateway/MobileApp-only PR, exactly the path class the original Atlas credential leak used (per `ISSUE-002`). A secret scanner and dependency audit are cheap enough to run on every PR regardless of what changed.
3. Trivy's severity gate distinguishes findings by `.Results[].Target` — anything under `app/<Service>.deps.json` is project-introduced and fails the build; everything else (base-image OS layer, shared-framework `deps.json`) is inherited from `mcr.microsoft.com/dotnet/aspnet:10.0` and only warns, since this project cannot fix a base-image CVE directly. Verified against real scans of a built service image and the bare base image, not just designed on paper.
4. Two real, previously-undocumented defects were found and fixed in the same gates that found them, not filed for later: `Profession/Dockerfile` had the identical runtime/SDK version mismatch as the three deleted class libraries (caught by the generalized structural guard the moment it ran repo-wide); and `aquasecurity/trivy-action@0.28.0` referenced a tag that doesn't exist upstream (missing the `v` prefix), which would have failed on its first real CI run.
5. A Party Review Critical finding (missing regression tests for 5 acceptance criteria) was fixed before merge rather than deferred — added `SecurityScanAndDockerJobShapeTest` and a fixture-based Trivy severity-gate test, both mutation-tested to confirm they're non-vacuous. See `DECISIONS.md` for the full ADR-047 record of what was fixed vs. accepted as a logged warning.
6. A third real defect surfaced during the Test sub-phase's own Layer 7 security scan (run for the first time using this feature's own tooling rather than by-hand greps): the gitleaks canary script's own fake-password literal tripped the default `generic-api-key` rule, which would have failed F-017's own future PR. Fixed via a `.gitleaksignore` fingerprint entry after confirming an inline `gitleaks:allow` comment alone doesn't survive a diff-range history scan (git history is immutable).

---

## Files Created

- `.github/dependabot.yml` — weekly NuGet + GitHub Actions dependency-update PRs
- `.gitleaks.toml` — custom rule detecting MongoDB/Atlas-style connection strings with embedded credentials
- `.gitleaksignore` — fingerprint suppression for the canary script's own fixture false-positive
- `AgendaBuddy.AppHost.Tests/DockerAndComposeHygieneTest.cs` — generalized structural guard: no Dockerfile/Compose regression, repo-wide runtime/SDK version-parity check
- `AgendaBuddy.AppHost.Tests/PublishContainerTest.cs` — structural regression guard for the `EventAndCommands` appsettings publish conflict
- `AgendaBuddy.AppHost.Tests/PinnedThirdPartyActionsTest.cs` — asserts gitleaks-action/trivy-action are pinned to full commit SHAs
- `AgendaBuddy.AppHost.Tests/SecurityScanAndDockerJobShapeTest.cs` — structural coverage for the gitleaks-step presence, the docker-build-and-scan matrix/timeout/no-run shape, added at the Review gate to close a Critical finding
- `EventsAndCommands.Tests/appsettings.json` — the test project's own copy, replacing reliance on `EventAndCommands`'s transitive copy
- `scripts/trivy-severity-gate.sh` — the actual severity-gate filter for the Trivy step
- `scripts/verify-gitleaks-canary.sh` — CI-wired proof that gitleaks detects and redacts the Atlas-credential-shaped canary
- `scripts/verify-trivy-severity-gate.sh` — CI-wired fixture-based proof of the severity gate's branching logic, added at the Review gate

## Files Modified

- `.github/workflows/dotnet.yml` — added `security-scan` and `docker-build-and-scan` jobs, a new `docker` path filter, `summary` job updates
- `CLAUDE.md` — documented the new CI jobs, security tooling, deleted Dockerfiles, and the `EventAndCommands` publish-conflict precedent; corrected stale test counts
- `Customer/Customer.csproj`, `Provider/Provider.csproj` — removed `ErrorOnDuplicatePublishOutputFiles=false`, a symptom-level suppression of the same root cause `EventAndCommands.csproj`'s fix addresses
- `EventAndCommands/EventAndCommands.csproj` — removed the `CopyToOutputDirectory` metadata that collided with every service's own `appsettings.json`
- `EventsAndCommands.Tests/EventsAndCommands.Tests.csproj` — added its own `appsettings.json` copy item
- `Profession/Dockerfile` — base image corrected `runtime:8.0` → `aspnet:10.0`
- `docker-compose.yml`, `docker-compose.override.yml` — removed the `events`/`kafka-library`/`common-library` no-op service blocks

## Files Deleted

- `EventAndCommands/Dockerfile`, `Kafka/Dockerfile`, `Library/Dockerfile` — class libraries with no entry point, publishing `net10.0` onto a `dotnet/runtime:8.0` base that could never run

---

## Test Summary

| Layer | Status | Passed | Failed | Skipped | Notes |
|-------|--------|--------|--------|---------|-------|
| Unit | pass | 484 | 0 | 0 | 468 at build-loop-done → 484 after the Review/Test fix cycles (9 new structural tests) |
| Integration | pass | 234 | 0 | 0 | Not required by this PRD (no HTTP surface); run anyway for regression safety against a real MongoDB Testcontainer |
| E2E | skip | — | — | — | No command exists in this project; not a required §7 gate |
| Performance | skip | — | — | — | No command exists in this project; not a required §7 gate |
| Accessibility | skip | — | — | — | No command exists in this project; not a required §7 gate |
| Visual Regression | skip | — | — | — | No command exists in this project; not a required §7 gate |

**Constitution gates:** All required gates passed. §7's security scan (dependency audit + secret scan) ran automated for the first time ever on this project, using this feature's own new tooling rather than by-hand greps — and found a real defect (the canary script's own false-positive) the same way it's meant to, fixed live via `.gitleaksignore`.

---

## Deployment Record

- **Deployed to:** _not yet shipped — Construction only_
- **CI/CD method:** GitHub Actions — `.github/workflows/dotnet.yml` (this feature's own subject)
- **Custom deploy artifact used:** no — default pipeline
- **Deployment Review Party:** not convened — not yet at Ship
- **Config changes introduced:** two new CI jobs (`security-scan`, `docker-build-and-scan`), `.github/dependabot.yml`, `.gitleaks.toml`, `.gitleaksignore`
- **New tags recorded:** none yet
- **Rollback tested:** n/a — not yet deployed
- **Overrides used:** TDD gate override for F-017-T03/T08 (infra-only, human-confirmed — see STATE.md Guardrail Log)
- **DEPLOYMENTS.md updated:** no — pending `/ship`

---

## Known Tradeoffs & Tech Debt Introduced

- **[TD] AC10 (the `docker` path filter's live-PR trigger) is unverified pre-merge** — no PR has been opened against `feat/F-017-container-and-cd-hardening` yet. Must be confirmed live the moment a real PR opens; this is `/ship`'s responsibility.
- **[TD] `.github/dependabot.yml`'s AC12 (a real Dependabot PR opens post-merge) is deferred by nature** — cannot fire before this feature merges to `main`.
- **[TD] One flaky test run** — `AgendaBuddy.AppHost.Tests` showed 10 failures in 1 of 5 full-suite runs (77/87), all other runs clean at 484/484. Suspected resource contention across ~13 concurrently-run test assemblies, not a logic bug. Worth investigating before it becomes a source of false-red PRs.
- **[TD] Gateway has zero CI coverage of any kind** — not in any path filter in `dotnet.yml` (pre-existing F-015 gap, surfaced but not introduced by this review).
- **[TD] `Customer/Dockerfile` does not exist at all** — discovered while verifying the Dockerfile hygiene guard; pre-existing, out of scope (the image-build job uses SDK container support, not Dockerfiles, so this doesn't block anything).
- **[TD] Duplicate `RepoRoot()` test helper** — copy-pasted across `DockerAndComposeHygieneTest`, `PinnedThirdPartyActionsTest`, and `PublishContainerTest` (YAGNI `shrink:` finding, accepted as low-priority polish).

---

## Agent Team

**Always-on:**
- **Neo** (Architect) — architecture review, PRD conformance, YAGNI lens, cross-talk synthesis
- **Echo** (QA Engineer) — test coverage review, found the Critical regression-coverage gap
- **Phantom** (Security Reviewer) — threat-mitigation verification, promoted the path-filter finding to Important
- **Jarvis** (Tech Writer) — docs/CHANGELOG review, found the `CLAUDE.md` drift

**Auto-selected for this feature:**
- **Pulse** (DevOps) — Wave Kickoff standup contribution on CI/Compose coordination

---

## Reflect Notes

**What went well:**
<!-- Filled at Reflect -->

**What broke or slowed us down:**
<!-- Filled at Reflect -->

**What to improve next time:**
<!-- Filled at Reflect -->

**Cycle time:** Inception (2026-08-25) to Construction complete (2026-08-26): ~1 day
**Test pass rate:** 100% (484/484 backend, 234/234 integration; 0 skipped in either)

**Planning accuracy:** Readiness at plan: Fair (1 gap — `security-ac-unmaterialized` — caught and fixed in-party before Construction started). Surfaced later: 2 within-task scope widenings found at the Wave 1/2/3 standups (Profession/Dockerfile, EventsAndCommands.Tests appsettings, Customer/Provider suppression cleanup) plus 1 Critical and 4 Important findings at Review, and 1 real defect found at Test's own Layer 7 run — none were plan misses in the readiness-gap sense; all were genuinely undiscoverable before the code existed to inspect.

---

## Approval

**Reviewed by:** _pending_
**Date approved:** _pending_
**Notes:** Draft — awaiting human review before commit.
