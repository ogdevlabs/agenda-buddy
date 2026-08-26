# Architecture — API Refactor Pilot: Booking (F-019)

## 1. Where this feature lives

Four new projects replace the current single `Booking` project's role, plus the entities it already shares
with everyone else:

```
Booking.Api/            (was Booking — thin: endpoint definitions, DI wiring, no logic)
Booking.Core/           (new — MediatR command/query handlers, moved from EventAndCommands/Commands/Booking
                          and EventAndCommands/Queries/Calendar's Booking-owned pieces — see §4)
Booking.Domain/         (new — commands, queries, request/response DTOs, DataResponse<T>)
Booking.Infrastructure/ (new — only if Booking needs its own repository wrapper; likely thin, since
                          Library.Repositories.MongoDbRepository<T> already exists and is reused, not
                          duplicated — see §6)
```

`Booking.Tests` stays as one project (not split per new project) — see §6 for why.

## 2. What moves, what stays

| Today | Becomes |
|---|---|
| `Booking/Program.cs` (464 lines: endpoints + inline logic) | `Booking.Api/Program.cs` (endpoints only, each body is `await mediator.Send(command, ct)`) |
| `Booking/Requests/RequestCollection.cs`, `IRequestCollection.cs` | **Deleted.** Replaced by MediatR dispatch — no replacement type needed. |
| `EventAndCommands/Commands/Booking/*CommandHandler.cs` (3 handlers: Book/Update/Cancel) | Move to `Booking.Core/Commands/` |
| `EventAndCommands/Commands/Booking/*Command.cs` (3 commands) | Move to `Booking.Domain/Commands/` |
| The 7 F-014 routes' inline logic (status/notes/payment, currently calling `bookingService` directly in the route lambda) | Each becomes a new MediatR command/query + handler in `Booking.Domain`/`Booking.Core` |
| `Library.Services.BookingService` | **Unchanged, stays in `Library`.** Handlers in `Booking.Core` call it exactly as `RequestCollection` and the F-014 route lambdas do today — this is not a repository/service rewrite, only a dispatch and layering rewrite. |
| `Library.Repositories.MongoDbRepository<T>`, `IRepository<T>` | **Unchanged, stays in `Library`.** `Booking.Infrastructure` is deliberately thin — see §6. |

## 3. New module: `Booking.Domain`'s `DataResponse<T>`

Per ADR-049, this is an in-repo type, not a package type:

```csharp
namespace Booking.Domain.Responses;

public sealed record DataResponse<T>(T? Data, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;
    public static DataResponse<T> Ok(T data) => new(data, []);
    public static DataResponse<T> Fail(IEnumerable<string> errors) => new(default, errors.ToList());
}
```

