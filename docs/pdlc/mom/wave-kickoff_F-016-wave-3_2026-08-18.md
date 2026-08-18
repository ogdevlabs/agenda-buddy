# MOM — Wave 3 Kickoff Standup

**Feature:** `secure-public-endpoints` (F-016)
**Date:** 2026-08-18T19:56:00Z
**Called by:** Neo (Architect)
**Participants:** Neo, Bolt, Pulse, Echo — 4 agents
**Spawn mode:** **solo**

> ⚠️ **Spawn mode note.** STATE.md records `Party Mode: agent-teams`, but this session carries a
> standing instruction not to call the Agent tool unless the user requests it. That instruction
> overrides STATE, so this standup ran **solo** — one model roleplaying all four agents. Same as
> every prior F-016 meeting. Fidelity is lower than agent-teams; recorded so the reader can weigh
> the findings accordingly.

---

## Wave under discussion

| Task | Title | Labels | Depends on | AC |
|---|---|---|---|---|
| `F-016-T03` | `CryptoSessionFixture` — session RSA keypair, in memory, never on disk | `backend`, `security` | T02 ✅ | AC-3 |
| `F-016-T04` | `DockerPreflight` — actionable diagnostics when the runtime is unavailable | `backend`, `devops` | T02 ✅ | AC-7 |
| `F-016-T10` | `GetPagedAsync` on `IRepository<T>` + both implementers | `backend` | T01 ✅ | AC-15 *(mechanism)* |

T03 and T04 both feed **T06** (`ServiceHostFixture`), the wave-4 bottleneck carrying the CRITICAL
security AC (AC-20 / T-002). T10 is deliberately off the harness critical path.

Dependency graph confirmed unchanged from the plan (`tasks.cjs dep tree`):
`T03 <- T02`, `T04 <- T02`, `T10 <- T01`.

---

## Round 1 — findings

### Bolt (Backend Engineer)

**File collisions: none.** The three tasks are disjoint at file level.

| Task | Writes |
|---|---|
| T03 | `AgendaBuddy.IntegrationTests/Harness/CryptoSessionFixture.cs` (new) |
| T04 | `AgendaBuddy.IntegrationTests/Harness/DockerPreflight.cs` (new) + retrofit of `ContainerRuntimeGuardTest.cs` |
| T10 | `Library/Repositories/IRepository.cs`, `Library/Repositories/MongoDbRepository.cs`, `Identity.Tests/Helpers/InMemoryRepository.cs`, `Library.Tests/Repositories/MongoDbRepositoryTest.cs` |

**No new package references needed.** `System.Security.Cryptography` is BCL; `Testcontainers.MongoDb`
4.6.0 is already on `AgendaBuddy.IntegrationTests.csproj:30`.

**🔍 Finding B-1 — the precedent T03 is told to follow cannot be reused.**
`F-016-T03`'s body says to follow `Identity.Tests/Auth/TestCollectionDefinition.cs`, which solves the
`JWT_*` env-var race with `[CollectionDefinition("Sequential", DisableParallelization = true)]`.
That is the right *pattern*, but **xUnit collection definitions are per-assembly** — the attribute in
`Identity.Tests` has no effect on `AgendaBuddy.IntegrationTests`. The harness needs its own
definition. The race is real: `Library.ServerAuth/AuthenticationExtensions.cs:12` reads
`JWT_PUBLIC_KEY` from the process environment at startup, and six services echo that in their
`Program.cs` comments (`Booking:43`, `Calendar:45`, `Provider:47`, `Profession:45`, `Identity:39`).

**Ownership assigned to T03**, because the keypair is what gets shared across classes via
`ICollectionFixture`, and T06 will consume both the fixture and the collection.

### Pulse (DevOps)

**🔍 Finding P-1 — an existing call site already exhibits the failure T04 exists to prevent.**
`AgendaBuddy.IntegrationTests/Harness/ContainerRuntimeGuardTest.cs:35-37` calls
`new MongoDbBuilder().WithImage("mongo:7.0").Build()` then `StartAsync()` with no preflight. With the
runtime unavailable that test *is* the opaque hang. AC-7 says "**the harness** fails with a message
that names the runtime problem and the remedy" — the guard test is part of the harness, so T04 must
retrofit that call site rather than only authoring a helper for T06 to adopt in wave 4. Two-line
change; in scope, not scope creep.

