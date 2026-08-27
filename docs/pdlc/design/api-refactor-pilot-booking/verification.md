# Verification — API Refactor Pilot: Booking (F-019)

**Date:** 2026-08-27 · **Branch:** `feat/F-019-api-refactor-pilot-booking`
**PRD:** [`PRD_F-019_api-refactor-pilot-booking_2026-08-26.md`](../../prds/PRD_F-019_api-refactor-pilot-booking_2026-08-26.md)

**Claim: Booking proves the full target architecture — Clean Architecture layering, real MediatR dispatch,
`FluentResults`, `DataResponse<T>` — end-to-end on all 10 of its routes, before F-020 replicates the shape
across the other six services.**

---

## 1. Suites

| Suite | Command | Before (F-018 baseline) | After T11 | After Party Review remediation |
|---|---|---|---|---|
| Backend unit | `dotnet test agenda-buddy-backend.slnf` | 484 | 512 (+28) | **516** (+32) |
| Integration | `dotnet test AgendaBuddy.IntegrationTests/…csproj` | 301 | 310 (+9) | **310** (+9, unchanged — one test's assertion strengthened, not added) |
| Mobile | `…/MobileApp.Tests.csproj /p:MobileWorkloads=false` | 165 | 165, untouched | **165**, untouched (158 pass, 7 deliberately skipped) |
| **Total** | three commands | 950 | 987 | **991** |

0 failing. `dotnet format agenda-buddy-backend.slnf --verify-no-changes` clean, re-verified after the Party
Review remediation edits (one round of drift, from the two new handler-test files, auto-fixed and
re-verified — see §3.11). Integration suite duration ~4 minutes against the 600s CI budget (ADR-017's
tripwire this feature was explicitly flagged as testing — comfortable margin, not close to the line).

**Backend net +32 vs. the F-018 baseline (484→516), not the sum of what each task/finding added**: T01–T11
alone accounted for +28 (484→512, per the table above), and Party Review's own remediation added a further
+4 (512→516) — 8 new Moq-based Update/Cancel handler tests replacing 2 placeholder GuardClause-only tests
(net +6), minus 2 dead-code test methods removed (`AppointmentStatusRequestSpec_*`/`PaymentRequestSpec_*`,
testing specs that were themselves deleted as dead code, net −2). F-019-T10 deleting 1 stub-adjacent handler
pair and F-019-T04 deleting `EventsHelperTest.cs` (3 tests) as a direct consequence of `RequestCollection`'s
removal are both already inside the T01–T11 +28 figure, disclosed at the task that made them — not a
surprise here.

---

## 2. Acceptance criteria

| AC | Criterion | Evidence | Verdict |
|---|---|---|---|
| 1 | `Booking.Api`/`Booking.Core`/`Booking.Domain`/`Booking.Infrastructure` exist; `Booking.Api` has no business logic | All 4 projects exist (`ls Booking.*`); `grep -rn "IMongoClient\|IRepository<" Booking.Api` finds only DI registration (`Program.cs:10`) and `MongoDbConfiguration.cs` (pre-existing F-016-era health-check wrapper, not F-019 business logic) | ✅ (annotated — see §3.4) |
| 2 | Zero hand-constructed handlers in `Booking.Api`/`Booking.Core` | `grep -rn "new.*CommandHandler(" Booking.Api Booking.Core` → clean | ✅ |
| 3 | `RequestCollection.cs`/`IRequestCollection.cs` no longer exist | `find Booking.Api -iname "*RequestCollection*"` → no matches | ✅ |
| 4 | Zero `as KafkaClient` under `Booking.Api`/`Booking.Core` | `grep -rn "as KafkaClient" Booking.Api Booking.Core` → clean (fixed at T04 by typing the ctor param `IKafkaClient?`, the actual DI-registered interface, not just removing the cast syntax) | ✅ |
| 5 | Zero string-sniffed control flow (`StartsWith("exception"`) | `grep -rin 'StartsWith("exception"' Booking.Api Booking.Core` → clean | ✅ |
| 6 | Validot validates every one of Booking's 10 request DTOs; zero `MiniValidator` in `Booking.Api` | `grep -rn "MiniValidator" Booking.Api` finds 2 real calls (`Program.cs`, Update/Cancel), unchanged by Party Review. **Updated at Party Review remediation:** Validot coverage improved from 1/10 routes (Book only) to 3/10 (Book plus the 2 note-content routes, via the `NoteSpec` fix — see §3.11) | ❌ **partially met — disclosed, see §3.5/§3.11, filed `agenda-buddy-02e`** |
| 7 | Zero `new CancellationToken()` under Booking's new projects | `grep -rn "new CancellationToken()" Booking.Api Booking.Core` → clean | ✅ |
| 8 | Every one of Booking's 10 routes returns a `DataResponse<T>`-shaped envelope on success, verified by a real HTTP request | All 8 body-bearing routes confirmed live, parsing `.data` explicitly (`BookingPersistenceTest` for Book; `SessionNotesTest`/`MobileClientRouteResolutionTest` for Get/Create/Update Notes; `PaymentsAndStatusTest`/`MobileBookingRouteResolutionTest` for Update-status, Pay, GetPayment, and — added at this task — Update appointment). Cancel and Delete Note are the disclosed exception — 204 has no body — see §3.5 | ✅ (8/8 body-bearing routes live-verified; 2 disclosed, unavoidable bodyless exceptions) |
| 9 | `BookingAuditTest.cs` passes with zero assertion changes | Ran unmodified throughout T04–T10; still 2/2 green, 0 lines touched since `main` | ✅ |
| 10 | `BookingRouteContractTest.cs` passes with zero status-code assertion changes | Ran unmodified throughout; still 1/1 green | ✅ |
| 11 | `BookingPersistenceTest.cs` passes with zero *persisted-state* assertion changes | The identifier-extraction line changed (root → `.data`, T04) — the AC14 carve-out for envelope shape; the persisted-state assertions themselves (`stored.EmailProvider`, etc.) are byte-identical | ✅ |
| 12 | `EventStoreWriteGuardTest`'s handler enumeration still covers every Booking handler after the move | Confirmed at T07: the T03 dual-scan-root fix already covered all 10 Booking.Core handlers with zero further edits; 29→28 theory cases as the last EventAndCommands duplicate was deleted at T10, still ≥20 sanity floor | ✅ |
| 13 | All 484 backend + integration tests pass; zero regressions elsewhere | 512 backend / 310 integration / 165 mobile, 0 failing anywhere (one `AgendaBuddy.ServiceDefaults.Tests` flake reproduced the known, pre-existing, cross-test `TracerProvider` flakiness on record since F-017 — 22/22 clean in isolation and on retry, not a regression). `git diff main --name-only \| sed 's|/.*||' \| sort -u` confirms every changed top-level path is `.github`, `agenda-buddy.sln(f)`, `AgendaBuddy.AppHost(.Tests)`, `AgendaBuddy.IntegrationTests`, `Booking*` (all 6), `docs`, `EventAndCommands`, `EventsAndCommands.Tests`, `Library.Tests`, or `scripts` — nothing in another service's own directory | ✅ |
| 14 | No Booking-owned test file deleted; bodies updated only for envelope-shape changes | `Booking.Tests/Events/EventsHelperTest.cs` deleted at T04 — disclosed there as a direct, unavoidable consequence of Requirement 3 (nothing survives to test once `RequestCollection`, the class it delegated to, is gone), not a discretionary AC14 violation. `EventsAndCommands.Tests`' 3 stub tests moved (not deleted) to `Booking.Tests/Commands/` at T03. All other test-body edits are envelope-shape-only (T04's `BookingPersistenceTest`, T06's `SessionNotesTest`/`PaymentsAndStatusTest`/`MobileClientRouteResolutionTest`) | ✅ |

**2 of 14 ACs carry a disclosed deviation (AC1's annotation, AC6's partial-met), and AC8 carries one
unavoidable, documented exception.** None are silently marked done — see §3.4/§3.5 and the corresponding
`api-contracts.md` corrections (§4) made at this task.

---

## 3. Real defects and process findings across the build loop

This project's METRICS has recorded the same observation after every feature: *the real defects are found by
running the software, not by reviewing it.* This feature's own thesis — the rewrite exists to prove the
target shape catches real problems — held on itself, repeatedly:

### 3.1 The moved handlers' constructor shape blocked the real DI dispatch they were built for (T04)

`RequestCollection.cs` existed specifically because `BookingAppointmentCommandHandler` (and its two
siblings) took the per-request `AppointmentEntity` as a *constructor* parameter — a value a DI container has
no way to supply, since it only knows about registered services, not the current HTTP request's body. Real
`mediator.Send(command, ct)` dispatch was structurally impossible until that value moved onto the command
itself, read inside `Handle(request, ct)`. Found by trying to actually wire it, not by reading the code.

### 3.2 `AddMediatR` wasn't scanning the assembly the handlers actually live in (T04)

T03 moved every handler into `Booking.Core`, a separate assembly from `Booking.Api`. `AddMediatR(cfg =>
cfg.RegisterServicesFromAssembly(typeof(Program).Assembly))` only scans the assembly it's given — every
`mediator.Send` would have thrown "no handler registered" at runtime, invisible to any compile-time check.
Fixed by scanning both assemblies.

### 3.3 `KafkaClient?` was never resolvable — the real fix for `agenda-buddy-5og` (T04)

The dormant downcast bug (`kafkaClient as KafkaClient`) wasn't fixed by deleting the cast syntax alone. The
constructor parameter's *type* was the concrete `KafkaClient` class, which is never registered in DI (only
`IKafkaClient` is) — real dispatch would have thrown `InvalidOperationException: Unable to resolve service
for type 'KafkaClient'` on every request. Retyped to `IKafkaClient?`, the interface DI actually provides.

