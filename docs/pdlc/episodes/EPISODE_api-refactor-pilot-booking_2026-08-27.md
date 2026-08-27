# Episode 008: API Refactor Pilot — Booking

**Episode ID:** 008
**Feature name:** API Refactor Pilot — Booking
**Feature slug:** api-refactor-pilot-booking
**Date delivered:** 2026-08-27
**Phase delivered in:** Construction
**Status:** Draft

---

## What Was Built

Booking became the pilot for a 4-project Clean Architecture layering that F-020 will replicate across the
other 6 services: `Booking.Api` (thin — endpoints/DI only, was `Booking/`), `Booking.Core` (MediatR command
and query handlers), `Booking.Domain` (commands/queries/DTOs, the new `DataResponse<T>` envelope), and
`Booking.Infrastructure` (deliberately empty — nothing needed it yet). All 10 of Booking's routes now
dispatch through real `mediator.Send(command, ct)` — `RequestCollection`, the hand-construction workaround
that used to exist because handlers took per-request values as constructor parameters, is deleted. Every
handler returns `FluentResults.Result`/`Result<T>` instead of a string-sniffed `"exception"`-prefixed
convention, mapped to `DataResponse<T>` (`Success`/`Data`/`Errors`) at the API boundary. Validation started
migrating from `MiniValidator` to Validot's declarative `Specification<T>` DSL: 3 of the 10 routes use it
today (Book, and the 2 note-content routes — the latter wired during Party Review, after catching a real
whitespace-acceptance bug in the originally-authored spec before it shipped). A real, pre-existing defect
(Update's response echoing the client's forged `AppointmentStatus`, even though the database write already
correctly ignored it) was found while closing this feature's own AC8 evidence gap and fixed during Party
Review remediation, along with the untested handler branches that let it ship undetected the first time.

---

## Links

- **PRD:** [PRD_F-019_api-refactor-pilot-booking_2026-08-26.md](../prds/PRD_F-019_api-refactor-pilot-booking_2026-08-26.md)
- **PR:** not yet opened
- **Review file:** [REVIEW_api-refactor-pilot-booking_2026-08-27.md](../reviews/REVIEW_api-refactor-pilot-booking_2026-08-27.md)
- **Blast radius:** [BLAST-RADIUS_api-refactor-pilot-booking_2026-08-27.md](../reviews/BLAST-RADIUS_api-refactor-pilot-booking_2026-08-27.md)
- **Design docs:** [ARCHITECTURE.md](../design/api-refactor-pilot-booking/ARCHITECTURE.md) | [data-model.md](../design/api-refactor-pilot-booking/data-model.md) | [threat-model.md](../design/api-refactor-pilot-booking/threat-model.md) | [api-contracts.md](../design/api-refactor-pilot-booking/api-contracts.md) | [validot-spike-findings.md](../design/api-refactor-pilot-booking/validot-spike-findings.md) | [verification.md](../design/api-refactor-pilot-booking/verification.md)

---

## Key Decisions & Rationale

1. **4-project split (`Booking.Api`/`Booking.Core`/`Booking.Domain`/`Booking.Infrastructure`), not 3 or 5** —
   ADR-049 dropped a planned `SmallApiToolkit` dependency (it doesn't ship a response-envelope type this
   project needs) in favor of an in-repo `DataResponse<T>` record, settling on 4 packages
   (FluentResults, Validot, GuardClauses, Mapster) and 4 projects, not 5.
2. **`Booking.Infrastructure` stays empty** — YAGNI. No Booking-specific repository/infrastructure need
   arose in this feature; creating abstractions for a need that doesn't exist yet was rejected.
3. **Requirement 7 (Mapster-based response DTOs) not implemented this feature** — never assigned to any of
   the 11 tasks. `AppointmentEntity` keeps flowing through route signatures unchanged. Mapster has zero call
   sites in this feature as a result; disclosed rather than left implicit, so F-020 doesn't assume a
   pattern to copy that was never actually built.
4. **Requirement 6 (Validot everywhere) only reached 3 of 10 routes** — Book at build time, the 2
   note-content routes during Party Review remediation. Update/Cancel remain on `MiniValidator`
   (`agenda-buddy-02e`) — no task ever assigned that conversion for them.
5. **`Book`'s handler stayed on the concrete `ProviderService`/`BookingService`; `Update`/`Cancel`'s were
   retyped to `IProviderService`/`IBookingService`** — `Book` calls `AppendAppointmentAsync`, not on the
   interface, and adding it would be an out-of-scope `Library` change. `Update`/`Cancel` need nothing the
   interfaces don't already have, and Echo's Party Review finding showed the concrete typing was actively
   harmful — it made both handlers' real branches untestable with Moq.
