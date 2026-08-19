# MOM — Wave 6 Kickoff Standup

**Feature:** `secure-public-endpoints` (F-016)
**Date:** 2026-08-18T20:44:00Z
**Called by:** Neo (Architect)
**Participants:** Neo, Bolt, Echo, Phantom — 4 agents
**Spawn mode:** **solo** — standing session instruction not to spawn agents overrides STATE's
`Party Mode: agent-teams`. Same as every prior F-016 meeting.

---

## Wave under discussion

Wave 6 is the production-behaviour wave: **10 tasks**, all the actual authorization change. Ready now:

| Task | Title | Depends on | ACs |
|---|---|---|---|
| `F-016-T08` | `AgendaBuddyExceptionHandler` — central 403, registered unconditionally | T07 ✅ | 13, 14, **23 `[security]` T-004** |
| `F-016-T09` | Fix `AssertOwner`'s null-claim pass | T07 ✅ | **21 `[security]` T-001 HIGH** |
| `F-016-T18` | Query audit → metadata + `Event.actor` | T07 ✅ | 17, **24 `[security]` T-005** |

Still blocked, unblocking behind T08/T09: T11, T12, T13, T14, T15, T16, T17.

---

## Round 1 — findings

### Neo — three counting errors in the approved artifacts

Each was verified against the code, and each would have made a task look incomplete or an AC
unsatisfiable.

**🔍 N-1 — there are NINE query handlers, not ten.**
`docs/pdlc/context/15-cqrs-and-messaging.md:161` states *"10 queries, 10 handlers"* directly above a
table containing **9 rows**. That figure propagated verbatim into PRD AC-17's broadening note,
`ARCHITECTURE.md` §5 (*"all ten query handlers"*), the plan's threat table, and `F-016-T18`'s task body.

Verified: `grep -rln "IRequest<" EventAndCommands/Queries/` → 9 files; `IRequestHandler<` → 9 files.

```
Calendar/CheckCalendarAppointmentsQuery      Customers/GetCustomersQuery
Calendar/CheckCalendarAvailabilityQuery      Professions/GetProfessionByNameQuery
Customers/GetCustomerByEmailQuery            Professions/GetProfessionsQuery
Provider/GetProviderByEmailQuery             Services/GetServicesFromProviderQuery
Provider/GetProvidersQuery
```

Each handler has **two** `JsonSerializer.Serialize` sites (success and fail), so T18's real scope is
**18 call sites across 9 files**, not 20 across 10. The catalog line should be corrected at
`/ship` (context refresh), not silently in passing.

**🔍 N-2 — there are SEVEN hand-written `ForbiddenException` catch sites, not eight.**
PRD AC-14 and `api-contracts.md` §3.1 both say 8. Verified: Booking `:125,:149,:174`, Customer `:154`,
Provider `:203`, Services `:143,:167` — **7**. (A naïve repo-wide grep now returns 8, because
`AuthFailurePathTest.cs:21`'s doc comment mentions the pattern. That is very likely how the 8 arose
in the first place — a grep that caught a comment.) T08's AC-14 attestation must say 7.

**🔍 N-3 — `Event.actor` cannot be "one `[BsonElement]` and one assignment per handler".**
`ARCHITECTURE.md` §5 states that cost. It is not achievable: **no query handler has any access to the
caller.** The read path is

```
Provider/Program.cs endpoint  (has ClaimsPrincipal)
  → EventsHelper.GetAllProvidersEvent(requestCollection, mediator, providerService)
    → IRequestCollection.GetProvidersRequest(mediator, providerService)
      → new GetProvidersQueryHandler(mediator, providerService, eventStore)
          .Handle(new GetProvidersQuery(), ct)
```

`ClaimsPrincipal` is dropped at the endpoint. `GetProvidersQuery` has no properties. And
`IHttpContextAccessor` is **registered nowhere in this solution** (`AddHttpContextAccessor()` is never
called). So the field needs a decision, not an assignment — see Neo's escalation below.

### Bolt (Backend Engineer)

**🔍 B-1 — middleware ordering decides whether T08 works at all, and the safe order is the
counter-intuitive one.**

The existing `UseExceptionHandler(options)` sits *inside* the Development guard at
`Profession/Program.cs:62`, registered **before** `UseAuthentication`/`UseAuthorization` and the
endpoints — so it **wraps** them. Middleware registered earlier is outermost; an exception propagates
outward and is caught by the **innermost** handler first.

Therefore `app.UseExceptionHandler()` (the new, parameterless one that consults
`IExceptionHandler` implementations) must be registered **after** the `if (IsDevelopment())` block, so
it is *inner* to the Development lambda:

- `ForbiddenException` → the new handler catches it first, returns `true`, emits 403. ✅
- anything else → returns `false`, the middleware rethrows, it propagates outward to the Development
  lambda, which behaves exactly as today. ✅ This is what makes the task body's "return false so the
  two coexist" actually true.

Register it **before** the Development block instead and the dev lambda becomes innermost, swallows
`ForbiddenException`, and AC-13 fails **in Development only** — passing in Production and failing
locally, which is the worst possible way round for a developer to discover it.

The `UseExceptionHandler(options)` overload does **not** consult `IExceptionHandler`, which is why the
two can coexist at all.

**🔍 B-2 — wave 6's "largely parallel" is a dependency claim, not a file claim.**
The plan says "10 tasks, largely parallel". True of the dependency graph; false of the filesystem:

