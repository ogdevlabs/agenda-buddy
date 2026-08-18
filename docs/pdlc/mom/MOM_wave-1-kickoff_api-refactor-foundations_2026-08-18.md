# MOM — Wave 1 Kickoff Standup

**Feature:** api-refactor-foundations (F-018)
**Phase / Sub-phase:** Construction / Build
**Date:** 2026-08-18T17:33Z
**Called by:** Neo (Architect)
**Spawn mode:** `solo` — the session carries a standing "do not call the Agent tool unless requested" instruction, which overrides STATE.md's `Party Mode: agent-teams` for this meeting. Recorded rather than silently substituted.
**Participants (roleplayed):** Neo (Architect), Bolt (Backend), Pulse (DevOps), Echo (QA) — 4 agents, per the ≥3-task rule that pulls Echo in.

---

## Wave under discussion

5 tasks, declared "fully parallel" by the plan.

| Task | Title | Labels |
|---|---|---|
| F-018-T01 | Rename `EventAndCommands/Persitency` → `Persistence` | `backend` |
| F-018-T02 | Amend CONSTITUTION §1/§4/§9, verify ADR-014…020, fix the Identity comment | `docs` |
| F-018-T03 | Add `.editorconfig` and enforce `dotnet format` in CI | `devops` |
| F-018-T04 | File the beads issue tracking the 10-green-run count | `devops` |
| F-018-T19 | Confirm the 3 mobile CI jobs pass; report 379 | `devops` |

Execution is **sequential** this session at the user's explicit request, so the standup's job is ordering, not parallel-safety.

---

## Round 1 — findings

**Bolt (Backend) — `dotnet format` will swallow the rename diff.**
`T03` says outright: "expect to re-apply formatting once when the `.editorconfig` lands." That is a repo-wide write across every tracked `.cs` file. `T01` is an 11-file namespace rename that **AC-16 requires to be its own reviewable commit**. If the mass reformat lands first, or in between, the rename commit stops being the clean, auditable, mechanically-revertible diff AC-16 exists to guarantee. Named resource at risk: the whole `agenda-buddy-backend.slnf` tree.
→ **`T01` must precede `T03`.**

**Neo (Architect) — the Constitution currently forbids `T01`.**
`CONSTITUTION.md:159` (§9) reads: *"The `EventAndCommands/Persitency/` typo is a known issue — do not rename until a dedicated refactor is planned."* `T02` is the task that removes that clause. Committing `T01` while the prohibition still stands makes the build's first code commit read as a §9 violation in the git history, even though the clause's own stated condition ("until a dedicated refactor is planned") is satisfied — the refactor *is* planned, in an approved PRD.

Checked whether this collides with AC-16: it does not. AC-16 pins `T01` to *its own commit, before any integration test is authored* — not to being the first commit of the feature. So the ordering is free, and the coherent order is `T02` → `T01`.
→ **`T02` must precede `T01`.**

**Pulse (DevOps) — `T02` and `T03` edit the same file.**
Both write `CONSTITUTION.md`. `T02` owns §1 (still says .NET 8 / C# 12; the project is `net10.0`), §4 (MiniValidator → Validot per ADR-016) and §9 (the five approved packages, minus the rename prohibition). `T03` needs §2's "Linting & Formatting" block, which currently reads `<!-- not yet configured -->` for both linter and formatter — leaving that stale after adding an `.editorconfig` and a CI gate would be exactly the mandated-but-unimplemented drift `T04` exists to prevent.
→ **Section ownership is disjoint (§1/§4/§9 vs §2), so this is a sequencing concern, not a merge conflict. `T02` before `T03`.**

**Pulse (DevOps) — `T19` cannot complete in this session, and the graph can't say so.**
`T19` has two halves. The local half is doable now: verify the 379 headline count, investigate the seven skipped MobileApp tests, correct `CLAUDE.md` / `OVERVIEW.md`. The other half — "confirm the 3 mobile CI jobs pass on a REAL run" — needs a maintainer-pushed throwaway branch, because the jobs are path-filtered and `main` is PR-protected. This is the plan's known gap #2/#7 (`dependency-missed`): the dependency graph has no way to encode "waits on a human."
→ **Recommend the Step 9e split-and-defer when `T19` is reached**, not a half-open task.

**Echo (QA) — two of these five tasks have nothing a test can pin, and that needs saying out loud, not discovering at the TDD gate.**
- `T01` is behaviour-preserving by construction: the EventStore collection name comes from config (`EventsCollection`), not the namespace. Its verification *is* the existing suite — 379 green before and after. No new test is owed; the regression suite is the test.
- `T02` is comment-and-prose only. `T04` creates an issue in an external tracker. Both are the TDD skill's explicit carve-outs (config-only / infrastructure-only) and both therefore need an **explicit human TDD override** before proceeding — there is no silent exception.
- `T03`'s gate *is* testable and should be treated as red-green: `dotnet format --verify-no-changes` must fail on the tree before the reformat and pass after.
- No shared fixtures are in play in this wave — fixture collision risk starts at `T06`/`T08`.

---

## Round 2 — cross-talk

One conflict needed reconciling: Bolt's "`T01` before `T03`" versus Neo's "`T02` before `T01`". These compose rather than compete — `T02` → `T01` → `T03` satisfies both, and Pulse's §2-vs-§9 file-ownership point independently wants `T02` before `T03`. Consensus in one round.

---

## Wave Execution Plan

**Confirmed safe parallel:** none — the wave is sequential by user instruction, and three real ordering edges were found that the plan's "fully parallel" claim missed.

**Recommended order**

1. `F-018-T02` — Constitution §1/§4/§9 + ADR verification + Identity comment. Lifts the §9 prohibition first.
2. `F-018-T01` — the rename, as its own commit. AC-16.
3. `F-018-T03` — `.editorconfig`, the one-time reformat, §2, CI `--verify-no-changes`.
4. `F-018-T04` — the beads green-run-counter issue.
5. `F-018-T19` — local half now; CI-confirmation half split and deferred behind the maintainer push.

**Dependency updates applied**

```
node scripts/tasks.cjs dep add F-018-T01 F-018-T02   # §9 prohibition lifted before the rename commit
node scripts/tasks.cjs dep add F-018-T03 F-018-T01   # repo-wide format must not absorb the rename diff
node scripts/tasks.cjs dep add F-018-T03 F-018-T02   # both write CONSTITUTION.md (§2 vs §1/§4/§9)
```

No cycle introduced — `tasks.cjs ready` returns `[F-018-T02, F-018-T04, F-018-T19]` after the edits.

**Flagged for the human**

- `T02` and `T04` need an explicit TDD override (docs-only / external-tracker-only). `T01`'s verification is the existing 379-test suite.
- `T19` will be split; the deferred half is gated on a maintainer-pushed throwaway branch.
- The plan called wave 1 "fully parallel." It is not. Three ordering edges are now recorded in the task store so a future resume inherits them.
