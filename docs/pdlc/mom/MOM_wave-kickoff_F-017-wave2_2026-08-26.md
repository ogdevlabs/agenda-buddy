# Wave Kickoff Standup — F-017 Wave 2

**Called by:** Neo (Architect)
**Mode:** Solo (Neo directly inspected the file regions both tasks touch before deciding — a full 4-agent panel is disproportionate for a 2-task wave with one obvious, already-verified risk)
**Purpose:** Coordination check before claiming F-017-T04 and F-017-T06
**Date:** 2026-08-26

---

## Wave 2 tasks

- **F-017-T04** (depends on T03, done) — add a gitleaks step to the existing `security-scan` job.
- **F-017-T06** (depends on T02, done) — add a new `docker-build-and-scan` job (7-service SDK-container matrix), plus a new `docker` path filter in the `changes` job and a `summary` job update.

## File-overlap check

Both tasks edit `.github/workflows/dotnet.yml` — the only file either touches. Inspected the current file directly (619 lines, job list: `changes:17`, `build-and-test:165`, `security-scan:310`, `integration:381`, `build-android:481`, `build-ios:504`, `build-mobile-tests:527`, `summary:555`):

- **T04** edits inside the `security-scan` job's `steps:` list only (adds one step after the existing dependency-audit step, ~line 357).
- **T06** edits three regions, none overlapping T04's: the `changes` job (new `docker` filter, ~line 17-165), a brand-new job block (inserted after an existing job, before `summary`), and the `summary` job's `needs:`/reporting env vars (~line 555+, same pattern `security-scan` already used when T03 added it).

No overlapping lines. Same conclusion as Wave 1's T03/T08 (different files) and T01/T02 (different files) — here it's the same file but disjoint regions, which git's line-based merge handles cleanly in the common case.

## Wave Execution Plan

1. **Confirmed safe parallel tasks:** T04, T06 — build in separate isolated worktrees.
2. **Flagged sequential pairs:** none.
3. **Scope notes carried into the task prompts:**
   - T06 must verify its new `docker` filter is actually consumed by an `if:` condition (AC 10) — this pipeline already has one precedent dead-filter bug (`library`, still unconsumed as of this wave) that must not repeat.
   - T06's matrix needs `timeout-minutes: 10` per entry (Requirement 10) — do not add a timeout to any of the 5 pre-existing jobs (out of scope).
4. **Dependency updates:** none needed.

**Recommended order:** parallel, worktree-isolated, merge back with the same conflict-resolution discipline as Wave 1 (expect a clean auto-merge; resolve by hand if not).
