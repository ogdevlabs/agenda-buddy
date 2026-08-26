---
feature: api-refactor-pilot-booking
feature-id: F-019
date: 2026-08-26
status: approved
---

# PRD: API Refactor Pilot — Booking (F-019)

## Overview

Stage 2/3 of the API refactor program. Applies the full target architecture — Clean Architecture layering,
MediatR as the single dispatcher, `FluentResults`, `Validot`, `Mapster`, `GuardClauses`, and `SmallApiToolkit`'s
`DataResponse<T>` envelope — to **all 10** of Booking's current routes, proving the shape end-to-end before
it's replicated across the remaining six services (F-020). Booking is the pilot because it's the only
service exercising Kafka, the EventStore audit trail, and `RequestCollection` removal together.

## Problem Statement

Booking's 3 oldest routes (Book/Update/Cancel appointment) carry the program's original defects: success is
decided by string-sniffing (`StartsWith("exception")`), handlers are hand-constructed
(`new BookingAppointmentCommandHandler(...)`) instead of dispatched through the already-injected `IMediator`,
and `Booking/Requests/RequestCollection.cs` has a dormant `(kafkaClient as KafkaClient)` downcast that
silently null-references the moment `IKafkaClient` is ever substituted (the same class of bug F-018 fixed
for Provider, filed as `agenda-buddy-5og` for Booking, still open).

Booking's 7 newer routes (status, notes, payment — added by F-014) are structurally closer to the target:
typed `Results<>` returns, no string-sniffing, no hand-constructed handlers. But they still bypass MediatR
entirely, use hand-rolled validation instead of a shared validator, and have no Clean Architecture layering.
Both groups need the same destination shape, just from different starting points.

## Target User

This repo's developers (one maintainer + AI agents). No provider- or customer-facing behavior change is
intended — this is an internal refactor. `DataResponse<T>`'s envelope is a wire-format change for any HTTP
client, but re-wiring `MobileApp` to it is explicitly out of scope (F-020's concern, since `MobileApp` talks
to all 7 services and shouldn't be touched twice).

## Requirements

1. `Booking.Api`, `Booking.Core`, `Booking.Domain`, `Booking.Infrastructure` exist as the four Clean
   Architecture projects. The existing `Booking` project becomes `Booking.Api` — thin: endpoint definitions,
   DI wiring, no business logic. 🧪 test-first
2. All 10 of Booking's current routes dispatch via `mediator.Send(command)` — zero hand-constructed
   `new SomeCommandHandler(...)` calls anywhere in `Booking.Api`. 🧪 test-first
3. `Booking/Requests/RequestCollection.cs` and `IRequestCollection` are deleted. Their 3 routes' logic moves
   into `Booking.Core` handlers dispatched through MediatR. 🧪 test-first
4. The dormant `IKafkaClient` downcast (`agenda-buddy-5og`) disappears as a consequence of requirement 3 —
   no separate fix, no `as KafkaClient` cast anywhere in the new handlers. 🧪 test-first
5. Zero string-sniffed control flow (`StartsWith("exception")` or equivalent) anywhere in Booking's request
   path. `FluentResults` carries success/failure instead. 🧪 test-first
6. `Validot` validates every one of Booking's 10 request DTOs. Zero `MiniValidator.TryValidate` calls remain
   in `Booking.Api`. 🧪 test-first
7. Request and response DTOs (via `Mapster`) exist for all 10 routes. `AppointmentEntity` (or any other
   Library entity) never appears directly in a route signature. 🧪 test-first
8. `GuardClauses` is used for defensive null/argument checks in `Booking.Core` handlers, replacing manual
   `if (x is null) throw` patterns. 🧪 test-first
9. The real ASP.NET Core `CancellationToken` is threaded from each route through to its handler and any
   downstream `await` — zero `new CancellationToken()` instances. 🧪 test-first
10. **`DataResponse<T>` becomes the response envelope for all 10 routes — authored in-repo, not from a
    package.** A pre-Design spike (2026-08-26) found `SmallApiToolkit` v10.0.0 does not ship `DataResponse<T>`
    at all (confirmed by reflection over the restored assembly: only `ExceptionMiddleware`, `LoggingMiddleware`,
    `IHttpRequestHandler<T,T>`, and a handful of extension classes). It was the *reference repo's own type*,
    not a package type. `SmallApiToolkit` is dropped from this feature's dependencies entirely (see
    Requirement 11) — `DataResponse<T>` is a small record type living in `Booking.Domain`. 🧪 test-first
