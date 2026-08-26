---
feature: api-refactor-pilot-booking
date: 2026-08-26
status: define-complete
last-updated: 2026-08-26T21:40:00Z
approved-by: oscargarcia@ogdevlabs.onmicrosoft.com
approved-date: 2026-08-26
prd: docs/pdlc/prds/PRD_F-019_api-refactor-pilot-booking_2026-08-26.md
---

# Brainstorm Log: API Refactor Pilot — Booking (F-019)

**Program-level context:** [brainstorm_refactor-minimal-apis_2026-08-18.md](brainstorm_refactor-minimal-apis_2026-08-18.md) —
**read that log first.** It already settled, for the whole 3-stage program: the reference implementation
(Gramli/AuthApi), MediatR retained as the single dispatcher (ADR-014), the five approved packages (ADR-015),
Validot replacing MiniValidator (ADR-016), Booking chosen as the pilot (only service exercising Kafka +
EventStore audit + `RequestCollection` removal together), and Identity's `AuthRequests.cs` DTOs as the
in-repo precedent to stay consistent with. **This log does not re-derive any of that** — condensed Inception,
per user request (2026-08-26), covering only what's genuinely new or has changed since 2026-08-18.

## What's changed in Booking since the program log was written (2026-08-18)

Booking shipped real changes across F-014, F-016, and F-021 that the original scoping never saw. Verified by
reading `Booking/Program.cs` directly (464 lines now, not the ~150 implied by "three endpoints"):

**10 routes exist today, not 3:**

| Route | Added by | Dispatch pattern |
|---|---|---|
| `POST /appointments` (Book) | original | `RequestCollection` → hand-constructed `BookingAppointmentCommandHandler`, string-sniffed result |
| `PUT /appointments/` (Update) | original | same pattern |
| `DELETE /appointments/` (Cancel) | original | same pattern |
| `POST /appointments/{id}/status` | F-014 (ADR-037, server-owned status) | **inline in `Program.cs`**, typed `Results<Ok<...>,ForbidHttpResult,NotFound,Conflict<string>,BadRequest<string>>`, calls `bookingService` directly — no `RequestCollection`, no string-sniffing |
| `GET/POST /appointments/{id}/notes`, `PUT/DELETE /notes/{id}` | F-014 (session notes) | same inline/typed-`Results` pattern |
| `POST/GET /appointments/{id}/payment` | F-014 (payments) | same inline/typed-`Results` pattern |

**The three original routes still carry defect #3's exact shape** — `Booking/Requests/RequestCollection.cs`
hand-constructs `new BookingAppointmentCommandHandler(mediator, kafkaClient as KafkaClient, ...)` per call,
never calls `mediator.Send(...)`, and still has the `(kafkaClient as KafkaClient)` downcast — the same latent
NRE-on-substitution bug F-018-T10 fixed for Provider, filed for Booking as `agenda-buddy-5og`, still unfixed.

**The seven newer routes are already partway to the target shape** — typed `Results<>` returns (no
string-sniffing), direct service calls (no hand-constructed handler) — but still bypass MediatR entirely
(no `mediator.Send`), have no Clean Architecture layering, and use hand-rolled validation rather than Validot.

