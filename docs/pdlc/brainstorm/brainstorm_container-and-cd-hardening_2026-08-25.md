---
feature: container-and-cd-hardening
date: 2026-08-25
status: in-progress
last-updated: 2026-08-25T20:29:23Z
approved-by:
approved-date:
prd:
---

# Brainstorm Log: container-and-cd-hardening

## Divergent Ideation
_Not run._

## Socratic Discovery

**Completed:** 2026-08-25T20:29:23Z
**Interaction mode:** Sketch

### Round 1 — Problem Statement

**Q1:** What problem does this specific feature solve?
**A:** Three Dockerfiles (`Library`, `Kafka`, `EventAndCommands`) publish `net10.0` builds onto a `dotnet/runtime:8.0` base and cannot run at all — a defect from F-011's .NET 10 upgrade, invisible because CI never builds a single Docker image. Compounding it, those three are class libraries with no `ENTRYPOINT`; Compose declares them as services anyway. Separately, CONSTITUTION §7's "always required, cannot be unchecked" security scan has never been implemented in CI — concretely costly already, since a secret scanner would have caught the committed Atlas credential (`ISSUE-002`). (Source: `docs/pdlc/context/08-cicd-deploy.md` "Class-library Dockerfiles" + "Pipeline-wide gaps"; `STATE.md` Guardrail Log, `required_gate_unmet` 2026-08-18.)

**Q2:** Who specifically will use this feature?
**A:** Not end-users at all — entirely internal/operational. The maintainer running `docker compose` or eventually cutting a release image, and CI itself, which today has zero image-build or security-scan coverage. (Source: `INTENT.md` has no ops/maintainer persona; feature record scope is pure CI/Docker/security-gate.)