11. **`SmallApiToolkit` is not adopted at all.** Its `IHttpRequestHandler<T,T>` is already rejected (ADR-014,
    MediatR is the sole dispatcher). Its `ExceptionMiddleware` would duplicate `Library.ServerAuth/AgendaBuddyExceptionHandler.cs`
    (F-016), which already centrally maps `ForbiddenException` → 403 for every service — repeating ADR-014's
    exact mistake (two competing dispatchers) one layer up (two competing exception handlers). Its
    `DataResponse<T>` doesn't exist in the package (Requirement 10). With all three reasons for depending on
    it gone, the dependency itself is dropped — amends ADR-015 from five packages to four
    (FluentResults, Validot, Mapster, GuardClauses). Booking's new handlers rely on the existing central
    exception handler; local `catch (ForbiddenException)` blocks may be kept for defense-in-depth per F-016's
    own design, unchanged.
12. The EventStore audit trail is unchanged in substance — `AgendaBuddy.IntegrationTests/Audit/BookingAuditTest.cs`
    passes without modification to its assertions (only mechanical changes, if any, to how the request is
    issued). 🧪 test-first
13. `AgendaBuddy.IntegrationTests/Contract/BookingRouteContractTest.cs` and `Persistence/BookingPersistenceTest.cs`
    pass without modification to their assertions — proving the rewrite preserves status codes and persisted
    state, which is what F-018's tests were deliberately built to check regardless of envelope changes.
    🧪 test-first
14. CONSTITUTION §3's invariants survive: EventStore audit, cache-aside (N/A to Booking — it has no cached
    reads today, confirmed by grep; not introduced here either).

## Assumptions

- F-018's Tier 1/2/3 tests for Booking are the actual regression net for this rewrite, per the program-level
  brainstorm's Conflict B resolution (assert status + persisted state, not envelope). Confirmed by reading
  them: none assert response body shape beyond what's needed for the test's own setup (e.g. reading an
  appointment identifier back out of a 201 body to use in a follow-up call).
- `EventStoreWriteGuardTest`'s convention-based guard (scans for the literal `eventStore.SaveAsync(` string
  in each handler file) will need its `HandlerFileNames` list updated once `BookingAppointmentCommandHandler.cs`
  etc. move from `EventAndCommands/Commands/Booking/` into `Booking.Core` — otherwise the guard silently stops
  covering Booking once the files move. Verify at Design.
- The 7 newer routes' existing typed-`Results<>` shape is compatible with wrapping in `DataResponse<T>` —
  not yet verified against `SmallApiToolkit`'s actual API; a Design-time spike should confirm before Plan
  sizes the work, matching F-018's own precedent of spiking risky mechanisms before committing to a plan.

## Acceptance Criteria

1. `Booking.Api`/`Booking.Core`/`Booking.Domain`/`Booking.Infrastructure` exist; `Booking.Api` contains no
   business logic (grep-provable: no direct `IMongoClient`/`IRepository<T>` usage outside DI registration).
2. `git grep "new.*CommandHandler(" Booking.Api Booking.Core` returns zero hand-constructed handlers.
3. `Booking/Requests/RequestCollection.cs` and `IRequestCollection.cs` no longer exist in the tree.
4. `git grep "as KafkaClient"` returns zero matches anywhere under `Booking.Api`/`Booking.Core`.
5. `git grep 'StartsWith("exception"'` (case-insensitive) returns zero matches under Booking's new projects.
6. `git grep "MiniValidator"` returns zero matches under `Booking.Api`/`Booking.Core`.
7. `git grep "new CancellationToken()"` returns zero matches under Booking's new projects.
8. Every one of Booking's 10 routes returns a `DataResponse<T>`-shaped envelope, verified by a real HTTP
   request per route.
9. `AgendaBuddy.IntegrationTests/Audit/BookingAuditTest.cs` passes with zero changes to its assertions.
10. `AgendaBuddy.IntegrationTests/Contract/BookingRouteContractTest.cs` passes with zero changes to its
    status-code assertions.
11. `AgendaBuddy.IntegrationTests/Persistence/BookingPersistenceTest.cs` passes with zero changes to its
    persisted-state assertions.
12. `EventStoreWriteGuardTest`'s handler-file enumeration still covers every Booking command/query handler
    after the move to `Booking.Core` (updated, not silently dropped).
