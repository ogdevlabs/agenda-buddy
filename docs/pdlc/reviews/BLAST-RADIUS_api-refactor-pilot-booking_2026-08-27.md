# Blast Radius — api-refactor-pilot-booking (F-019)

**Scope:** large diff (>40 changed files) — exported/public symbols + anything whose signature, return
type, or project identity changed. Most of this analysis was performed live during Build (T04's DI/Kafka
fixes, T06's full-suite-vs-narrow-filter discovery, T10's pre-deletion reference checks, T11's `git diff
--name-only` blast-radius confirmation) rather than re-derived here from scratch — this file consolidates
that evidence into the reviewer-facing shape.

**Symbols examined:** ~15 (3 renamed commands, 7 new commands/queries, 1 new type `DataResponse<T>`, 2
deleted types `RequestCollection`/`IRequestCollection`, 1 deleted duplicate command pair, 1 renamed project)
**Call sites found:** 100+ (mostly within `Booking.*`) **⚠ At risk:** 0 **Untested paths:** 0

## ⚠ At risk (review these first)

None found. Every changed public contract either has zero external callers (confirmed by repo-wide grep,
not just the diff) or was updated at its one call site in the same commit.

## Contract changes

| Contract | Consumers named | Verdict |
|---|---|---|
| `BookAppointmentCommand`/`UpdateAppointmentCommand`/`CancelAppointmentCommand`: `IRequest<string>` → `IRequest<Result<AppointmentEntity>>` | Only consumer is `Booking.Api/Program.cs`'s own 3 routes — updated in the same task (T04) | ✅ updated |
| `Booking.Requests.RequestCollection`/`IRequestCollection` — deleted outright | Repo-wide grep for `RequestCollection`/`IRequestCollection` finds 6 **separate, namespace-scoped** classes in Calendar/Provider/Profession/Customer/Services — each its own independent type, zero shared base or interface with Booking's. Deletion has zero cross-service impact | ✅ confirmed, no external consumers |
| `EventAndCommands.Commands.Booking.ChangeAppointmentStatusCommand`/Handler — deleted (T10) | Repo-wide grep for `EventAndCommands.Commands.Booking` (post-deletion): 0 matches | ✅ confirmed, no consumers survived |
| `Booking` project → `Booking.Api` (folder/csproj/Aspire `Projects.Booking` → `Projects.Booking_Api`) | `AgendaBuddy.AppHost/AppHostWiring.cs` (updated), `Booking.Tests`/`AgendaBuddy.IntegrationTests` `ProjectReference`s (updated), CI's docker-build matrix/path-filters/structural test (updated), `generate-openapi.sh`/`run-ios.sh` service arrays (updated), `TransportSecurityOrderTest`'s hardcoded list (updated). Repo-wide grep for `Projects.Booking\b` (old name): 0 matches | ✅ all consumers found and updated — see `verification.md` §3.9 for the .NET SDK container-naming gotcha found only by actually running the publish |
| New: `DataResponse<T>`, `ChangeAppointmentStatusCommand`/`CreateAppointmentNoteCommand`/`UpdateAppointmentNoteCommand`/`DeleteAppointmentNoteCommand`/`PayForAppointmentCommand`/`GetAppointmentNotesQuery`/`GetAppointmentPaymentQuery` | Greenfield — no prior callers, confirmed by construction (all new files) | ✅ n/a |
| `AgendaBuddy.IntegrationTests.OpenApi.OpenApiSpecCatalog["Booking"]` key, `EntryPoints.Booking` alias | Both still reference `Booking.Configuration.MongoDbConfiguration` by namespace, which was deliberately NOT renamed to `Booking.Api.Configuration` — decision recorded in `verification.md` §4 | ✅ unaffected by the project rename |

## Untested changed paths

None. Every new command/query handler has either a real Moq-based unit test (Notes/Payment, `INoteService`/
`IPaymentService` are mockable interfaces) or a GuardClause-only unit test plus full end-to-end integration
coverage (Book/Update/Cancel/Status, `BookingService`/`ProviderService` are concrete and unmockable per
`verification.md`'s TDD discussion) — see `Booking.Tests/Commands/`, `Booking.Tests/Queries/`, and the 310
integration tests.

## Full call-site map

- `BookAppointmentCommand`/`UpdateAppointmentCommand`/`CancelAppointmentCommand` → `Booking.Api/Program.cs` (3 routes, updated), `Booking.Tests/Commands/*HandlerTest.cs` (updated)
- `ChangeAppointmentStatusCommand` (new, Booking.Domain) → `Booking.Api/Program.cs`'s status route (updated at T06); the **old** `EventAndCommands.Commands.Booking.ChangeAppointmentStatusCommand` had exactly one caller (the same route, pre-T06) and is now deleted (T10) with the caller already migrated
- `CreateAppointmentNoteCommand`/`UpdateAppointmentNoteCommand`/`DeleteAppointmentNoteCommand`/`GetAppointmentNotesQuery` → `Booking.Api/Program.cs`'s 4 Notes routes (updated at T06)
- `PayForAppointmentCommand`/`GetAppointmentPaymentQuery` → `Booking.Api/Program.cs`'s 2 Payment routes (updated at T06)
- `RequestCollection`/`IRequestCollection` (Booking's own) → 0 remaining callers repo-wide (deleted, T04)
- `EventsHelper` (Booking's own, wrapped `RequestCollection`) → 0 remaining callers (deleted alongside `RequestCollection`, T04 — disclosed AC14 consequence, see `verification.md`)
- `Booking.csproj` → `Booking.Api.csproj` → `AgendaBuddy.AppHost.csproj`, `Booking.Tests.csproj`, `AgendaBuddy.IntegrationTests.csproj` (all 3 `ProjectReference`s updated, T04)
