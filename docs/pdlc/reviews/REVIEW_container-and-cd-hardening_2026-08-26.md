# Review — container-and-cd-hardening (F-017)

**Date:** 2026-08-26
**Reviewers:** Neo (Architecture/PRD/YAGNI), Phantom (Security), Echo (Test coverage/quality), Jarvis (Docs/contracts)
**Mode:** Party Review — all four spawned in parallel as independent subagents, cross-talk on two interconnected threads
**Blast radius:** Skipped — docs/config-only diff (see `BLAST-RADIUS_container-and-cd-hardening_2026-08-26.md`)
**Muse:** Did not join — `ux-review.md` Step 10.6 triage was Skip (no UI surface)

---

## Critical

### C1 — ✅ FIXED (commit `7cefae1`) — ACs 6, 8, 9, 11, 13 have zero committed regression test, and this was not an approved TDD exception (Echo, cross-talked with Neo)

**Resolution:** added `SecurityScanAndDockerJobShapeTest` (5 tests: gitleaks step presence/AC6, matrix service list + no-suppressed-failures/AC8, 10-min timeout on the new job only/AC11, no docker run/health-check/registry-push/AC13) and `scripts/verify-trivy-severity-gate.sh` (4 synthetic-fixture cases for AC9's project-vs-base-image branching, wired as a CI step alongside the gitleaks canary). All 9 new assertions mutation-tested — each confirmed red against a deliberately broken version of what it guards, then reverted. `AgendaBuddy.AppHost.Tests`: 87 → 92. Backend suite: 478 → 483, 0 failing.

`AgendaBuddy.AppHost.Tests` has structural tests for AC1-3 (`DockerAndComposeHygieneTest`), AC4 (`PublishContainerTest`), AC7/14/15 (`PinnedThirdPartyActionsTest` + `verify-gitleaks-canary.sh`) — but **nothing** asserts:
- AC6: the gitleaks step exists in `security-scan`
- AC8: `docker-build-and-scan`'s matrix covers all 7 services and fails if any entry fails
- AC9: `trivy-severity-gate.sh`'s project-vs-base-image branching logic (Echo hand-built synthetic Trivy JSON fixtures and verified the logic is *currently* correct, but nothing pins it down for regression)
- AC11: `timeout-minutes: 10` is present on the new job only, not the 5 pre-existing jobs
- AC13: no `docker run`/health-check anywhere in the job

STATE.md's Guardrail Log records an explicit human TDD-gate override for **T03/T08 only** (AC5, AC12 — genuinely infra-only, live-verification-only). T04/T06/T07 were **not** covered by that override and were built "red-test-first as normal" per the same log — but the red-green verification each did was live/ephemeral (shell commands run during the build session), not committed as a repeatable artifact. Contrast with T05's canary script, which *is* wired as a real, repeatable assertion.

**Neo's synthesis (architectural implications):** this is a real gap, and it's a pointed one — a CI-hardening feature whose own new CI wiring has no regression coverage for most of its shape. If someone later removes the gitleaks step, drops the timeout, or breaks the severity-gate's branching, nothing in the suite catches it before a live CI run does (or doesn't). This doesn't need a DECISIONS.md entry — it's a straightforward test-coverage gap with an established fix pattern already in this same diff (`PinnedThirdPartyActionsTest`, `DockerAndComposeHygieneTest` both structurally assert `dotnet.yml` content). Recommend **Fix**: add one structural test class asserting the gitleaks step's presence (AC6), the matrix service list + `fail-fast`/timeout shape (AC8/AC11), the absence of `docker run`/health-check in the job (AC13), and commit Echo's synthetic-fixture verification of `trivy-severity-gate.sh` as a real script-level test (AC9) rather than a one-off manual check.

---

## Important

### I1 — ✅ FIXED (commit `521a7ce`) — `security-scan`'s path-filter coverage excluded docs/scripts/Gateway/MobileApp, reopening the exact leak vector this feature exists to close (Neo, promoted to a standalone security finding by Phantom cross-talk)