**F-016 added centralized `ForbiddenException` → 403 handling** (`AgendaBuddyExceptionHandler`, referenced at
`Program.cs:45`) — all 10 routes' local `catch (ForbiddenException) { return TypedResults.Forbid(); }` blocks
are now redundant with the central handler (kept for defense-in-depth per F-016's own design). F-019's rewrite
inherits this — the new Clean Architecture handlers should rely on the central handler, not re-add local
catches.

**Booking's audit/persistence/route-contract behavior is now covered by F-018's harness** — `BookingAuditTest.cs`,
`BookingPersistenceTest.cs`, `BookingRouteContractTest.cs` all exist and pass. This is F-019's actual
regression net, per the program log's Conflict B resolution: these tests assert HTTP status + persisted state,
not envelope shape, specifically so `DataResponse<T>` (F-019's own envelope change) doesn't break them for the
wrong reason.

## Genuinely open questions for F-019 (not settled by the program log)

1. Does F-019's Clean Architecture rewrite cover **all 10 routes**, or only the original 3 (the ones that
   actually have the defects the program was framed around)? The 7 newer routes already avoid defects #1/#3;
   rewriting them into 4-project layering with no functional defect to fix is a different kind of work
   (structural consistency) than the original 3 (defect repair).
2. `agenda-buddy-5og` (Booking's dormant `IKafkaClient` downcast) — fixed as part of this rewrite (natural,
   since `RequestCollection` is being deleted anyway), or handled separately/first?
3. Exact project layout: `Booking.Api` / `Booking.Core` / `Booking.Domain` / `Booking.Infrastructure` as
   siblings next to the existing `Booking` project (which becomes `Booking.Api`?), or `Booking` renamed/split
   in place? Test project split to match (`Booking.Core.Tests`, etc.) or keep one `Booking.Tests`?
4. **ROADMAP's inherited-scope note is stale.** It says F-019 "inherits the `services.BuildServiceProvider()`
   ASP0000 fix." Verified by grep: **no `.csproj`/`Program.cs` anywhere in the repo calls `BuildServiceProvider()`
   today** — the issue doesn't exist. Nothing to inherit; dropped from scope.

## Discover decisions (user-confirmed, 2026-08-26)

**Q1 — Route scope:** **All 10 routes.** Full consistency — every Booking route ends up in the same Clean
Architecture layering (`mediator.Send`, Validot, `DataResponse<T>`), not just the 3 that had the original
defects. The 7 F-014 routes' current typed-`Results<>` shape is a good sign for the target API contract but
still needs the MediatR dispatch + layering + envelope work.

**Q2 — Kafka downcast (`agenda-buddy-5og`):** **Folds into F-019.** `RequestCollection.cs` is deleted by this
rewrite regardless, so the bug disappears with it — no separate task. Close `agenda-buddy-5og` as
resolved-by-F-019 once it lands.

**Q3 — Project layout:** **`Booking` → `Booking.Api`, plus 3 new sibling projects** (`Booking.Core`,
`Booking.Domain`, `Booking.Infrastructure`), matching Gramli/AuthApi's actual layout directly.
`Booking.Tests` layout (split to match, or stay one project) is a Design-phase call, not decided here.

## Discovery Summary

**Confirmed by the user:** 2026-08-26T21:35:00Z

**Feature:** F-019 `api-refactor-pilot-booking` — stage 2 of 3. Applies the full Clean Architecture shape
(Api/Core/Domain/Infrastructure, MediatR as single dispatcher, Validot, `DataResponse<T>`, FluentResults,
Mapster, GuardClauses) to **all 10** of Booking's current routes, proving the target shape end-to-end on the
one service that exercises Kafka + EventStore audit + `RequestCollection` removal together.

**Problem:** Booking still carries the original defects on its 3 oldest routes (string-sniffed control flow,
hand-constructed handlers bypassing MediatR, the dormant `IKafkaClient` downcast). Its 7 newer routes
(F-014) are structurally closer to the target but still bypass MediatR, use hand-rolled validation, and have
no Clean Architecture layering. F-019 unifies all 10 under one consistent shape.

**User:** Same as the program log — this repo's developers. No provider/customer-facing behavior change
expected; `DataResponse<T>`'s envelope is a contract change for any client, but `MobileApp` is F-020's/a
later feature's concern to re-wire, not F-019's (F-019 proves the shape on one service, doesn't propagate it).

**Success metric:** All 10 routes dispatch through `mediator.Send`; zero string-sniffed control flow; zero
hand-constructed command handlers; zero `IKafkaClient` downcasts; `RequestCollection`/`IRequestCollection`
deleted from Booking; F-018's Tier 1/2/3 tests for Booking still pass (status codes + persisted state
unchanged — the actual regression-net proof); new envelope/validation/mapping tests added for the Core layer.

**Out of scope:** The other 6 services (F-020). `MobileApp` re-wiring to the new envelope. The
`services.BuildServiceProvider()` fix (doesn't exist — dropped).

**Key risks / assumptions carried from the program log:** container-per-class + the 10-minute CI budget
(F-018 measured comfortable at ~20 tests; F-019 is the feature that tests this for real, per ADR-017's
explicit tripwire). `SmallApiToolkit`'s narrow-slice-only adoption (`DataResponse<T>`, validation base,
`ExceptionMiddleware` — not `IHttpRequestHandler`). Assert behavior not envelope in F-018's existing Booking
tests (already true, confirmed by reading them) so this rewrite doesn't fight its own regression net.