| File | Tasks that edit it |
|---|---|
| `Provider/Program.cs` | T08, T11, T12, T14, T15 |
| `Customer/Program.cs` | T08, T12, T15, T16 |
| `Calendar/Program.cs` | T08, T13 |
| `Services/Program.cs` | T08, T12 |
| `Profession/Program.cs` | T08, T17 |
| `Booking/Program.cs` | T08 |

Immaterial while tasks run sequentially, which they are. It would matter immediately if anyone
parallelised this wave across worktrees — recorded so that is a decision rather than a surprise.

### Phantom (Security Reviewer)

**🔍 P-1 — T08 is an egress change and should be read as one.** Today Production returns a *bare,
empty-bodied* 500 for an unhandled `ForbiddenException` — accidentally the most conservative behaviour
available. T08 starts emitting a body where none existed. That is the right trade (a silent 500 is
worse than an honest 403) but it means AC-23/T-004's "no exception type, no message, no stack frame"
is not belt-and-braces; it is the whole safety margin of the change. It must be tested in
`Production`, not just Development, and the harness can now do that via `UseEnvironment`.

**🔍 P-2 — T09 before T11 and T13 is load-bearing, and the reason is worth re-reading.**
`OwnershipGuard.cs:9-11` — `string.Equals(sub, entityEmail, OrdinalIgnoreCase)` with **no null guard**,
so `string.Equals(null, null)` is `true` and the guard **passes**. `AssertOwnerAny` at `:17` checks
`sub is null` explicitly; `AssertOwner` does not. T11's projection selects owner-vs-non-owner with
`AssertOwner`, and the null-claim fall-through lands on the **owner** branch — returning the
unprojected `ProviderEntity` with the full appointment book and subscribed-customer list. Building T11
before T09 ships the bypass. The dependency edge exists; do not optimise it away.

`TokenFactory.CreateTokenWithoutSubject()` (T05) already exists to prove this over real HTTP.

### Echo (QA Engineer)

**🔍 E-1 — AC-13's two options are not equivalent, and the cheaper one is better.** AC-13 says
"Demonstrated by a test-only endpoint **or** by removing one existing `try/catch` and asserting the
status is unchanged." With the harness in place, the second is strictly better: it proves the central
handler works *and* proves AC-14's no-double-handling in the same stroke, with no test-only route
shipped into a production service. Recommend removing exactly one of the seven — `Customer:154`, the
route `AuthFailurePathTest` already covers — so the existing green 403 test becomes the regression
proof.

**🔍 E-2 — T18's `[security]` AC needs a two-part assertion and only one half is obvious.** AC-24
requires the events document to record the caller's `sub` **and** still contain no provider email,
customer email or appointment record. Asserting the absence needs a positive read of the written
document from `service.Database` — the harness supports it — and the assertion should search the raw
`data` string for the seeded email values rather than checking a field is absent, because "no PII"
fails open if the shape changes.

---

## Round 2 — cross-talk

**Neo → Bolt on N-3.** Two viable implementations, and they differ by an order of magnitude:

| Option | Where the actor is set | Files touched | Notes |
|---|---|---|---|
| **A — thread it** | each handler, from a new parameter | ~30: 6 × `EventsHelper`, 6 × `IRequestCollection`, 6 × `RequestCollection`, 9 handlers, 9 query types | What `ARCHITECTURE.md` §5 describes. Widens six public interfaces for an audit field. |
| **C — centralise it** | `EventStore.SaveAsync`, from `IHttpContextAccessor` | ~8: `Event`, `EventStore`, `AddEventStore`, + `AddHttpContextAccessor()` in 6 services | Audit attribution is a property of *writing an audit record*, not of each handler. Also attributes the 11 **command** handlers for free — same field, no extra scope. |

Bolt prefers **C** and Phantom concurs: fewer places to forget, and it cannot be half-done. Echo notes
C is also more testable — one seam instead of nine.

**Neo's position:** C is the better architecture and I would normally take it under my own authority.
But it deviates from an approved design artifact I own, and it introduces `IHttpContextAccessor` as a
new cross-cutting registration in six services. **Escalated to the maintainer** rather than decided
in-party.

---

## Wave Execution Plan

### Recommended ordering

1. **`F-016-T08`** — gates T12, T13, T14, T16, T17. Nothing else in wave 6 should land first.
2. **`F-016-T09`** — must precede T11 and T13 (P-2).
3. **`F-016-T18`** — independent of both; ordered last of the three pending the N-3 decision.

### Dependency updates applied

**None.** The graph already encodes T08 → {T12,T13,T14,T16,T17} and T09 → {T11,T13}. Verified against
`tasks.cjs dep tree`.

---

## Carried into the tasks

| ID | Finding | Owner |
|---|---|---|
| N-1 | 9 query handlers / 18 call sites, not 10 / 20 | T18, T19, `/ship` context refresh |
| N-2 | 7 hand-written catch sites, not 8 | T08, T19 |
| N-3 | `Event.actor` needs a threading decision — escalated | T18 |
| B-1 | Register `UseExceptionHandler()` **after** the Development block or AC-13 fails in Development only | T08 |
| B-2 | Wave 6 is dependency-parallel, not file-parallel | any future parallelisation |
| P-1 | T08 is an egress change; test the 403 body under `Production` | T08 |
| P-2 | T09 before T11/T13 — the null-claim fall-through lands on the owner branch | T09, T11, T13 |
| E-1 | Satisfy AC-13 by removing `Customer:154`'s catch, not by shipping a test-only route | T08 |
| E-2 | Assert AC-24's "no PII" by searching the raw `data` for seeded emails, not by field absence | T18 |