`security-scan`'s `if:` (`dotnet.yml:331`) gated on `needs.changes.outputs.api == 'true'` — the `api` filter covers backend service/Library/EventAndCommands/test directories only. A PR touching **only** `docs/`, `scripts/`, `Gateway/`, or `MobileApp/` ran zero dependency-audit or gitleaks scanning.

**Phantom's verdict, cross-talked:** promoted to its own Important finding, not just an architecture cross-reference. `docs/issues/ISSUE-002-atlas-credential-rotation.md` states the original Atlas credential leak lived not only in `appsettings*.json` but in **two files under `docs/pdlc/context/`** that a documentation backfill copied it into — the exact path class this filter excludes, not a hypothetical. Also: the pre-existing F-013 credential grep in `build-and-test` has the identical `api`-gated condition, so this hole predates F-017 — but F-017's new `security-scan` job was the opportunity to close it and inherited the same gap instead.

**Resolution:** `security-scan` now declares `if: always()` — no path filter at all, runs unconditionally on every PR. New test `SecurityScanJobRunsUnconditionallyOnEveryPullRequest` locks this in. `ARCHITECTURE.md` and the `summary` job's report table updated to match.

### I2 — ✅ RESOLVED at Ship — AC10's "live test PR" verification has not happened yet (Neo)

PRD AC10 and `ARCHITECTURE.md` require the `docker` path filter to be verified live by observing a real PR trigger the job. `feat/F-017-container-and-cd-hardening` has no remote/PR yet (`git branch -a` shows no `remotes/origin/feat/F-017-...`). Unlike AC12 (Dependabot — which *cannot* fire before merge by nature, and is correctly logged as deferred), AC10 *could* have been verified pre-merge via a real PR and wasn't. Currently unverified, not deferred-by-design.

**Resolution:** PR #48 opened at Ship. All 7 `Docker — <Service>` matrix jobs genuinely triggered and ran (confirmed via `gh pr checks 48`) — AC10 is now live-verified, not just anticipated. Confirmed a second time on the post-merge Dependabot consolidation PR (#67).

### I3 — ✅ FIXED (commit `ebabba7`) — `CLAUDE.md` described a CI pipeline/toolchain that no longer matched reality (Jarvis)

No mention anywhere of `gitleaks`, `trivy`, `.gitleaks.toml`, or `.github/dependabot.yml`. The CI pipeline description omitted both new jobs. Project Structure didn't note `Library`/`Kafka`/`EventAndCommands` no longer have Dockerfiles. No pointer to the `EventAndCommands.csproj` publish-conflict precedent for a future class library hitting the same bug. Key Files had no entry for either new script or the two new config files. "Security controls that default OFF" didn't cross-reference the new always-on CI gates.

**Resolution:** all of the above added to `CLAUDE.md`, plus the stale 867/468 test counts corrected to 883/484.

### I4 — Full backend suite flaked once in 5 runs, specifically on the tests proving this feature's core bug fix (Echo)

One run of five showed 10 failures in `AgendaBuddy.AppHost.Tests` (77/87) when run as part of the full `agenda-buddy-backend.slnf`, including both `Customer`/`Provider` `ErrorOnDuplicatePublishOutputFiles` tests. 4 subsequent full-suite runs and repeated isolated runs of `AgendaBuddy.AppHost.Tests` alone were all clean (478/478). Smells like resource contention across ~13 concurrently-run test assemblies, not a logic bug — matches this feature's own claimed count on the clean runs. Worth a follow-up bead to investigate before it becomes a source of false-red PRs; not blocking.

---

## Advisory

