# Changelog

All notable changes to this project are documented in this file, in [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) style.

## [Unreleased]

## [0.11.0] - 2026-08-27

### Added

- **F-022 password-reset-flow**: `POST /api/v1/auth/password-reset/request` and
  `/password-reset/confirm` — a single-use, 30-minute-expiry opaque token (same hash-only-storage
  pattern as the refresh token), anti-enumeration (`request` always returns `202`), and confirming
  clears any active session and lockout. `LoginAsync` now enforces `CredentialEntity.MustResetPassword`
  (`403 password_reset_required`) instead of silently ignoring it. No real email/SMS provider exists in
  this project (ADR-052, same category as ADR-038's non-charging payment gateway) — the reset token is
  logged for local development and mirrored into the existing in-app notification inbox as a secondary
  signal. Mobile UI is deliberately out of scope (`agenda-buddy-qe9`).

## [0.10.0] - 2026-08-27

### Fixed

- **F-025 booking-correctness**: `POST /api/v1/booking/appointments` accepted appointments booked
  backwards (`End` before `Start`), in the past, and overlapping another appointment already booked
  for the same provider — zero domain-invariant checks existed before this. Now enforced: `Start < End`
  at the Validot boundary, future-dating and the overlap check in `BookingAppointmentCommandHandler`.
  An appointment immediately adjacent to an existing one is not treated as an overlap. The overlap
  check is a documented, accepted read-then-insert race (ADR-051), not an atomic conditional write —
  see `docs/pdlc/design/booking-correctness/ARCHITECTURE.md`.

## [0.9.0] - 2026-08-27

### Changed

- **Rolled Booking's Clean Architecture pattern out to 5 more services** — Calendar, Customer, Provider, Services, Profession all now split into `<Service>.Api`/`Core`/`Domain`/`Infrastructure`, each with its own `mediator.Send` dispatch, `FluentResults.Result<T>`, and in-repo `DataResponse<T>` envelope. `RequestCollection`/`IRequestCollection` deleted for all 5. `Identity` is deliberately excluded — it never adopted the CQRS/`RequestCollection` shape the others share, so migrating it would introduce the pattern fresh, not replicate a proven one.
- **Every project in the solution — all 47 — now carries the `AgendaBuddy.` prefix**: folder, `.csproj`, solution reference, and internal C# namespace, matching the convention `AgendaBuddy.AppHost`/`ServiceDefaults`/`IntegrationTests` set at F-013. This includes a retroactive rename of Booking's own 5 projects (shipped last release) plus `Library`, `EventAndCommands`, `Kafka`, `Gateway`, `Identity`, and `MobileApp` — all pure renames with no behavior change.
- `AgendaBuddy.EventAndCommands` now holds zero command/query handler implementations — every service's handlers live in its own `*.Core` project.
- `DataResponse<T>` stays per-service, not extracted to a shared package, even with 6 total near-identical copies now — no cross-service code needs the same type, only the same shape.

### Fixed

- **Threat T-204**: `Customer`'s `AddCustomerCommandHandler` was still typed against the concrete `KafkaClient` class rather than `IKafkaClient` — the one `agenda-buddy-5og`-shaped copy of this bug F-018/F-019 never touched. Retyped; a real `InvalidOperationException` under live MediatR dispatch would have resulted otherwise.
- 2 genuinely dead command handlers deleted rather than migrated forward: `BookCalendarCommand` (Calendar) and `AddProfessionCommand` (Profession) — both unreachable, `NotImplementedException`-bodied, with no route or possible DI resolution path.
- A real cross-service namespace bug, unrelated to any rename: `ProblemDetailsServiceEndpointFilter.cs` lived under `namespace Customer.Extensions;` inside the *Profession* project, compiling only because of a compensating `global using` — fixed to the correct namespace.
- `AgendaBuddy.MobileApp`'s `CustomerApiService.ParsePagedCustomers` read `items` at the response root; wrapping `GET /customers` in `DataResponse<T>` moved it to `data.items` — fixed the parser and its test fixtures.
- A subtle Aspire bug found live: a service's `appsettings.json`/`appsettings.Development.json` `Kestrel:Endpoints` blocks got swapped during project scaffolding, silently zeroing Aspire's endpoint auto-detection for that resource (no compile error — an empty collection where one was expected). Restored from git history.
- `scripts/generate-openapi.sh`'s `project_dir()` mapping and `scripts/run-ios.sh`'s service arrays, each missing an entry for one or more renamed projects — found and fixed across several of this release's own commits.

### Known issues

- `agenda-buddy-02e` (Booking's Update/Cancel routes still on `MiniValidator`) and `agenda-buddy-cy2` (Booking's null-`EmailProvider` 500) — both pre-existing, Booking-scoped, unchanged by this release.
- Customer's `UpdateCustomerCommandHandler` still audits its not-found branch under the wrong event `Type` (a copy-paste defect, already ruled out of scope at F-018-T13) — preserved, not fixed, pinned by a test.
- Services' Add/Update handlers still skip an audit write on 2 specific branches — pre-existing, pinned by tests, not fixed.
- Mapster remains approved (ADR-049) with zero call sites across all 6 migrated services.

## [0.8.0] - 2026-08-27

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

[Unreleased]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.9.0...HEAD
[0.9.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.8.0...v0.9.0
[0.8.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.7.0...v0.8.0
