# Architecture: Booking Correctness (F-025)

## 1. Boundary placement

Two of the three invariants are stateless (need only the request payload); one needs the database and
the clock. That split decides where each check lives, matching how this repo already separates
concerns at `POST /appointments`:

| Check | Needs | Lives in | Why |
|---|---|---|---|
| `Start < End` | request payload only | `AppointmentEntitySpecification.Spec` (Validot, API boundary) | Pure predicate, no I/O — this route already runs Validot before dispatch (`AgendaBuddy.Booking.Api/Program.cs`), and it's one of only 3 of Booking's 10 routes on Validot rather than `MiniValidator` (Requirement 6 migration, in progress). |
| `Start` is future-dated | the clock | `BookingAppointmentCommandHandler.Handle` (Booking.Core) | Needs "now," which Validot specs — stateless static delegates — don't have a clean DI path to. Uses the existing `IDateTimeProvider`/`SystemDateTimeProvider` abstraction (`AgendaBuddy.Library/Tools/IDateTimeProvider.cs`), already used by `AgendaBuddy.Identity`, registered here for the first time in Booking (`ServiceCollectionExtension.AddMongoDbRepository`). |
| No overlap with an existing appointment for the same provider | the database | `BookingAppointmentCommandHandler.Handle`, via a new `BookingService.FindOverlappingAppointmentsAsync` | Needs a query against `appointments`. Added as a plain method on the concrete `BookingService` — the handler already depends on the concrete class, not `IBookingService`, for the same reason `AppendAppointmentAsync` isn't on `IProviderService`: this handler's dependency shape was already widened for one Library-internal call, and mirroring that (rather than growing the interface for a single caller) keeps the pattern consistent. |

Both handler-side checks return `Result.Fail<AppointmentEntity>(...)` before any provider lookup or
write — the same shape the handler already used for "no provider found," so `BookAppointment`'s
Api-layer `BadRequest(DataResponse<AppointmentEntity>.Fail(...))` mapping needed no change.

## 2. What changed

- `AgendaBuddy.Booking.Api/Validation/AppointmentEntitySpecification.cs` — added
  `.Rule(m => m.Start < m.End).WithMessage(...)`.
- `AgendaBuddy.Library/Services/BookingService.cs` — added
  `FindOverlappingAppointmentsAsync(emailProvider, start, end)`, a `FindAllAsync` call against
  `appointments` filtered by `email_provider` and a half-open range test (`start < end && end > start`,
  the standard interval-overlap predicate).
- `AgendaBuddy.Booking.Core/Commands/BookingAppointmentCommandHandler.cs` — added the future-dating
  check and the overlap check, both before the existing provider lookup; added an `IDateTimeProvider`
  constructor parameter.
- `AgendaBuddy.Booking.Api/Extensions/ServiceCollectionExtension.cs` — registered
  `IDateTimeProvider`/`SystemDateTimeProvider` as a singleton (mirrors `AgendaBuddy.Identity/Program.cs`).

No entity schema change, no new collection, no new NuGet package.

## 3. Concurrency: an accepted race, not an atomic write

The feature record named three candidate designs for the overlap check. This ships **option 3** — a
documented, accepted race between the overlap read and the insert — not an atomic conditional write or
a slot-key unique index. Reasoning:

- **Atomic conditional write.** `IRepository<T>.FindOneAndUpdateAsync` (ADR-032) expresses "change one
  field on a document that already exists"; it doesn't express "insert only if nothing else matches a
  range predicate" — that needs an aggregation-pipeline update or a transaction, either of which is a
  materially bigger change to the repository's primitive set for a single call site, and MongoDB.Driver
  is pinned at 2.25.0 (CLAUDE.md's Aspire caveat) without having verified that surface against the pin.
- **Unique index on a derived slot key.** Appointments have arbitrary `Start`/`End`, not slots aligned
  to a fixed grid — forcing a slot granularity would change what a "conflict" means (two genuinely
  non-overlapping 20-minute appointments could collide on a coarse slot key) rather than express the
  actual invariant.
- **Accepted race (chosen).** Between `FindOverlappingAppointmentsAsync` returning empty and the
  `InsertAsync` that follows, a second concurrent booking for the *same provider* in an overlapping
  window could still both pass the check and both insert. This is the same shape of race
  `SearchAndUpdateProviderAppointments`'s own doc comment already flags as *fixed* for the
  provider's embedded list (`AppendAppointmentAsync`'s atomic `$push`, ADR D-9) — this PRD does **not**
  reopen that one; it only accepts a *new, narrower* race for the pre-insert overlap check itself. For a
  single-provider calendar, concurrent double-submission of overlapping times is a low-frequency edge
  case, not a routine load pattern, and closing the "zero checks at all" gap has a much higher
  correctness return per unit of change. If usage ever shows this race firing in practice, revisit with
  real data rather than pre-optimizing against a guess.

## 4. Explicitly not touched here: `AppointmentStatus.Cancelled`

`AppointmentEntity.TransitionTo`'s own remarks already say `Cancelled` is "deliberately not reachable"
pending a product decision, because `CancelAppointmentAsync` hard-deletes rather than transitioning.
Making it soft-delete touches three things this feature doesn't: the provider's embedded appointment
list (needs its own atomic removal-or-mark primitive), `ReportingService`'s counts (which would need to
start excluding cancelled-but-present rows), and F-024's future erasure work (a soft-deleted row is a
row F-024 still has to account for). Filed separately as `agenda-buddy-m6m` rather than bundled in —
same reasoning F-014→F-025's own split used: different shape of work, no technical dependency forcing
them together.
