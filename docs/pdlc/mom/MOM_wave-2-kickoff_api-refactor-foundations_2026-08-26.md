# MOM — Wave 2 Kickoff Standup, F-018 api-refactor-foundations

**Date:** 2026-08-26 · **Called by:** Neo (Architect) · **Participants:** Neo, Bolt, Pulse, Echo

## Round 1 — Coordination Analysis

**Neo:** Two tasks (T03, T17) both edit `.github/workflows/dotnet.yml`, but in disjoint regions — T03 adds a `dotnet format --verify-no-changes` step inside the existing `build-and-test` job (~line 194); T17 adds a **new job** (CI spec-drift check), which per this project's established convention must also be wired into the `summary` job's `needs:` list (line 723) so branch protection still has one job to point at. Same shape as F-017 Wave 2's two devops tasks sharing one file — auto-merged clean there. Low collision risk, not blocking, but the merge order matters less than making sure whichever task lands second doesn't silently drop the other's edit to `needs:` — worth a careful diff at merge time, not a sequencing requirement.

**Bolt:** T13 (Tier 3 audit assertions) is self-contained — new files under `AgendaBuddy.IntegrationTests/Audit/`, reads `EventStore`'s collection directly via `MongoDB.Driver` per ARCHITECTURE D4, no shared fixture beyond what F-016's harness already provides. Riskier than it looks: the permanent guard test (AC-15) requires a manual mutation red/green recorded once in the verification doc, then a test that stays green under normal code and would catch a future regression — easy to build a guard that's vacuously green. Confirm the mutation genuinely turns it red before calling it done.

**Pulse:** T19 (headline count) explicitly reads "current" test totals — same staleness trap Wave 1a's standup caught. T13 adds new tests this same wave, so T19 running before T13 lands would report a stale count again. **Added `T19 → T13` dependency** (T19 already depended on T10/T11/T12/T15/T16 from Wave 1a's fix). T17's own task text still says specs are "NOT committed... drift baseline is the previous run's artifact or a hash manifest" — stale as of this session: T16 committed them under ADR-048. **Amended T17's task file** to say the baseline is now simply the committed spec body — simpler than what the task originally called for.

**Echo:** No shared test fixtures at risk — T13's `Audit/` tests are read-only against `EventStore`'s Mongo collection, independent of T03/T17's CI-only changes. No ambiguous ACs. T13's guard test is the one item Echo flags for extra scrutiny at Review: a mutation-tested guard that isn't actually mutation-tested is a false sense of coverage, exactly the class of gap this program's episode 001 exists to close.

## Round 2 — Cross-talk

Not needed — no conflicting recommendations; Neo's T03/T17 file-sharing note and Pulse's T19/T13 dependency were independent findings, not disagreements.

## Wave Execution Plan

1. **Confirmed safe parallel:** T03, T13, T17 — three independent worktrees, no real file collision (T03/T17 share a file but disjoint regions).
2. **Sequenced after:** T19, once T03/T13/T17 (specifically T13, per the dependency below) land.
3. **Recommended order:** T03, T13, T17 in parallel (Wave 2a) → T19 alone (Wave 2b).
4. **Dependency updates applied:** `tasks.cjs dep add F-018-T19 F-018-T13` (done — T19 now depends on T10/T11/T12/T13/T15/T16).
5. **Task amendment applied:** F-018-T17 amended to reflect ADR-048's cleared commit-deferral (drift baseline = committed spec body, not a hash manifest).

**Watch at merge:** T17's edit to the `summary` job's `needs:` list — confirm it survives whichever merge lands second.