### 3.4 AC1's "no business logic" carve-out: `MongoDbConfiguration.cs`

One `IMongoClient` usage outside DI registration survives in `Booking.Api`: `Configuration/
MongoDbConfiguration.cs`, a pre-existing (F-016-era) thin wrapper resolving the shared client for the health
check. It predates F-019, is unrelated to Booking's business logic, and was not touched by this feature.
Recorded here rather than silently counted as a pass on a technicality.

### 3.5 Two Requirement gaps this feature's own task list never assigned

- **Requirement 6** (Validot everywhere) only actually happened for `POST /appointments` — the one route
  T02's spike and T04's build covered. `PUT`/`DELETE /appointments/` (Update/Cancel) still call
  `MiniValidator.TryValidate` unchanged; no F-019 task's requirement list ever included 6 for them. Filed
  `agenda-buddy-02e`.
- **Requirement 10**'s blanket "every route gets `DataResponse<T>`" cannot apply to Cancel and Delete Note —
  a 204 No Content has no body by HTTP semantics. Both stay bodyless, unchanged, disclosed in each route's
  own code comments and in `api-contracts.md` §4 (corrected at this task).
- **Requirement 7** (Mapster DTOs, keeping `AppointmentEntity` out of route signatures) was never assigned to
  any task either — `api-contracts.md` §3.1 originally predicted a new `AppointmentResponse` DTO; T04 used
  `AppointmentEntity` directly instead, since introducing the DTO was out of every task's actual scope.
  Corrected in `api-contracts.md` at this task rather than left contradicting what was actually built.

