---
feature: container-and-cd-hardening
topic: progressive-thinking
date: 2026-08-25
mode: solo
participants: Atlas, Neo, Echo, Phantom, Bolt, Friday, Muse, Pulse, Jarvis
---

# Meeting Minutes: Progressive Thinking
## Feature: container-and-cd-hardening | 2026-08-25

**Mode:** Solo
**Participants:** Atlas (Product Manager, facilitator), Neo (Architect), Echo (QA), Phantom (Security), Bolt (Backend), Friday (Frontend), Muse (UX), Pulse (Deployment), Jarvis (Tech Writer)

---

## Context

F-017 `container-and-cd-hardening` is next on the roadmap after F-015. Socratic discovery (3 rounds, Sketch mode) established: delete the 3 class-library Dockerfiles/Compose services (`Library`, `Kafka`, `EventAndCommands` — broken `runtime:8.0` base, no `ENTRYPOINT`); implement CONSTITUTION §7's security scan via `dotnet list package --vulnerable --include-transitive` + gitleaks; add a build+scan-only (no push) image-build CI job for the 7 remaining service Dockerfiles, gated on Dockerfile-path changes; add Trivy for container-image-layer scanning (distinct from the source-level §7 scan); Gateway gets no Dockerfile in this feature; the 7 remaining Dockerfiles' smaller hygiene defects (EXPOSE mismatch, no HEALTHCHECK, restore-before-COPY, unpinned digests) are explicitly out of scope. This meeting pressure-tests that understanding before Define.

---

## Discussion

### Round 1 — Concrete (what do we know for certain?)

**Bolt (Backend Engineer):** Confirmed from `08-cicd-deploy.md`: the 7 service Dockerfiles (`Booking` read directly, 5 others inferred identical with the project name substituted, `Identity` read directly) build on `aspnet:10.0`/`sdk:10.0` correctly — only the 3 class-library ones are on the broken `runtime:8.0` base. `EXPOSE 8080/8081` contradicts every service's real Kestrel bind (`localhost:603x`), confirmed out of scope for this feature per Socratic Round 2.

**Echo (QA):** No test currently asserts anything about Dockerfiles, Compose, or CI security scanning — this whole surface is untested. `AppHostWiringTest.NoServiceBindsAHardcodedHostPort` is the direct structural-test precedent Socratic Round 3 named for the "3 Dockerfiles are gone" AC.

**Phantom (Security):** Confirmed: CONSTITUTION §7 marks the security scan "always required, cannot be unchecked," and it has never run in CI — only by hand, 5 documented times (Guardrail Log). The committed Atlas credential (`ISSUE-002`) is the concrete, already-realized cost of that gap.

**Pulse (Deployment):** Confirmed: `.github/workflows/dotnet.yml` is the only workflow. Five jobs, none builds a Docker image, none scans anything. Path-filter gap confirmed: Dockerfile/Compose/`global.json`/`scripts/` changes trigger zero jobs (`08-cicd-deploy.md:59`).

**Neo (Architect):** Confirmed: no container registry exists anywhere in this repo or its secrets. ADR-035 defers cloud deploy until every pending feature ships. No architectural dependency from F-017 to anything else on the roadmap.

**Jarvis (Tech Writer):** Confirmed: `CLAUDE.md` and the context catalog both already describe the pipeline accurately as gapped; no doc currently claims the security scan or image build exist, so there's no misdocumentation to correct — only gaps to close.

**Friday (Frontend) / Muse (UX):** Nothing in our domains — this feature has no UI/UX surface. Confirmed absence, not a gap.

**Atlas synthesizes:** We know for certain — 3 Dockerfiles are broken and shouldn't exist as images at all; the §7 gate has never run automatically; CI has zero Docker awareness at all (build, scan, or trigger-path); no registry exists; no roadmap dependency blocks this feature.

### Round 2 — Inferential (what can we reasonably infer?)

**Pulse:** Infer the new image-build job belongs in its own job (not folded into `build-and-test`) — the existing job matrix is already path-filtered by `changes`, and Docker builds have a materially different runtime profile (slower, needs BuildKit) than `dotnet build`.