**🔍 Finding P-2 — T04's testability seam is the substance of the task.**
Docker cannot be uninstalled inside a test, so a `DockerPreflight` written as one method that shells
out and throws makes AC-7 **asserted, not tested** — precisely the shape that passes review and
proves nothing. `DockerPreflight` must separate *probe* from *diagnose*, so a unit test can inject
"runtime unavailable" and assert the resulting message names both the problem and the remedy
(under Rancher Desktop, `docker` lives at `~/.rd/bin` and is off `PATH`; Testcontainers shells out
to it). This is the one design decision in T04 that matters.

### Echo (QA Engineer)

**🔍 Finding E-1 — `MongoDbRepository<T>` has no test coverage at all, and cannot cheaply get any.**
`Library.Tests/Repositories/MongoDbRepositoryTest.cs` is an **empty placeholder**: a single
`[Fact] public void METHOD() { }` with an empty body. Both `MongoDbRepository<T>` constructors take
`MongoClient` / `IMongoDatabase` (`MongoDbRepository.cs:9,15`), and mocking the driver's
`Find(...)` → `IFindFluent<T,T>` → `IAsyncCursor<T>` chain is exactly the speculative abstraction the
`yagni` ladder at level `full` tells us not to build for two paginated endpoints.

**Agreed coverage split for T10 — recorded so T19's attestation does not overclaim:**

| Behaviour | Verified by | When |
|---|---|---|
| `IRepository<T>` exposes `GetPagedAsync(int, int)` returning `Task<(IEnumerable<TEntity>, long)>` | reflection test, `Library.Tests` | T10 |
| skip / take / `totalCount`, skip-past-end → `[]` with full count | `InMemoryCredentialRepository`, `Identity.Tests` | T10 |
| **Mongo** `Skip`/`Limit`/`CountDocumentsAsync` semantics | paginated endpoint tests on the real harness | **T15** |

**🔍 Finding E-2 — do not delete the `METHOD()` stub.** AC-19 forbids deleting or skipping a
pre-existing test, and the backend count is currently **309**. Add T10's tests *into* the existing
`MongoDbRepositoryTest` class; the empty stub stays. (It is worthless as a test, but removing it is
F-017/F-019 hygiene, not this task's business.)

---

## Round 2 — cross-talk

Not required. No two agents named the same resource, and no finding contradicted another.
B-1, P-1, P-2, E-1 and E-2 are all within their own task's boundary.

---

## Wave Execution Plan

### Confirmed safe parallel

All three. Disjoint files, disjoint projects, no shared state. T03 and T04 both write into
`Harness/` but to different new files.

### Flagged sequential pairs

**None.** T04 does not need T03's keypair; T03 does not need T04's probe. T10 shares nothing with
either.

### Recommended ordering

Critical path first, since both T03 and T04 gate T06:

1. **`F-016-T03`** — `CryptoSessionFixture` + the harness's own `Sequential` collection (finding B-1)
2. **`F-016-T04`** — `DockerPreflight`, probe/diagnose split (P-2), retrofit the guard test (P-1)
3. **`F-016-T10`** — `GetPagedAsync`, off the harness path entirely

### Dependency updates applied

**None.** The plan's wave-3 parallelism claim holds — unlike F-018's wave-1 standup, which found
three missing ordering edges. Verified against `tasks.cjs dep tree`.

---

## Carried into the tasks

| ID | Finding | Owner |
|---|---|---|
| B-1 | xUnit collection definitions are per-assembly; the harness needs its own `Sequential` collection | T03 |
| P-1 | `ContainerRuntimeGuardTest.cs:35-37` starts a container unguarded — retrofit it | T04 |
| P-2 | Split probe from diagnose or AC-7 is asserted rather than tested | T04 |
| E-1 | `MongoDbRepository<T>` is untestable without Mongo; Mongo paging semantics land on T15 | T10, T15, T19 |
| E-2 | Keep the empty `METHOD()` stub — AC-19 | T10 |
