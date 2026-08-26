# Plan — API Refactor Pilot: Booking (F-019)

**11 tasks, 7 waves.**

| Task | Title | Depends on | Labels |
|---|---|---|---|
| T01 | Spike: `DataResponse<T>` + `ObjectIdJsonConverter` | — | backend |
| T02 | Spike: Validot rule API vs. Booking's DTOs | — | backend |
| T03 | Scaffold Domain/Core/Infrastructure; migrate 3 original routes' commands/handlers | T01 | backend |
| T04 | Rewrite `Booking.Api`: delete `RequestCollection`, dispatch 3 original routes via MediatR | T03 | backend |
| T05 | Author commands/handlers for the 7 F-014 routes | T02, T04 | backend |
| T06 | Rewire `Booking.Api`'s 7 F-014 routes to MediatR + Validot + `DataResponse<T>` | T05 | backend |
| T07 | Update `EventStoreWriteGuardTest`'s handler-file enumeration | T04, T06 | backend |
| T08 | [security] T-101 Validot-strictness regression test | T04, T06 | backend, security |
| T09 | [security] T-102 error-detail-leak test | T04, T06 | backend, security |
| T10 | Delete moved files; verify F-018's Booking tests pass unmodified | T04, T06 | backend |
| T11 | Final verification — all 14 ACs attested | T07, T08, T09, T10 | backend |

## Wave order

1. **T01, T02** (parallel — independent spikes)
2. **T03** (needs T01's confirmed `DataResponse<T>` shape)
3. **T04** (needs T03's new projects to dispatch into)
4. **T05** (needs T02's Validot pattern + T04's established dispatch pattern)
5. **T06** (needs T05's handlers)
6. **T07, T08, T09, T10** (parallel — all just need T04+T06 done, touch disjoint files: the guard test, two new security tests, and a cleanup+verify pass)
7. **T11** (final verification, needs everything)

## Why this order

The two spikes gate everything else — `DataResponse<T>`'s actual serialization behavior and Validot's actual
rule-authoring API are both unverified assumptions inherited from the program-level brainstorm (one of which,
`SmallApiToolkit`, already turned out wrong at the pre-Design spike). Building `Booking.Domain` against an
unconfirmed envelope shape risks the same class of rework.

The original-3-routes migration (T03→T04) comes before the F-014-7-routes migration (T05→T06) because the
original routes are the ones carrying the actual defects (`RequestCollection`, hand-constructed handlers,
the dormant Kafka downcast) — they're the load-bearing proof of the pattern. The 7 newer routes are lower-risk
(already typed, already defect-free) and benefit from T04 having established the concrete dispatch pattern
first.

Wave 6's four tasks are genuinely parallel: T07 (test infrastructure), T08/T09 (new security tests), T10
(deletion + verification) touch four different files/concerns with no shared state.

## Readiness

**Task count:** 11 · **Waves:** 7 · **Domains:** backend, security · **Unresolved MUST requirements:** none.

Not run as a full Readiness Party (condensed Inception, per user request) — self-assessed: **Fair**.
Traceability is strong (every PRD requirement maps to at least one task, every threat-model "mitigate now"
item is a structured `[security]` AC on T08/T09). The one honest gap: no adversarial pass challenged this
plan's wave-parallelism claims the way F-018's own wave-1 standup caught a real ordering bug — Build's Wave
Kickoff Standup (Step 4) is the backstop for that, same as every feature.