Wraps every one of Booking's 10 routes' response bodies. Serialization must be verified against
`ObjectIdJsonConverter` (already registered for Booking per CLAUDE.md's Key Files) — `Data`'s inner shape
(e.g. an `AppointmentResponse` DTO carrying a Mongo `ObjectId`-backed identifier as a string) needs the
converter to still fire on the nested type. **Spike this at Plan, before the first task claims it works.**

## 4. Command/query dispatch flow (representative: Book Appointment)

```
POST /api/v1/booking/appointments
  → Booking.Api: validate route params, build BookAppointmentCommand from the request DTO
  → mediator.Send(command, ct)          [ct = the real HTTP request's CancellationToken]
  → Booking.Core.BookAppointmentCommandHandler.Handle(command, ct)
      → GuardClauses: null/argument checks on the command
      → Library.Services.BookingService.BookAppointmentAsync(...)   [unchanged]
      → EventStore.SaveAsync(event)                                  [unchanged — CONSTITUTION §3]
      → returns FluentResults.Result<BookAppointmentResult>
  → Booking.Api: map Result → DataResponse<T> (Ok or Fail), map to the right HTTP status via TypedResults
```

**Where `Validot` fits:** validates the incoming request DTO in `Booking.Api` (or a `Booking.Domain`
validator invoked from the endpoint filter) *before* the command is built — the same place
`MiniValidator.TryValidate` runs today, just a different library. Not inside the MediatR handler; a
handler receiving an already-validated command is what makes it safely mockable/testable in isolation.

**Where `Mapster` fits:** request DTO → command (in `Booking.Api`, before `Send`); domain result → response
DTO (in `Booking.Api`, after `Send` returns). Handlers never see a DTO, only domain types — this is what
keeps `AppointmentEntity` out of the route signature (Requirement 7) without hand-writing 20 mapping methods.

## 5. Integration with existing modules

- **`IMediator` is already registered and injected in all 7 services** (confirmed unchanged from the
  program-level brainstorm's Progressive Thinking finding) — F-019 needs zero new DI registration for
  MediatR itself, only for the moved handlers' constructors (`Booking.Core`'s DI module, registered from
  `Booking.Api`'s `Program.cs`).
- **`Library.ServerAuth.AgendaBuddyExceptionHandler`** (F-016) is unchanged and untouched — Booking's new
  handlers rely on it for `ForbiddenException` → 403, per ADR-049. `Booking.Api` registers it exactly as
  every other service does today (`UseAgendaBuddyTransportSecurity()` before `UseAuthentication()`,
  CLAUDE.md's Key Files entry for `TransportSecurity.cs` — unaffected by this feature).
- **`EventStore`/`IEventStore`** (`EventAndCommands/Persistence/`) — unchanged. Handlers in `Booking.Core`
  call `eventStore.SaveAsync(...)` exactly where the current handlers do, at the same point in the
  success/failure flow. This is what keeps `AgendaBuddy.IntegrationTests/Audit/BookingAuditTest.cs` and
  `EventStoreWriteGuardTest.cs` valid — **but `EventStoreWriteGuardTest`'s `HandlerFileNames` enumeration
  must be updated to the new `Booking.Core/` paths**, or it silently stops covering Booking (flagged in the
  PRD's Known Risks; this is the concrete fix).
- **`IKafkaClient`** — Booking's handlers don't create topics (only Provider registration does, per F-018's
  own `KafkaClientFake` scoping); Booking's Kafka involvement today is limited to the dormant downcast bug in
  `RequestCollection.cs`, which is deleted outright by this rewrite (PRD Requirement 4). No new Kafka wiring
  needed in `Booking.Core`.
- **`AgendaBuddy.IntegrationTests`** — `Harness/ServiceHostFixture<BookingAnchor>` continues to boot
  `Booking.Api` (the anchor type moves with the project, still public, still the thing
  `InternalsVisibleTo` grants access to). No harness change needed beyond what Requirement 12/13 already
  requires (the existing Contract/Persistence/Audit tests keep passing).

## 6. Architectural decisions

1. **`Booking.Infrastructure` is thin, possibly near-empty.** `MongoDbRepository<T>` already exists in
   `Library` and is used by every service; duplicating it per-service-project would be the same mistake
   Neo's YAGNI lens would flag at Review. `Booking.Infrastructure` exists for layering completeness (matches
   Gramli/AuthApi's 4-project shape, which Plan should size honestly rather than pad) and as the place a
   future Booking-specific repository concern would go, if one ever arises.
2. **`Booking.Tests` stays one project**, not split per new project (`Booking.Core.Tests` etc.). The existing
   test project already references `Booking` (soon `Booking.Api`); splitting it is pure ceremony with no
   Booking-specific consumer waiting on the split, and F-018's own precedent (`AgendaBuddy.IntegrationTests`
   as one project, not one per tier) supports keeping test surface area consolidated unless a real reason
   to split appears.
3. **`DataResponse<T>` lives in `Booking.Domain`, not a new shared project.** F-020 will need the same type
   for the other six services — moving it to a shared location (`Library` or a new `Shared.Contracts`
   project) is explicitly **F-020's decision**, made once the second consumer exists and its actual needs
   are known, not speculated now for a consumer that doesn't exist yet (YAGNI).
4. **The route/verb/payload shape of all 10 routes does not change** — same paths, same HTTP verbs, same
   request bodies. Only the response envelope (`DataResponse<T>` wrapping) and the internal dispatch
   mechanism change. This is why `BookingRouteContractTest.cs` (status codes) keeps passing unmodified —
   it never asserted the envelope.

## 7. Conformance with CONSTITUTION §3

- **MediatR as CQRS dispatcher**: now literally true for Booking (was previously registered-but-unused).
- **EventStore audit on every command**: unchanged mechanism, unchanged call sites' semantics, moved files.
- **Cache-aside**: N/A to Booking (confirmed no cached reads exist today); not introduced by this feature.

## 8. What this architecture deliberately does not do

- Does not touch Calendar, Customer, Provider, Services, Profession, or Identity (F-020).
- Does not re-wire `MobileApp` to the new envelope.
- Does not change any route's path, verb, or request body shape.
- Does not introduce `SmallApiToolkit` (ADR-049) or any dispatch abstraction competing with MediatR (ADR-014).
- Does not change `Library.Services.BookingService`'s public surface — this is a controller/handler-layer
  rewrite, not a business-logic rewrite.

## 9. Open items carried into Plan

- Spike `DataResponse<T>` + `ObjectIdJsonConverter` interaction (§3) before sizing tasks around it.
- Confirm `Validot`'s actual validation-rule API against Booking's existing `[Required]`/`[EmailAddress]`
  data-annotation-based DTOs (do these get replaced entirely, or does Validot wrap/coexist? — Bloom's
  Round 1 question, not yet asked).
- Size `EventStoreWriteGuardTest`'s update as its own task, not an afterthought inside a bigger task — it's
  cheap, but forgetting it silently reopens a gap Party Review already flagged once (F-018's N1/E1).