**Q3:** What does success look like? What metric moves, and by how much?
**A:** Today 0 of the 3 class-library Dockerfiles run at all, and the security-scan gate is satisfied "by hand" at every ship gate (5 documented occurrences in the Guardrail Log, F-013 through F-021) rather than by CI. Target: those 3 images are deleted (not fixed — they shouldn't exist), the remaining Dockerfiles build successfully in CI on every PR that touches them, and the security scan runs automatically in CI rather than by hand at ship time. (Source: `08-cicd-deploy.md`; repeated `required_gate_flagged_accepted ... by hand ... F-017 still owns automating it` Guardrail Log entries.)

**Q4:** What are the technical constraints or dependencies?
**A:** No roadmap dependency — F-017 is next, unblocked. Must not repeat the exact gap that let the `runtime:8.0` defect ship silently: today, changes to any `Dockerfile`, `docker-compose*.yml`, `global.json`, or `scripts/` trigger zero CI jobs — any new image-build job must trigger on those paths. Two scope questions carried into Round 2: (a) does the Gateway (F-015's 8th AppHost resource, postdates this feature record) get a Dockerfile here? (b) "add image build/scan/push to CI" implies a registry — none exists, and cloud deploy is deferred by ADR-035, so should this stop at build+scan with no push? (Source: `08-cicd-deploy.md`; `ADR-035`; Gateway confirmed to have no Dockerfile.)

### Round 2 — Future State / Key Capabilities

**Q1:** What tool satisfies CONSTITUTION §7's "dependency audit + secret scan"?
**A:** `dotnet list package --vulnerable --include-transitive` for the dependency audit — the exact command already run by hand at every ship gate, promoted into a CI step directly. **gitleaks** for the secret scanner (GitHub Action available, no backend dependency, scans the diff) rather than hand-rolling the ad hoc "six patterns" grep used manually to date.

**Q2:** Should the new image-build CI job push anywhere, or build+scan only?
**A:** Build + scan only, no push. No container registry exists anywhere in the repo, and ADR-035 defers cloud deployment until every pending feature ships and tech debt clears. Runs on every PR touching a `Dockerfile` — the exact trigger gap that let the `runtime:8.0` defect ship unnoticed.

**Q3:** Does this feature also fix the smaller cataloged Dockerfile defects (EXPOSE/port mismatch, missing HEALTHCHECK, restore-before-COPY layer-cache defeat, unpinned base-image digests) on the 7 service Dockerfiles that stay?
**A:** No — out of scope for F-017, filed separately. The feature record's four items are specific; the hygiene issues are real but orthogonal, and none currently break anything.

**Q4:** Does the Gateway get a Dockerfile in this feature?
**A:** No. F-017's feature record predates F-015 (Gateway shipped 2026-08-24) and never mentions it. A one-line Out-of-Scope note in the PRD records this explicitly so it isn't silently forgotten.

### Round 3 — Acceptance Criteria

**Q1:** "Image build/scan/push" (item 4) vs. CONSTITUTION §7's "security scan" (item 3) — same scan, or two different ones?
**A:** Two different things, both needed. §7 is source-level (.NET package vulnerabilities + secrets in the diff). Item 4's "scan" is the built container image itself — OS-layer CVEs a .NET-package audit never sees. **Trivy** for the image scan (free, no backend, single GitHub Action).

**Q2:** Acceptance test for "the three broken Dockerfiles are gone"?
**A:** A structural regression test (same pattern as `AppHostWiringTest.NoServiceBindsAHardcodedHostPort`) asserting `Library/Dockerfile`, `Kafka/Dockerfile`, `EventAndCommands/Dockerfile` don't exist, and the two Compose files have no `events`/`kafka-library`/`common-library` service blocks.

**Q3:** How does the new required security-scan gate handle the already-accepted SSH.NET HIGH (`ADR-030`) so it doesn't fail every future PR on a known, accepted risk?
**A:** Codify `ADR-030`'s disposition as the existing per-package `NU1903` suppression, so the CI dependency-audit step passes on the known exception but still fails on anything new.

**Q4:** Acceptance proof that the new image-build CI job actually works?
**A:** A live test — edit one Dockerfile on a throwaway branch/PR, confirm the job triggers, builds successfully, and Trivy runs against it. Proven once at Construction's closing verification, the same "found live, not by inspection" standard every prior feature here has used.

## Progressive Thinking (Agent Team Meeting)

**MOM:** [container-and-cd-hardening_progressive-thinking_mom_2026_08_25.md](../mom/container-and-cd-hardening_progressive-thinking_mom_2026_08_25.md)

### Confirmed Facts
The 3 broken Dockerfiles (`Library`, `Kafka`, `EventAndCommands`) are the only ones misconfigured — the 7 service Dockerfiles are correct on the base-image axis. CI has zero Docker or security-scan awareness today. No container registry exists anywhere in the repo. No roadmap dependency blocks this feature.

### Accepted Inferences
The new image-build job builds then scans in one job (not two separate jobs). gitleaks scans the full PR diff history, not just the working tree, so a secret added-then-removed within the same PR is still caught. Dependabot (`.github/dependabot.yml`) is added in this feature (user-confirmed). The structural regression test lives alongside `AppHostWiringTest`.

### Key Consequences
One new CI job, gated on a new `docker` path filter (Dockerfiles + `docker-compose*.yml` + the 7 service dirs): `docker build` → Trivy scan → fail on HIGH/CRITICAL from project-introduced findings. The `ADR-030` `NU1903` suppression must ship in the same PR as the dependency-audit step, or the feature's own first CI run fails on an already-accepted risk. `CLAUDE.md` needs a line for the new required jobs and for Dependabot.

### Risks & Unknowns
A new CI path filter that's computed but never wired into an `if:` condition would make the whole feature a silent no-op — this pipeline has that exact bug already (the `library` filter). A structural test guarding only the 3 *named* Dockerfiles wouldn't stop a ninth file repeating the same `runtime:8.0` mistake — generalize to "final-stage base image major version matches the SDK stage's." Trivy flagging a CVE inherited from the base image itself has no existing ADR precedent — resolved below.

### Conflicts Resolved
No agent-vs-agent disagreement arose. Two open decisions were escalated to the user directly:
1. **Dependabot in scope for F-017?** User: **yes.**
2. **Base-image-inherited Trivy findings — fail or warn?** User: **warn only** — this project can't fix a base-image CVE directly; failing the build on something unfixable would block unrelated PRs.

### Design Priorities
1. Verify the new CI filter is actually consumed by an `if:` condition (not dead, like `library`).
2. `ADR-030` suppression ships atomically with the dependency-audit step.
3. Delete-and-verify the 3 broken Dockerfiles/Compose services first, independent of the CI work.
4. Structural test generalized to the base-image-version pattern, not just the 3 named files.
5. Trivy severity threshold starts conservative — HIGH/CRITICAL only, and only for project-introduced findings (base-image-inherited ones warn).

## Adversarial Review

**Completed:** 2026-08-25T21:32:44Z

### Findings
1. Assumption checked directly against `docker-compose.yml`/`docker-compose.override.yml`: no other Compose service `depends_on` `events`, `kafka-library`, or `common-library` — deletion is safe on that axis. Verified, not a real risk.
2. Scope leak: the new image-build job (build+scan only, no run) could collide with the explicitly-out-of-scope EXPOSE/port-mismatch/HEALTHCHECK defects if it ever grows a smoke test.
3. Success-metric fragility: "the scan runs" is a binary on job existence, not on whether the chosen tool (gitleaks default ruleset) actually recognizes this project's real leaked-secret shape (a MongoDB/Atlas connection string).
4. Technical risk blindspot: unverified assumption that `ubuntu-latest` GitHub runners have a working Docker daemon + BuildKit for `docker build` out of the box.
5. Dependency blindspot: Trivy's CVE database download isn't cached anywhere in the plan — cost/rate-limit risk on every Dockerfile-touching PR.
6. Edge case silence: no defined behavior for a PR that *deletes* a Dockerfile rather than editing one.
7. Requirement/documentation drift: CONSTITUTION §7 lists "security scan" as one checkbox; this feature adds a third distinct scan type (Trivy image scan) that §7's wording doesn't reflect.
8. Definition-of-done gap: every prior feature was verified against a live AppHost; a CI-only artifact needs a different, explicitly stated verification method.
9. Timeline/sizing naivety: the original 4-bullet feature record grew materially during Discover (Dependabot, Trivy, generalized structural test, atomic ADR-030 suppression, CI-filter-verification requirement) — real scope is bigger than it looked at claim time.
10. User-problem framing risk: framing this as "Dockerfiles are broken" undersells the real driver — preventing another Atlas-credential-class incident (a still-open P0).
11. Scope leak on the legacy dev path: `docker compose up` was already a broken full-stack path (1 of 7 services wired in) before this feature and stays broken after — worth an explicit PRD note that this feature doesn't touch that.

### Follow-up Q&A

**Q1 (Finding #2 — build-only vs. smoke-test):** Should the PRD explicitly state the new CI job is build+scan only, no `docker run`/smoke test?
**A:** Yes — confirmed explicitly in the PRD. Decouples this feature entirely from the out-of-scope EXPOSE/HEALTHCHECK hygiene issues.

**Q2 (Finding #9 — sizing):** One PRD, or split into a multi-stage program like the API-refactor work?
**A:** One PRD, split into 3 independently-mergeable waves at Plan: Wave 1 = delete the 3 Dockerfiles + generalized structural test; Wave 2 = dependency-audit + secret-scan gate + Dependabot + ADR-030 suppression; Wave 3 = image-build job + Trivy.

**Q3 (Finding #3 — does the chosen tool actually catch our real leak-shape):** Formal acceptance criterion, or just an implementation detail verified without a dedicated AC?
**A:** Formal AC — a canary test at Construction's closing verification: run gitleaks against a fixture matching the leaked-credential's shape (not the real value) and confirm it fires; add a custom rule if the default ruleset misses it. Matches this project's existing convention of 100% test-first ACs.

## External Context
_None ingested._

## Edge Case Analysis

**Completed:** 2026-08-25T21:36:21Z

### Findings

| # | Category | Scenario | Trigger Condition | Addressed? | Risk if Unhandled |
|---|----------|----------|--------------------|------------|--------------------|
| 1 | Empty/boundary data | PR edits `docker-compose.yml` only, no Dockerfile touched | Compose-only change | Yes (already scoped in the path filter) | — |
| 2 | Scale/load | One PR touches all 7 remaining service Dockerfiles at once | Bulk base-image bump | No | Unclear sequential vs. matrix |
| 3 | Invalid input | A Dockerfile has a syntax error unrelated to this feature | Malformed Dockerfile in a PR | No | No `timeout-minutes` anywhere in this pipeline — a bad build could hang |
| 4 | Integration failure | Trivy's CVE-DB fetch or the gitleaks Action is rate-limited/down | Transient upstream outage | No | Unrelated PR blocked on a required gate for reasons unrelated to its changes |
| 5 | Migration/transition | PRs already open when this feature merges gain new required checks | Existing open PR + this feature merges | No | Those PRs unexpectedly blocked until rebased |
| 6 | Partial completion | Matrix build across 7 services: some succeed, one fails | One broken Dockerfile among many | Resolved by #2's matrix decision | — |
| 7 | Permission boundary | Dependabot needs write access to open PRs | Enabling Dependabot via the yml file alone | No | Config file alone might not activate it if repo Dependabot settings are off |

### Triage Decisions

| # | Decision | Notes |
|---|----------|-------|
| 1 | Out of scope (already addressed) | Round 3's Consequences already scoped the path filter to include `docker-compose*.yml` |
| 2 | In scope | AC: new image-build job uses a GitHub Actions matrix, one entry per remaining service Dockerfile |
| 3 | In scope | AC: new job carries `timeout-minutes: 10`. Explicitly **not** retroactive to the 5 existing untimed jobs — real pre-existing gap, but out of scope for F-017, same treatment as the EXPOSE/HEALTHCHECK exclusion |
| 4 | Known risk | No clean fix for an external dependency outage; accepted, same class as other external-dependency risk already accepted in this project |
| 5 | Known risk | One-time transition cost, self-resolving via rebase; not worth engineering around |
| 6 | Out of scope (resolved by #2) | A matrix build reports per-entry status natively |
| 7 | In scope (verification, not design) | AC: confirm live at Construction that a Dependabot PR actually opens post-merge, not just that the config file exists |

## Discovery Summary
_Pending._