6. **A real, pre-existing response-integrity bug (`agenda-buddy-2hd`) was fixed during Party Review, not
   deferred** — found while closing this feature's own AC8 evidence gap (3 reviewers converged on it
   independently), it was cheap to fix once the handler was already being retyped for the finding above, so
   fixing beat filing.

---

## Files Created

- `Booking.Api/`, `Booking.Core/`, `Booking.Domain/`, `Booking.Infrastructure/` — the 4 new projects (via
  rename for `Booking.Api`, from-scratch for the other 3), each with its own `.csproj`/`GlobalUsings.cs`
- `Booking.Domain/Responses/DataResponse.cs` — the `DataResponse<T>` envelope record
- `Booking.Domain/Commands/*.cs`, `Booking.Domain/Queries/*.cs` — commands/queries for all 10 routes
  (`Book`/`Cancel`/`Update`/`ChangeStatus`/`CreateNote`/`UpdateNote`/`DeleteNote`/`GetNotes`/`Pay`/`GetPayment`)
- `Booking.Core/Commands/*.cs`, `Booking.Core/Queries/*.cs` — the matching MediatR handlers
- `Booking.Api/Validation/AppointmentEntitySpecification.cs`, `AppointmentExtrasRequestsSpecifications.cs` —
  the Validot specs (`NoteSpec` survives to ship; `StatusSpec`/`PaymentSpec` were authored, then deleted as
  dead code during Party Review)
- `Booking.Tests/Commands/*.cs`, `Booking.Tests/Queries/*.cs`, `Booking.Tests/Validation/*.cs` — unit tests
  for every new handler/spec above
- `Library.Tests/Tools/ObjectIdJsonConverterTest.cs` — confirms the converter fires correctly nested inside
  `DataResponse<T>`
- `AgendaBuddy.IntegrationTests/Contract/BookingValidotStrictnessTest.cs`,
  `BookingErrorLeakageTest.cs` — T-101/T-102 regression tests (the latter gained a second test during Party
  Review, forcing the handled `Result.Fail`→`DataResponse<T>.Fail` path, not just the unhandled-exception one)
- `CHANGELOG.md` — did not exist before this feature; created with an `[Unreleased]` entry
- `docs/pdlc/design/api-refactor-pilot-booking/verification.md`, `validot-spike-findings.md`
- `docs/pdlc/mom/api-refactor-pilot-booking_*.md` — 4 wave-kickoff/design-roundtable minutes
- `docs/pdlc/reviews/BLAST-RADIUS_api-refactor-pilot-booking_2026-08-27.md`,
  `REVIEW_api-refactor-pilot-booking_2026-08-27.md`

---

## Files Modified

- `Booking/` → `Booking.Api/` (renamed: `Program.cs`, `Booking.csproj`→`Booking.Api.csproj`, `GlobalUsings.cs`,
  `Configuration/`, `Extensions/`, `Requests/AppointmentExtrasRequests.cs`, `appsettings*.json`,
  `Properties/launchSettings.json`, `Dockerfile`) — `Program.cs` and `ServiceCollectionExtension.cs` (DI
  registrations for `IProviderService`/`IBookingService`, added during Party Review) both changed
  substantially beyond the rename
- `Booking/Requests/RequestCollection.cs`, `IRequestCollection.cs` — **deleted** (Requirement 3)
- `Booking/Events/EventsHelper.cs`, `Booking.Tests/Events/EventsHelperTest.cs` — **deleted** (dead once
  `RequestCollection` was gone)
- `EventAndCommands/Commands/Booking/*.cs` — `Book`/`Update`/`Cancel`'s commands **deleted**;
  `BookingAppointmentCommandHandler.cs`/`CancelAppointmentCommandHandler.cs`/`UpdateAppointmentCommandHandler.cs`/
  `ChangeAppointmentStatusCommand(Handler).cs` **moved** into `Booking.Core`/`Booking.Domain`