13. All 484 backend + (234 + Booking's integration count) tests pass; zero regressions in any other service.
14. No `Booking`-owned test file is deleted; test bodies may be updated only where the DTO/envelope shape
    itself changed (not where behavior changed).

## User Stories

**US-01 (Requirements 1-4):** As a developer, I want Booking's endpoint layer split into Clean Architecture
projects with MediatR dispatch, so that adding or changing a Booking route no longer means hand-constructing
a handler or risking the Kafka-substitution NRE bug.

**US-02 (Requirements 5-9):** As a developer, I want string-sniffed control flow, hand-rolled validation, and
fake cancellation tokens gone from Booking, so that a client disconnect actually cancels work and a failure
can never be confused with a message that happens to start with the wrong word.

**US-03 (Requirements 10-11):** As a developer, I want one consistent response envelope and exactly one
exception-handling mechanism (not two competing ones), so that every Booking route looks the same to a
client and to the next engineer reading the code.

**US-04 (Requirements 12-14):** As a developer, I want F-018's existing Booking tests to keep passing
unmodified through this rewrite, so that the harness proves it's a real regression net, not just a
demonstration that ran once.

## Testing Approach: Test-Driven Development (TDD)

Every requirement above is 🧪 test-first except requirement 14 (an invariant restated from CONSTITUTION §3,
verified by the existing test suite rather than a new test). The `git grep`-provable ACs (2, 3, 4, 5, 6, 7)
are structural regression guards, matching F-018's own precedent (`PinnedThirdPartyActionsTest`,
`DockerAndComposeHygieneTest`) — cheap, permanent, and immune to drift once written.

## Non-Functional Requirements

- **No behavior change** for provider/customer-facing responses beyond the envelope wrapper itself (status
  codes, persisted data, and audit events are unchanged — enforced by ACs 9-11).
- **CI budget:** the integration job's 10-minute tripwire (ADR-017, F-018-T15/T17) is the objective signal if
  this feature's added test count pushes past it — size Plan's task list against it, don't assume.

## Out of Scope

- The other 6 services (Calendar, Customer, Provider, Services, Profession, Identity) — F-020.
- Re-wiring `MobileApp` to `DataResponse<T>`'s envelope — F-020 or a dedicated follow-up.
- `services.BuildServiceProvider()` ASP0000 fix — doesn't exist in this codebase (verified by grep); dropped
  from scope, ROADMAP's inherited-scope note was stale.
- Any change to Calendar, even though `BookCalendarCommandHandler` (in `EventAndCommands/Commands/Calendar/`)
  throws `NotImplementedException` and has no route — that's Calendar's own defect, not Booking's.

## Known Risks

- ~~**`SmallApiToolkit`'s actual `DataResponse<T>` API shape is unverified against this codebase.**~~ —
  **RESOLVED by spike, 2026-08-26.** It doesn't exist in the package at all — see Requirements 10/11.
  `DataResponse<T>` will be authored in-repo; still needs Design to confirm it serializes as expected
  alongside `System.Text.Json`'s existing `ObjectIdJsonConverter`.
- **`EventStoreWriteGuardTest`'s file-scan guard will silently stop covering Booking's handlers** the moment
  they move to `Booking.Core`, unless its `HandlerFileNames` enumeration is updated in the same change. This
  is exactly the kind of drift the guard's own Party Review finding (N1/E1, F-018) already flagged as a
  known limitation — worth getting right here rather than repeating the gap.
- **This is the feature that tests F-018's 10-minute CI budget for real** (ADR-017's explicit tripwire).
  F-018's harness was measured comfortable at ~20 tests; F-019 adds an unknown number of new Core-layer unit
  tests (not integration tests, so likely not counted against the budget) plus whatever integration coverage
  the new DTOs need.

## Standards Alignment

Nordstrom Standards Readiness gate does not apply to this project (ADR-042) — skipped per CONSTITUTION §9,
not attempted.

## Design Docs

- [ARCHITECTURE.md](../design/api-refactor-pilot-booking/ARCHITECTURE.md)
- [data-model.md](../design/api-refactor-pilot-booking/data-model.md) (no changes)
- [api-contracts.md](../design/api-refactor-pilot-booking/api-contracts.md)
- [threat-model.md](../design/api-refactor-pilot-booking/threat-model.md) (Lite triage, 3 threats, 2 mitigate-now)
- [ux-review.md](../design/api-refactor-pilot-booking/ux-review.md) (Skip, no UI surface)

## Related Episodes

_To be linked after Ship._

## Approval

**Approved by:** oscargarcia@ogdevlabs.onmicrosoft.com
**Date:** 2026-08-26
