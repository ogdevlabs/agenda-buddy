# Changelog

All notable changes to this project are documented in this file, in [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) style.

## [Unreleased]

### Changed

- **Booking split into a 4-project Clean Architecture pilot** (`Booking.Api`, `Booking.Core`, `Booking.Domain`, `Booking.Infrastructure`), replacing the single `Booking/` project the other 6 services still use. `Booking.Api` is now thin — endpoint/DI wiring only; command/query handlers moved to `Booking.Core`, dispatched via `IMediator` instead of hand-constructed by the old `RequestCollection`, which is deleted.
- Every Booking command/query handler now returns `FluentResults.Result`/`Result<T>` instead of a string-sniffed `"exception"`-prefixed convention.
- Introduced `DataResponse<T>` (`Booking.Domain/Responses/DataResponse.cs`) as the response envelope for Booking's routes — `Success`/`Data`/`Errors`, mapped from each handler's `Result<T>` at the API boundary.
- Started migrating Booking's request validation from `MiniValidator` to Validot's declarative `Specification<T>` DSL: `POST /appointments` (Book) and the two note-content routes now validate via Validot. `PUT`/`DELETE /appointments/` (Update, Cancel) still use `MiniValidator` — tracked as `agenda-buddy-02e`, not a silent gap.
- `UpdateAppointmentCommandHandler` and `CancelAppointmentCommandHandler` now depend on `IProviderService`/`IBookingService` rather than the concrete `ProviderService`/`BookingService`, making both independently unit-testable with Moq. `BookAppointmentCommandHandler` stays on the concrete `ProviderService`/`BookingService` — it calls `AppendAppointmentAsync`, which isn't on `IProviderService`, and adding it would be a `Library` change out of scope for this pass.

### Fixed

- `PUT /appointments/` (Update) no longer echoes the client-submitted `AppointmentStatus` back in the response body. The database write already ignored it (threat T-203), but the response previously reflected the caller's forged value rather than the actual persisted status — a caller could not tell from the response alone that their forged status was rejected.
- A dormant downcast bug in the three Booking command handlers moved this feature (`Book`/`Update`/`Cancel`): each took a concrete `KafkaClient?` constructor parameter, resolvable from DI only as `IKafkaClient` — any attempt to actually use it would have thrown at resolution time. The parameter was unused in all three and has been removed rather than fixed in place.
- Validot's originally-authored note-content spec (`NoteSpec`) used `.Required().NotEmpty()`, which accepts a whitespace-only string — a strictness regression relative to the inline `IsNullOrWhiteSpace` check it replaces. Fixed to `.Required().NotWhiteSpace()`, verified live against the Validot 2.6.0 assembly to match `!string.IsNullOrWhiteSpace(x)` exactly before wiring it into any route.

### Known issues

- 2 of Booking's 10 routes (`Update`, `Cancel`) still validate via `MiniValidator`, not Validot — `agenda-buddy-02e`.
- A `null` `EmailProvider` on `POST /appointments` passes both Validot and the ownership guard, then throws downstream during provider lookup, surfacing as an unhandled 500 rather than a 400 — `agenda-buddy-cy2`.
- Mapster is approved (ADR-049) for this line of work but has zero call sites yet.

[Unreleased]: https://github.com/fererelabs/agenda-buddy/compare/v0.7.0...HEAD
