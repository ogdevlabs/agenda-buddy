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

## Adversarial Review
_Not run._

## External Context
_None ingested._

## Discovery Summary
_Pending._