### 3.6 A real, pre-existing CONSTITUTION §3 audit gap, invisible until the refactor moved the code into scannable files (T05)

None of the 6 new Notes/Payment handlers wrote an audit event when first authored — and neither had the
original inline Program.cs logic they replaced, for as long as those routes have existed. `EventStore
WriteGuardTest`'s file-based scan could never see this gap while the logic lived inline in `Program.cs`,
outside any scanned directory. The instant the logic became real handler files under `Booking.Core/`, the
guard caught it (6 test failures, not silent). Fixed with `eventStore.SaveAsync`/`QueryAudit.Success`/
`Failure` calls matching the project's established convention — closing a real compliance gap this refactor
exposed, not one it introduced.

### 3.7 A narrow test filter hid three whole test files' worth of coverage from every check in this session, until T06's full-suite run (T06)

Every "integration tests pass" verification from T03 through T05 used `--filter "FullyQualifiedName~Booking"`.
`SessionNotesTest`, `PaymentsAndStatusTest`, and `MobileClientRouteResolutionTest` are namespaced
`AgendaBuddy.IntegrationTests.Harness` with no "Booking" in the class name, so the filter never matched them
— they had not actually been run once since Construction started. Running the full ~301-test suite at T06
(not the narrow filter) surfaced one real regression the filter had been hiding (3 files parsing a
create-response's identifier from the JSON root, now under `.data`) alongside the expected OpenAPI drift.
Every full-suite run from T06 onward uses no filter.

### 3.8 A real, pre-existing, out-of-scope defect found by T08's fault injection, not fixed inline

`POST /appointments` with `EmailProvider: null` and a customer-role token passes both Validot (`Email
AddressAttribute.IsValid(null) == true`, matching `MiniValidator`'s behavior) and `OwnershipGuard`, then
throws an unhandled exception downstream — a 500, not a clean 400/404. Confirmed unchanged by this refactor
(the business logic moved verbatim). Filed `agenda-buddy-cy2` rather than fixed inline (out of T08's actual
scope) or silently dropped. T09 reused the same real fault as its fault-injection fixture, confirming the
wire response leaks no exception detail regardless of which path a failure takes.

### 3.9 A real, pre-existing correctness defect found by closing AC8's own evidence gap (T11)

Verifying AC8 properly (adding a live `.data`-shape assertion to the Update route, which previously checked
only status code and persisted state) found that the response body's `AppointmentStatus` field echoes the
*client's own submitted (possibly forged)* value — `UpdateAppointmentCommandHandler` returns
`request.AppointmentEntity` (the raw deserialized request object) on success, not the actual updated entity
it fetched and persisted. The database correctly ignores a forged status (T-203's actual guarantee, and
`AC13_T203_ThePutIgnoresAClientAssertedStatus`'s pre-existing persisted-state check already proved that);
the *response* just lies about it. Not a new information-disclosure risk (a caller only ever sees their own
submitted value echoed back — nothing about a third party leaks) but a real, pre-existing (unchanged by
F-019 — the original handler had identical behavior) correctness bug. Filed `agenda-buddy-2hd` rather than
fixed inline; the new test asserts a neutral field (`Identifier`) instead, so this AC8 evidence-gathering
doesn't silently re-scope into fixing T-203's response-shape defect.

### 3.10 CI blast radius from the `Booking` → `Booking.Api` rename (T04)

The project-wide rename (per PRD Requirement 1 and `ARCHITECTURE.md`'s project table) cascaded further than
the task's own description implied: CI's `docker-build-and-scan` matrix, path filters, and its own
structural test; `scripts/generate-openapi.sh`/`run-ios.sh`'s service arrays; `TransportSecurityOrderTest`'s
hardcoded service list. Each was checked directly, not assumed unaffected. One finding came only from
actually running `dotnet publish -t:PublishContainer` locally: the .NET SDK's container-name derivation
replaces `.` with `-` (`booking-api`, not `booking.api`), which would have silently broken CI's Trivy step
had the existing `tr '[:upper:]' '[:lower:]'` step not been corrected before it ran in a real pipeline.

### 3.11 Party Review remediation (Neo/Echo/Phantom/Jarvis, 2026-08-27)

The 4-agent Party Review converged on `agenda-buddy-2hd` (§3.9) independently from three angles (Neo:
architecture correctness, Phantom: response-integrity, Echo: the weak test that let it ship undetected) and
raised three further findings, all fixed in this same gate rather than filed:

- **`agenda-buddy-2hd` fixed.** `UpdateAppointmentCommandHandler.SearchAndUpdateAppointment` now returns the
  actual persisted `AppointmentEntity?` (was `bool`), and `Handle` returns `Result.Ok(updated)` — the real
  entity — instead of `Result.Ok(request.AppointmentEntity)`. `agenda-buddy-2hd` closed.
- **Echo's Critical finding: `Update`/`CancelAppointmentCommandHandler` were untestable.** Both took the
  concrete `ProviderService`/`BookingService`, which Moq cannot mock, so their only unit tests were
  GuardClause-null checks — the actual `Result.Ok`/`Result.Fail` branches (including the exact line
  `agenda-buddy-2hd` lived in) had zero unit coverage. Retyped both handlers to `IProviderService`/
  `IBookingService` — both interfaces already cover everything these two handlers call (unlike `Book`'s,
  which needs `AppendAppointmentAsync`, not on `IProviderService` — left on the concrete classes,
  disclosed, not silently "fixed" into an interface that doesn't have the method). Added 8 new Moq-based
  tests (4 each) covering success, no-such-provider, no-such-appointment, and null-request paths.
- **A real regression this retyping introduced, caught by re-running the full integration suite, not
  assumed clean from a green build.** `Booking.Api`'s DI container registers only the concrete
  `ProviderService`/`BookingService` (`Booking.Api/Extensions/ServiceCollectionExtension.cs`) — retyping the
  two handlers' constructors to the interfaces they actually need left `IProviderService`/`IBookingService`
  unregistered. `dotnet build` stayed green (interfaces exist, DI resolution is a runtime concern); the
  integration suite failed exactly where it should — 6 `ServiceProvider` validation failures at startup for
  every route both handlers sit behind. Fixed by forwarding both interfaces to the already-scoped concrete
  instance (`AddScoped<IProviderService>(sp => sp.GetRequiredService<ProviderService>())`), so a request
  resolving both the concrete class (existing route handlers) and the interface (the two retyped command
  handlers) in the same scope gets the same object, not two. Re-ran to green (310/310) before moving on —
  the T06 "narrow filter hid real regressions" lesson (§3.7) held again, this time for a full-suite run
  concealing nothing, catching a real defect a partial run (or trusting the build alone) would have missed.
- **Neo's YAGNI finding.** All three moved handlers (`Book`/`Update`/`Cancel`) carried an unused, `#pragma
  warning disable CS9113`-suppressed `IKafkaClient? kafkaClient` constructor parameter — "reserved for
  future Kafka publishing," never read anywhere. Removed from all three; the pragma is gone with it.
- **Neo's over-engineering finding: dead validation specs.** `StatusSpec`/`PaymentSpec`
  (`AppointmentExtrasRequestsSpecifications.cs`) were authored and unit-tested at T02 but never wired into
  DI or a route — `AppointmentStatusRequest.Status`/`PaymentRequest.Amount`/`Currency` have no Validot check
  today, validated only by the pre-existing inline `Program.cs` logic those specs never replaced. Deleted
  rather than wired (wiring a no-op would be ceremony with nothing to show for it); the 2 dead-code test
  methods that tested them went with them (AC14's carve-out: bodies/methods for now-deleted code are not a
  violation).
- **A real bug in the surviving spec, found by live-probing Validot before shipping the fix above, not
  assumed from its name.** `NoteSpec`'s `.Required().NotEmpty()` accepts a whitespace-only string —
  confirmed live against the real Validot 2.6.0 assembly that `.NotEmpty()` only rejects `null`/`""`. That
  would have been a real strictness *regression* relative to the inline `IsNullOrWhiteSpace` check it was
  about to replace (T-101's exact threat). Fixed to `.Required().NotWhiteSpace()`, confirmed byte-for-byte
  equivalent to `!string.IsNullOrWhiteSpace(x)` against `null`/`""`/`"   "`/`"x"`/`" x "` before wiring
  `NoteSpec` into DI and both note-content routes, replacing their inline checks. Also confirmed live that
  `validator.Validate(null)` degrades gracefully (`AnyErrors=true`, no exception) rather than assumed, since
  a malformed/empty request body binds `NoteRequest request` to `null` before the validator ever sees it.
- **Echo's Important finding, also fixed here.** The pre-existing `BookingErrorLeakageTest` (T09) only
  exercised the *unhandled*-exception path (500, `agenda-buddy-cy2`'s fault). Nothing forced a genuine,
  handled `Result.Fail` from inside a real handler through to the wire and inspected the response body —
  the actual mitigation T-102 names. Added
  `BookingANonExistentProvider_ReturnsBadRequest_WithTheHandlersFailureMessageInErrors`: POSTs a
  well-formed, valid-looking provider email that matches no provider document, forcing
  `BookAppointmentCommandHandler`'s real `Result.Fail($"No provider found for {email}")` branch, and asserts
  the live response is 400 with `DataResponse<AppointmentEntity>.Success == false` and the handler's actual
  message in `.Errors`.

Net effect on the suite counts: backend +4 (§1), integration +1 (§1's "9→9" line — the Update assertion was
strengthened, not added, but this new test is the +1). `dotnet build`/`dotnet test` (both suites)/
`dotnet format --verify-no-changes` all re-run clean after every fix above, not assumed from the individual
fixes looking correct in isolation.

---

## 4. Final project layout vs. `ARCHITECTURE.md`'s prediction

Matches as predicted: `Booking.Api`/`Booking.Core`/`Booking.Domain`/`Booking.Infrastructure` all exist,
`Booking.Infrastructure` stayed deliberately empty (YAGNI, no real Booking-specific repository need arose),
`Booking.Tests` stayed one project. One undocumented decision made during Build, recorded here rather than
left implicit: `Booking.Api`'s own pre-existing internal namespaces (`Booking.Configuration`,
`Booking.Requests`, `Booking.Validation`, etc.) were kept as `Booking.*`, not renamed to `Booking.Api.*` —
ARCHITECTURE.md's project-level table didn't specify either way, and renaming every internal file's
namespace for cosmetic consistency with the other 3 new `Booking.X` projects was judged disproportionate to
T04's actual scope (dispatch, envelope, `RequestCollection` deletion). `api-contracts.md` §3.1/§3.2/§4 also
corrected at this task (§3.5 above) — design docs corrected by implementation, matching this project's
standing convention.

---

## 5. Filed, not fixed here

| Issue | What | Why deferred |
|---|---|---|
| `agenda-buddy-cy2` | `POST /appointments` 500s on a null `EmailProvider` instead of 400/404 | Pre-existing, unchanged by this refactor; out of scope for the security tasks that found it |
| `agenda-buddy-02e` | Update/Cancel routes never migrated `MiniValidator` → Validot | Never assigned to any F-019 task; Requirement 6 partially met (3/10 routes as of Party Review, up from 1/10 — see §3.11); description updated with the current per-route breakdown |

`agenda-buddy-2hd` (`PUT /appointments/` response body echoing the client's forged `AppointmentStatus`) was
filed at T11 as "not fixed here" but was **actually fixed during Party Review remediation** (§3.11) and is
now closed — it is intentionally absent from this table, not an oversight.