**Bolt:** Infer the 7 remaining Dockerfiles should build in the **same job that scans them** (build → Trivy scan → done), rather than a separate scan job re-pulling the image — simpler dependency graph, one artifact handoff.

**Phantom:** Infer the gitleaks step should run on the **full history of the PR's diff**, not just the working tree — a secret introduced and then "fixed" in a later commit on the same PR would otherwise slip through. This wasn't explicit in Socratic discovery.

**Neo:** Infer this feature should also add `.github/dependabot.yml` — `08-cicd-deploy.md` names its absence explicitly, and it's a natural companion to the dependency-audit gate (Dependabot prevents the vulnerability from landing; the audit catches it if it does anyway). Not in the original 4-item scope, but cheap and directly adjacent.

**Echo:** Infer the structural "3 Dockerfiles are gone" test belongs in an existing test project close to where `AppHostWiringTest` already lives (`AgendaBuddy.AppHost.Tests`), for discoverability, rather than a new test project.

**Atlas synthesizes:** Two inferences worth confirming before Define: Neo's Dependabot suggestion (scope addition) and Phantom's full-diff-history framing for gitleaks (a correctness detail, not a scope question).

### Round 3 — Consequential (what follows from our inferences?)

**Bolt/Pulse:** One new CI job (`docker-build-and-scan` or similar), gated on a new `docker` path filter (Dockerfiles + `docker-compose*.yml` + the 7 service dirs), running: `docker build` per remaining service → Trivy scan of the built image → fail on HIGH/CRITICAL (mirroring the dependency-audit's severity bar).

**Echo:** New test file (or extension of `AppHostWiringTest`'s file) asserting the 3 Dockerfiles/Compose blocks are absent; new CI-log-level verification (not a unit test) that the new job actually ran and passed on a real PR, per Socratic Round 3 Q4.

**Phantom:** Security consequence: the dependency-audit step needs the `ADR-030` `NU1903` suppression carried through explicitly, or the very first CI run on `main` fails on a known-accepted risk — this would be an immediate, embarrassing false-positive block on the feature's own PR.

**Jarvis:** Doc consequence: `CLAUDE.md`'s "Key Files" and "Development" sections need a line each — the new required CI jobs, and (if Neo's Dependabot suggestion is accepted) a note that dependency PRs now arrive automatically.

**Muse/Friday:** No consequences — confirmed non-applicable again.

### Round 4 — Speculative (what might we be missing?)

**Phantom:** What if Trivy's base-image CVE database flags something in `mcr.microsoft.com/dotnet/aspnet:10.0` itself — a base image this project doesn't control? Unlike the SSH.NET case, there's no existing ADR to fall back on. Need a stated policy: fail the build, or warn-only for base-image-inherited CVEs vs. fail on CVEs from packages this project actually adds.

**Echo:** What if a future contributor adds a **ninth** Dockerfile (e.g., for a new service) that repeats the exact `runtime:8.0` mistake? The structural test only guards the 3 *named* files — it doesn't guard against the *pattern* recurring elsewhere. Consider a broader assertion: every Dockerfile's final-stage base image matches the SDK-stage's major version.

**Neo:** What if this feature's new "Dockerfile changed → CI runs" path filter has the same blind spot as the *existing* filters — i.e., it's added to the `changes` job's outputs but never wired into an `if:` condition, repeating the exact "library output computed and never consumed" bug already on file (`08-cicd-deploy.md:55`)? Worth an explicit acceptance criterion that the new filter is actually consumed.

**Pulse:** What if `docker build` in CI needs `buildx`/QEMU for multi-arch, given local dev is on Apple Silicon (per this session's own build troubleshooting) but CI runs `ubuntu-latest` (x64)? No multi-arch requirement exists today (Compose/AppHost only run locally), so this is speculative risk, not a real requirement — flag and drop.

**Atlas synthesizes:** Echo's broader-pattern-guard and Neo's filter-wiring risk are the two speculative points worth carrying into Define as explicit requirements, not just risks. Phantom's base-image-CVE policy needs a user decision (see Round 5). Pulse's multi-arch point is speculative with no present trigger — noted, not actioned.

### Round 5 — Conflicting (where do we disagree?)

No agent-vs-agent conflict arose — Neo's Dependabot suggestion (Round 2) and Phantom's base-image-CVE policy question (Round 4) are both open items but nobody on the team disagrees with another; they're additions/decisions needing the user's call, not internal disputes.

*Outcome: no cross-agent conflict — two items escalate to the user as open decisions, not disagreements.*

**Atlas escalates to the user:**

1. **Dependabot** (Neo, Round 2): add `.github/dependabot.yml` as part of this feature (cheap, directly adjacent to the dependency-audit gate), or leave it filed separately since it wasn't in the original 4-item scope?
2. **Base-image CVE policy** (Phantom, Round 4): when Trivy flags a CVE inherited from `mcr.microsoft.com/dotnet/aspnet:10.0` itself (not from anything this project added), should the build fail, or warn-only? There's no existing ADR precedent for this the way `ADR-030` covers SSH.NET.

### Round 6 — Strategic (what should we prioritize?)

**Neo:** #1 priority — get the CI trigger-path wiring right (Speculative Round 4). A new job that silently never runs is worse than no job; it repeats the exact failure class that shipped the `runtime:8.0` defect.

**Phantom:** #2 — the `ADR-030` suppression must land in the same PR as the dependency-audit step, atomically. A gate that blocks its own introduction on a pre-existing accepted risk is a bad first impression and invites disabling the gate instead of fixing the suppression.

**Bolt:** #3 — delete-and-verify the 3 broken Dockerfiles/Compose services before adding anything new. Simplest, lowest-risk, immediately valuable independent of the CI work.

**Echo:** #4 — the structural regression test (3 Dockerfiles gone) plus Echo's Round 4 broader-pattern guard, both landed together, so this class of defect can't recur in any form.

**Pulse:** #5, safe to simplify — Trivy's exact severity threshold and output format can start conservative (fail on HIGH/CRITICAL only, matching the dependency-audit's existing bar) rather than over-engineering a custom policy on day one.

**Atlas's ranked design priorities for Define/Design:**
1. CI trigger-path wiring for any new job (Neo) — must not repeat the dead-filter bug.
2. `ADR-030` suppression ships atomically with the dependency-audit step (Phantom).
3. Delete the 3 broken Dockerfiles/Compose services first, independent of CI work (Bolt).
4. Structural regression test, generalized to guard the *pattern* not just the 3 named files (Echo).
5. Trivy severity threshold starts conservative — HIGH/CRITICAL only (Pulse).

---

## Conclusion

The team found no internal disagreement requiring resolution among themselves. Two decisions need the user's call before Define: whether to add Dependabot in this feature, and what policy governs a Trivy finding inherited from the base image itself rather than introduced by this project. Everything else from Socratic discovery held up under pressure-testing, with two speculative risks (dead CI filter, narrow structural test) elevated to explicit requirements for Define.

---

## Next Steps

| # | Action | Owner | Notes |
|---|--------|-------|-------|
| 1 | Confirm Dependabot in/out of scope | Human | Round 5 escalation #1 |
| 2 | Confirm base-image-CVE policy (fail vs. warn) | Human | Round 5 escalation #2 |
| 3 | PRD requirement: new CI filter must be wired into an `if:` condition, verified | Atlas → Neo (Define/Design) | Round 4/6 |
| 4 | PRD requirement: `ADR-030` suppression ships in the same PR as the audit step | Atlas → Neo | Round 3/6 |
| 5 | PRD requirement: structural test generalized to the base-image-version pattern, not just 3 named files | Atlas → Echo | Round 4/6 |

---

## Escalation

**1. Dependabot (`.github/dependabot.yml`):** in scope for F-017, or filed separately? Not part of the original 4-item feature record; Neo flagged it as cheap and adjacent.
**User's answer:** Yes, include it in F-017.

**2. Trivy finding inherited from the base image itself** (not introduced by this project) — fail the build, or warn-only? No existing ADR precedent covers this case the way `ADR-030` covers SSH.NET.
**User's answer:** Warn only — this project cannot fix a base-image CVE directly; failing the build on something unfixable would block unrelated PRs.