- `EventsAndCommands.Tests/Commands/Booking/*HandlerTest.cs` — **deleted** (superseded by the moved
  handlers' own tests in `Booking.Tests`)
- `AgendaBuddy.IntegrationTests/Audit/EventStoreWriteGuardTest.cs` — `ScanRoots` gained `Booking.Core`
- `AgendaBuddy.IntegrationTests/Harness/{MobileClientRouteResolutionTest,PaymentsAndStatusTest,SessionNotesTest}.cs`,
  `Persistence/BookingPersistenceTest.cs` — envelope-shape parsing updated (root → `.data`); `PaymentsAndStatusTest`'s
  Update assertion strengthened again during Party Review, now that `agenda-buddy-2hd` is fixed
  (AC14 envelope-shape carve-out, not a discretionary test change)
- `docs/api/openapi/Booking.json` — regenerated (byte-deterministic, F-018-T16's mechanism), twice
  (project rename, then the 7 F-014 routes' schema changes)
- `agenda-buddy.sln`, `agenda-buddy-backend.slnf`, `.github/workflows/dotnet.yml`,
  `AgendaBuddy.AppHost/{AgendaBuddy.AppHost.csproj,AppHostWiring.cs}`,
  `AgendaBuddy.AppHost.Tests/SecurityScanAndDockerJobShapeTest.cs`, `scripts/{generate-openapi.sh,run-ios.sh}`,
  `Library.Tests/Security/{KeyMaterialHygieneTest,TransportSecurityOrderTest}.cs` — every place the
  `Booking`→`Booking.Api` rename cascaded (CI matrix/path filters, service arrays, hardcoded service lists),
  each checked directly rather than assumed unaffected
- `CLAUDE.md` — updated for the 4-project split, FluentResults/Validot/GuardClauses, and current test counts
- `docs/pdlc/design/api-refactor-pilot-booking/api-contracts.md` — corrected 4 times as implementation
  diverged from the original prediction (Requirement 7's DTO never built, Cancel/DeleteNote's 204 exception,
  Requirement 6's actual per-route state, twice)
- `docs/pdlc/memory/STATE.md`, `docs/pdlc/tasks/F-019/F-019-T01.md`…`T11.md`

---

## Test Summary

| Layer | Status | Passed | Failed | Skipped | Notes |
|-------|--------|--------|--------|---------|-------|
| Unit | pass | 516 | 0 | 0 | `agenda-buddy-backend.slnf`, 12 backend test projects |
| Integration | pass | 310 | 0 | 0 | Real MongoDB Testcontainer, all 7 services + Gateway |
| Mobile | pass | 158 | 0 | 7 | Untouched by this feature; 7 skips are the deliberate `AuthAcceptanceTests` live-Identity skip |
| E2E | skip | — | — | — | No E2E command exists in this project (same as every prior feature) |
| Performance | skip | — | — | — | No performance test command exists |
| Accessibility | skip | — | — | — | No accessibility check command exists; no UI surface in this feature |
| Visual Regression | skip | — | — | — | No visual regression command exists |

**Total: 516 + 310 + 165 = 991 tests, 0 failing.**

**Constitution gates:** All required gates passed. §7 security scan ran: dependency audit shows only the
pre-existing, ADR-030-dispositioned `SSH.NET` HIGH (nothing new — all 4 new `Booking.*` projects and all 4
new packages are clean); secret scan (`gitleaks detect --log-opts="main..HEAD"`, 12 commits) found no
leaks. `dotnet format agenda-buddy-backend.slnf --verify-no-changes` clean.

---

## Deployment Record

Not applicable — no deployment changes. This feature is a backend-internal refactor (project layering,
dispatch mechanism, response envelope); no new config, secrets, or infrastructure. Deploy/Verify/Reflect
happen at `/ship`, not here.

---

## Known Tradeoffs & Tech Debt Introduced

- **[TD — tracked as `agenda-buddy-02e`]** Update/Cancel routes still validate via `MiniValidator`, not
  Validot — Requirement 6 is 3/10 routes, not 10/10. Never assigned to any F-019 task.
- **[TD — tracked as `agenda-buddy-cy2`]** `POST /appointments` with a null `EmailProvider` surfaces as an
  unhandled 500 instead of a clean 400/404. Pre-existing, unchanged by this refactor; confirmed the wire
  response still leaks no exception detail regardless (T-102 holds even though the status code is wrong).
- **Mapster (ADR-049-approved) has zero call sites in this feature.** Requirement 7 (keeping
  `AppointmentEntity` out of route signatures via response DTOs) was never assigned to any task. Not a
  defect, but F-020 should not assume a Mapster usage pattern exists to copy from this feature.
- **`Booking.Api`'s own internal namespaces stayed `Booking.*`, not renamed to `Booking.Api.*`** for
  consistency with the other 3 new projects — judged disproportionate to any task's actual scope.

---

## Agent Team

**Always-on:**
- **Neo** (Architect) — architecture review and PRD conformance
- **Echo** (QA Engineer) — test strategy and coverage review
- **Phantom** (Security Reviewer) — OWASP and auth security review
- **Jarvis** (Tech Writer) — inline docs, API docs, episode draft

**Auto-selected for this feature:**
- **Bolt** (Backend Engineer) — API routes, business logic, handler moves — active across every wave
  kickoff/design roundtable (see `docs/pdlc/mom/api-refactor-pilot-booking_*.md`)

---

## Reflect Notes

_Filled during the Reflect sub-phase in `/ship`._

---

## Approval

**Reviewed by:** ogdevlabs (git-configured identity)
**Date approved:** —
**Notes:** Draft — self-approval expected at Ship's Reflect gate under this session's standing
full-autonomy grant, consistent with the Party Review's own self-approval (`REVIEW_api-refactor-pilot-booking_2026-08-27.md`).
