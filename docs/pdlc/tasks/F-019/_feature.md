---
id: F-019
title: api-refactor-pilot-booking
status: planned
priority: 20
labels: [roadmap, "priority:20"]
depends_on: [F-018, F-016]
claimed_by: null
created: 2026-08-18
updated: 2026-08-18
---
**Stage 2 of 3 in the API refactor program (F-018 → F-019 → F-020).** Apply the full target pattern to **one** service — `Booking` — end to end, proving the shape before it is replicated six more times. CLAUDE.md already describes `Booking/Program.cs` as "the representative Minimal API entry point showing the full wiring pattern", and Booking exercises every concern the pattern must handle: three write endpoints, ownership guards, Kafka, and the EventStore audit trail.

Target pattern, per the [Gramli/AuthApi](https://github.com/Gramli/AuthApi) reference as adapted by the F-018 Inception decisions:

- **Full Clean Architecture layering** — `Booking.Api` / `Booking.Core` / `Booking.Domain` / `Booking.Infrastructure`.
- **MediatR is the single dispatcher.** Endpoints call `mediator.Send(command)`, finally honouring CONSTITUTION §3 and eliminating the hand-constructed `new SomeCommandHandler(...)` calls. `SmallApiToolkit`'s `IHttpRequestHandler` is deliberately **not** used as a competing dispatch mechanism.
- **`SmallApiToolkit`** for `DataResponse<T>`, the validation base class, and the shared `ExceptionMiddleware` — replacing the ~40-line exception-handler block currently duplicated in all 7 `Program.cs` files.
- **`FluentResults`** replaces string-sniffed control flow (`StartsWith("exception")`) and the per-endpoint `try/catch (ForbiddenException)`.
- **`Validot`** replaces per-endpoint `MiniValidator.TryValidate`.
- **`Mapster`** + request/response DTOs, so `AppointmentEntity` stops being the public API contract.
- **`GuardClauses`** for defensive checks.
- The real request `CancellationToken` threaded through — no more `new CancellationToken()`.
- EventStore audit and cache-aside preserved (CONSTITUTION §3).

Gate on the mechanical zero-counts agreed at Inception. Also records the prediction to reconcile at F-014's ship: adding a route should touch one new file and no `Program.cs`.