- **A1 (Neo):** ✅ FIXED (commit `7cefae1`) — stale comment at `dotnet.yml:382-384` said gitleaks-action was "not yet pinned," directly above a line that already was (leftover from T04's original commit, never updated when T09 landed). Corrected in the same commit as the C1 fix.
- **A2 (Neo):** Tech debt — I1 and the Gateway path-filter omission are worth a tracked bead; neither is Critical given no active external contributors (ADR-043's own rationale), but both are real gaps a future accidental paste could exploit silently.
- **A3 (Neo, YAGNI):** `shrink:` — `DockerAndComposeHygieneTest.cs`, `PinnedThirdPartyActionsTest.cs`, and `PublishContainerTest.cs` each carry a near-identical, copy-pasted `RepoRoot()` walk-up-to-`agenda-buddy.sln` helper. One shared internal test helper replaces all three.
- **A4 (Echo):** AC3's generalized guard only recognizes literal `/dotnet/sdk:`/`/dotnet/aspnet:`/`/dotnet/runtime:` substrings with a `:MAJOR.` tag — a digest-pinned or ARG-parameterized base image would silently skip, not fail. Already proved itself once (caught `Profession/Dockerfile`'s real, previously-undocumented mismatch), so not narrowed-to-uselessness, just not exhaustive.
- **A5 (Echo):** `PublishContainerTest` is a structural proxy (asserts the causal metadata is absent) rather than a literal re-run of `dotnet publish -t:PublishContainer`, by design per ADR-031. Confirmed live that the real behavior (successful publish) holds for the actual bug this feature fixes.
- **A6 (Echo):** AC10/AC12 deferral is correctly anticipated by the PRD's own Testing Approach section and STATE.md's Guardrail Log (T08 override) — not a silent gap, though see I2 for why AC10 specifically still needed a stronger note.
- **A7 (Echo):** `CLAUDE.md`'s "867 tests total / 468 backend" is stale — actual is 478 backend. Folds into I3.
- **A8 (Jarvis):** Merge-commit subjects (8 of them, e.g. "Merge F-017-T07: ...") don't follow `<type>(<scope>):` format. CONSTITUTION §6 mandates merge commits but doesn't explicitly carve out their subject format — a calibration call, not a defect.
- **A9 (Phantom):** T-003/T-005/T-006 accept-rationales confirmed unaffected by the diff (no package allowlist added, no automerge config, no branch-protection change). T-004's cited control (`timeout-minutes: 10`) confirmed actually present in the merged job. No secrets, credentials, or unsafely-interpolated CI-context values found; both new third-party Actions are from their genuine upstream orgs.
- **A10 (Jarvis):** New scripts and test classes are well-documented (header comments explaining purpose/wiring; class-level `<summary>` + inline AC references). Test-method-level XML docs reasonably out of scope per CONSTITUTION §5's "public service methods" framing, consistent with the pre-existing `AppHostWiringTest` pattern.

---

## Confirmed (not findings — verified claims from the build)

- **T-001 mitigation** (Phantom, Neo, Echo, independently): both `gitleaks/gitleaks-action` and `aquasecurity/trivy-action` pinned to genuine 40-hex-char commit SHAs; `PinnedThirdPartyActionsTest` genuinely regexes every `uses:` line, not just checks file presence; passes live (87/87 in `AgendaBuddy.AppHost.Tests`).
- **T-002 mitigation** (Phantom, Neo, Echo, independently): `scripts/verify-gitleaks-canary.sh` runs real `gitleaks detect` against a synthetic Atlas-shaped fixture with the exact flags `gitleaks-action` uses, and passes live — file:line present, literal secret absent from both console output and the SARIF report. The historical "wrong capture group" redaction bug is confirmed fixed (`secretGroup = 2` in `.gitleaks.toml`) by the live pass, not just by reading the config.
- **Profession/Dockerfile fix** (Neo, Echo): base image bumped `runtime:8.0` → `aspnet:10.0`; confirmed via live `dotnet publish -t:PublishContainer` (succeeds, zero `NETSDK1152`) and the passing structural test.
- **`aquasecurity/trivy-action@0.28.0` broken-tag fix** (Neo): confirmed the original tag doesn't exist upstream (missing `v`); the pinned SHA resolves to the real `v0.28.0`.
- **Trivy severity-gate logic** (Neo, Echo, independently, against real scans): `app/*.deps.json` target correctly isolates project-introduced findings from base-image/OS findings across a real scan of a built `Profession`/`Booking` image.
- **`api-contracts.md`/`data-model.md` correctly say "no changes"** (Jarvis) — confirmed against the full diff.
- **`ARCHITECTURE.md`'s mid-Construction correction reads clearly** (Jarvis) — states the correction up front, explains the empirical finding, strikes through the original wrong text rather than deleting it.

---

## Over-Engineering (YAGNI)

- `shrink:` — see A3 above (duplicate `RepoRoot()` helper across 3 new test files).

Everything else in the diff is tightly scoped to its PRD requirement — no speculative abstraction, no unused config, no new NuGet packages, no reinvented stdlib.

---

## Process note

Two of the four reviewer subagents briefly disrupted the shared working directory while gathering evidence (a transient `git status` anomaly noted by Phantom, and an accidental `git checkout <ref> -- .` by Jarvis that staged a partial revert). Both self-corrected immediately (`git restore --staged --worktree .`), and Neo independently verified the working tree was clean and `HEAD` unchanged after each incident, before and after all four reviews. No review finding above was affected — all were checked against the stable, committed diff.

---

## Draft CHANGELOG Entry (Jarvis)

```markdown
## [Unreleased] — F-017 container-and-cd-hardening

### Added
- CI: `security-scan` job — dependency-vulnerability audit (`dotnet list package --vulnerable`
  against the full solution) and gitleaks secret scan (full PR diff history), running independently
  and in parallel with `build-and-test`.
- CI: `verify-gitleaks-canary.sh` — regression proof that the configured gitleaks ruleset detects an
  Atlas-credential-shaped secret and redacts it from both console output and the SARIF report.
- CI: `docker-build-and-scan` job — 7-service matrix building each remaining service via .NET SDK
  container support (`dotnet publish -t:PublishContainer`, no Dockerfile) and scanning the image with
  Trivy; fails on HIGH/CRITICAL findings introduced by project dependencies, warns on findings
  inherited from the base image.
- `.gitleaks.toml` — custom rule detecting MongoDB/Atlas connection strings with embedded credentials
  (closes the exact detection gap behind ISSUE-002).
- `.github/dependabot.yml` — weekly NuGet and GitHub Actions dependency update PRs.

### Fixed
- `dotnet publish` (and therefore container builds) failed with `NETSDK1152` for every one of the
  seven services, because `EventAndCommands.csproj` copied its own `appsettings.json` into every
  consumer's publish output via `ProjectReference`, colliding with each service's own file. Removed
  the offending `CopyToOutputDirectory` metadata; `Customer.csproj` and `Provider.csproj`'s
  `ErrorOnDuplicatePublishOutputFiles=false` suppressions (a symptom-level workaround for the same
  root cause) were also removed.
- Deleted `Library/Dockerfile`, `Kafka/Dockerfile`, and `EventAndCommands/Dockerfile` — all three
  published a `net10.0` build onto a `dotnet/runtime:8.0` base and could never run; none of the three
  projects has an entry point. Removed the corresponding `events`, `kafka-library`, and
  `common-library` blocks from `docker-compose.yml` and `docker-compose.override.yml`.
- `Profession/Dockerfile` — same runtime/SDK version-mismatch defect, found and fixed incidentally
  while generalizing the regression guard.
- CI — `gitleaks/gitleaks-action` and `aquasecurity/trivy-action` were referenced by a mutable tag;
  pinned both to full 40-character commit SHAs to close a supply-chain substitution risk (T-001).
  Trivy's own tag reference was additionally found broken (`"0.28.0"`, missing the leading `v` — that
  tag does not exist upstream) and corrected to the real `v0.28.0` commit.

### Changed
- Added a generalized structural test (`DockerAndComposeHygieneTest`) asserting no Dockerfile in the
  repository has a final-stage runtime major version that mismatches its build stage's SDK major
  version, so this defect class cannot recur under a different filename.
```
