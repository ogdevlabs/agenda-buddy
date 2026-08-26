# MOM — Wave 1 Kickoff Standup: F-018 `api-refactor-foundations`

**Date:** 2026-08-26 | **Called by:** Neo (Architect) | **Participants:** Neo, Bolt, Pulse, Echo (4)
**Purpose:** Coordinate the 8-task ready queue (post-resume, post-amendment) before claiming the first task.

---

## Context

This is the first real wave since F-018 resumed from its 2026-08-18 pause. The 8 F-016-absorbed tasks (T01, T05–T09, T14, T18) are already `done`. The ready queue is T02, T04, T10, T11, T12, T15, T16, T19 — all building against the harness F-016 already delivered (`ServiceHostFixture`, `CryptoSessionFixture`, `TokenFactory`, `MongoEndpointGuard`).

## Round 1 findings

**Neo:** All 8 tasks are either docs-only (T02: `CONSTITUTION.md`; T04: external tracker), or land in genuinely new subdirectories of `AgendaBuddy.IntegrationTests/` — `Contract/` (T11), `Persistence/` (T12), `OpenApi/` (T16), and likely a new file under `Support/` or `Harness/` (T10). None of those directories exist yet outside `Harness/`, so there's no pre-existing file for two tasks to collide on. T16 explicitly self-describes as decoupled (no container needed — resolves `ISwaggerProvider` from a booted-but-unreachable-Mongo host). No architectural risk beyond what's already in the task bodies.

**Bolt (backend — T10, T11, T12, T16):** Confirmed no shared-file risk: T10 (KafkaClientFake) is a new fake class + `ConfigureTestServices` swap, T11 writes one contract test file per service in `Contract/`, T12 writes one persistence test file per service in `Persistence/`, T16 writes OpenAPI generation logic in `OpenApi/`. All four consume the existing `HarnessCollection`/`ServiceHostFixture` but don't need to modify them. Genuinely parallel-safe.

**Pulse (devops — T04, T15, T19):** T04 (file a beads issue) and T15 (manual container-kill verification) touch nothing in the repo tree that any other task touches. **T19 is the one real finding**: it reports the project's headline test count and updates `CLAUDE.md`/`OVERVIEW.md` — but T10/T11/T12/T15/T16 all *add new tests this same wave*. If T19 runs before those land, the count it writes down is stale the moment the wave finishes. The dependency graph doesn't express this because T19 was authored before this wave existed.

**Echo (wave has 8 tasks ≥3):** No shared test-fixture conflicts — everything consumes F-016's existing fixtures read-only. No ambiguous ACs. Echo's one flag is the same one Pulse raised: T19's "current" count is only meaningful after the test-adding tasks close.

## Round 2 cross-talk

Pulse and Echo's findings were the same conflict, not two — no cross-talk round needed; consensus was immediate.

## Wave Execution Plan

**Confirmed safe parallel (Wave 1a, 7 tasks):** T02, T04, T10, T11, T12, T15, T16 — no shared files, no hidden coupling beyond what the graph already shows.

**Flagged sequential:** T19 must follow T10, T11, T12, T15, T16 (the count it reports is only accurate once they've landed). T02/T04 don't add tests, so T19 doesn't need to wait on those.

**Recommended order:** Wave 1a (parallel): T02, T04, T10, T11, T12, T15, T16. Wave 1b (after 1a's test-adders close): T19.

**Dependency updates applied:**
```
node scripts/tasks.cjs dep add F-018-T19 F-018-T10
node scripts/tasks.cjs dep add F-018-T19 F-018-T11
node scripts/tasks.cjs dep add F-018-T19 F-018-T12
node scripts/tasks.cjs dep add F-018-T19 F-018-T15
node scripts/tasks.cjs dep add F-018-T19 F-018-T16
```
`tasks.cjs ready` now returns 7 tasks (T19 correctly dropped from the queue until those five close).

## Conclusion

7 tasks confirmed parallel, 1 task (T19) resequenced. Proceeding to task selection.
