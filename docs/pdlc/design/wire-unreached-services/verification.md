# Verification — Wire Unreached Services (F-014)

**Date:** 2026-08-23 · **Branch:** `feat/F-014-wire-unreached-services`
**PRD:** [`PRD_F-014_wire-unreached-services_2026-08-23.md`](../../prds/PRD_F-014_wire-unreached-services_2026-08-23.md)

**Claim: every capability this product says it has can be reached, is owner-scoped, and reports what it
actually knows.**

---

## 1. Suites

| Suite | Command | Before | After |
|---|---|---|---|
| Backend unit | `dotnet test agenda-buddy-backend.slnf` | 431 | **452** (+21) |
| Integration | `dotnet test AgendaBuddy.IntegrationTests/…csproj` | 118 | **175** (+57) |
| Mobile | `…/MobileApp.Tests.csproj /p:MobileWorkloads=false` | 74 | **74**, untouched |
| **Total** | three commands | 623 | **701** |

0 failing, 0 warnings, `dotnet format --verify-no-changes` clean on both the slnf and the harness.
Integration duration **1 m 52 s** against the 600 s CI budget.

**The +57 integration tests are where this feature's claim actually lives.** Every one of the five services
had unit tests already and was unreachable anyway; a unit test on a service class is exactly what failed to
notice that for five features running.

---

## 2. Acceptance criteria

| AC | Criterion | Test | Verdict |
|---|---|---|---|
| 1 | A provider writes and reads back a note | `SessionNotesTest.AC1_TheOwningProvider_CanWriteAndReadBackANote` | ✅ |
| 2 | A message reaches the inbox and the shared thread | `MessagingAndNotificationsTest.AC2_…` | ✅ |
| 3 | Notifications are scoped to the caller | `AC3_TheNotificationListIsScopedToTheCaller` | ✅ |
| 4 | A provider reads their own report | `ReportAndDeactivationTest.AC4_AProviderReadsTheirOwnCounts` | ✅ |
| 5 | Deactivation dispatches and audits | `AC5_AProviderDeactivatesThemselves_AndTheCommandIsAudited` | ✅ |
| 6 | A payment is stored and readable | `PaymentsAndStatusTest.AC6_AC17_…`, `T205_*` | ✅ |
| 7 | Every capability resolves its repository and collection from configuration | Booking/Customer/Provider `ServiceCollectionMongoResolutionTest` | ✅ |
| 8 | **Every** new route refuses an anonymous caller | four `[Theory]` sweeps, 15 route/method pairs | ✅ |
| 9 | Someone else's note, message, notification, payment or report → 403 | `T201_*`, `T204_*`, `T205_AStrangerCanNeitherPayNorRead`, `AC9_AProviderCannotReadAnotherProvidersReport`, `T207_*` | ✅ |
| 10 | Customer-role → 403 on notes and the report | `AC10_ACustomerCannotTouchNotesAtAll`, `AC10_ACustomerCannotReadAReport` | ✅ |
| 11 | No list route returns anything but the caller's own | `AC11_T204_TheInboxContainsOnlyTheCallersMessages`, `AC3_…` | ✅ |
| 12 | Not-found and not-yours are indistinguishable | `T202_ANoteThatDoesNotExist_AndOneBelongingToSomebodyElse_AnswerIdentically`, `AMissingMessageId_…` | ✅ |
| 13 | The `PUT` ignores a client-asserted status | `AC13_T203_ThePutIgnoresAClientAssertedStatus` | ✅ |
| 14 | Transitions walk the graph; illegal ones answer 409 | `AC14_TheTransitionRouteWalksTheGraph…`, `AC14_AnIllegalTransitionAnswers409…` | ✅ |
| 15 | A booked appointment can be cancelled; a completed one cannot | `AC15_ABookedAppointmentCanBeCancelled_AndACompletedOneCannot` | ✅ |
| 16 | Completing is provider-only | `AC16_T203_ACustomerCannotCompleteTheirOwnAppointment` | ✅ |
| 17 | No key ⇒ the non-charging gateway, and no external call | `PaymentGatewaySelectionTest` (8 cases) + `AC6_AC17_…` | ✅ |
| 18 | The report publishes no revenue figure, and says why | `AC18_TheReportPublishesNoRevenueFigure_AndSaysWhy`, `ReportingServiceTest.GetProviderReportAsync_PublishesNoRevenueFigure_AndSaysWhy` | ✅ |
| 19 | Appending an appointment is a targeted write | `TargetedWriteShapeTest` (4 cases) | ✅ |

**AC-8 is asserted on 15 route/method pairs, not a sample.** A forgotten `RequireAuthorization()` is invisible
in review, and F-016 exists because five routes in this solution served PII to anonymous callers.

**Every scoping test plants a third party's records in the same database.** A route that returned nothing at
all would satisfy "the caller sees only their own" vacuously; the difference between asserting a filter and
asserting an empty collection is the whole value of AC-11.

---

## 3. Four defects found by running the software, none of them in the plan

This project's METRICS has recorded the same observation after every episode: *the real defects are found by
running the software, not by reviewing it.* Four more, all invisible to review and to unit tests:

### 3.1 🔴 `ObjectId` does not round-trip through JSON — and three route families need it to

The first notes test failed on `Assert.NotEqual(ObjectId.Empty, note.Id)`. `System.Text.Json` has no idea
what an `ObjectId` is, so it serialised the struct's public properties:

```json
"id": { "timestamp": 1787455661, "machine": 12345, "pid": 678, "increment": 90, "creationTime": "…" }
```

That cannot be read back into an `ObjectId` at all — it has no settable properties. **Three of F-014's route
families need the id a create response returned** (`PUT /notes/{id}`, `POST /messages/{id}/read`,
`POST /notifications/{id}/read`), so this was not cosmetic.

**Pre-existing, not introduced here.** Every route returning `ProviderEntity`, `CustomerEntity`,
`ServiceEntity` or `ProfessionEntity` has emitted the same unusable id since it was written. Nothing noticed
because the mobile client cannot reach them (F-015) and no test ever read an `id` back.

Fixed with `ObjectIdJsonConverter`, registered in the three services F-014 touches. **The other four are
filed, not changed** — altering their response shape is not this feature's business. Carried to F-015: a
client needs the same converter, or entities should declare `string Id` with
`[BsonRepresentation(BsonType.ObjectId)]` as `CredentialEntity` already does and need none.

### 3.2 🔴 `DeactivateProviderCommandHandler` could never have completed

Dispatching it for the first time returned 500. The handler called:

```csharp
await mediator.Publish(request, cancellationToken);   // request is IRequest<string>, NOT INotification
```

`Publish` has an `object` overload, so it compiles; at runtime MediatR throws. **The handler had a defect and
an absence of callers, and they arrived together** — nothing had ever dispatched it, so nothing had ever
failed. `DeactivateProviderEvent` existed for exactly this purpose with **zero references**. Every other
command handler publishes its event; this one lost a line to a copy-paste.

### 3.3 🟠 The API binds enums as integers, and a string 400s with no explanation

`AC13`'s first draft sent `appointmentStatus: "Completed"` and got a bare `400 Bad Request` with no
validation detail — model binding, not validation. No `JsonStringEnumConverter` is registered anywhere, so
every enum on this API is an integer on the wire.

Two consequences, both recorded rather than "fixed" by a sweeping change: the test now sends `2`, because
that is what a real client sends and a string would have made it pass for the wrong reason; and the new
status route deliberately takes a **string** and parses it, which also accepts the numeric form. `Enum.TryParse`
happily returns `true` for undefined numbers — `TryParse<AppointmentStatus>("99")` yields `99` — so
`Enum.IsDefined` is what makes that a 400 rather than a 409 implying the state exists.

### 3.4 🟠 A flaky telemetry test, made likelier by this feature

`TelemetryPiiTest.ExportedSpan_IdentifiesTheEndpointByRouteTemplate` failed on roughly **one run in three**,
with its expected span simply absent, and passed when run alone. OpenTelemetry's ASP.NET Core instrumentation
is process-wide, and two `TracerProvider`s alive at once do not reliably each receive every activity.

`AgendaBuddy.ServiceDefaults.Tests` had **one** server-starting class for years, so the problem could not
appear. F-021 added a second and F-014's full-suite runs made the overlap frequent. Fixed with an xUnit
collection that disables parallelism — the same mechanism F-016 chose for the integration harness. **Six
consecutive green full-suite runs** afterwards.

Note this is the *second* flake in the same file this month: F-021 fixed its sibling
(`RedactionPreservesThePathShape` selecting the wrong span). Both had the same root cause — a test written
when it was the only one of its kind in the assembly.

---

## 4. What this feature does not claim

1. **One pre-existing test was replaced.** `ReportingServiceTest.GetProviderReportAsync_CalculatesEstimatedRevenue`
   asserted that one completed appointment against a single 50 service produced revenue of 50. It passed, and
   the behaviour was wrong — the formula multiplied completed appointments by the *whole catalogue*, which is
   correct only in the single-service case the test happened to use. **Needs maintainer acknowledgement**, as
   F-016's ADR-025 and F-021's ADR-034 did.
2. **Revenue is not computed, and cannot be.** `AppointmentEntity` records no service, no fee and no amount.
   `revenueAvailable: false` with a reason is the honest answer; the fix is a data-model change that F-015's
   contract and F-025's rules both touch. Filed.
3. **The payment amount is unvalidated** (threat T-205(c), accepted). There is nothing to validate it against
   for the same reason revenue cannot be computed. With the recording gateway a wrong amount corrupts a
   record; **with a real Stripe key it would be a real underpayment.** Anyone configuring
   `Payments:Stripe:ApiKey` must read T-205 first.
4. **Nothing writes a notification.** No domain event calls `SendAsync`, so `GET /api/v1/notifications`
   returns `[]` until something does — asserted as an expectation so it is not read as a bug. Requirement 19:
   storage without delivery, and for now without production either. **F-022's recorded dependency on
   `NotificationService` is not yet satisfied**: "send" still does not mean "deliver".
5. **The two status writes are not atomic together.** The `appointments` document and the provider's embedded
   copy are separate documents in separate collections, and this deployment has no replica set and therefore
   no transaction. A fault between them leaves the embedded copy stale; re-issuing the same transition repairs
   it. Recorded in the handler, not hidden.
6. **`MarkReadAsync` still read-modify-writes.** `MessageService` and `NotificationService` load the document,
   set a boolean and replace it. Requirement 20 forbids *new* whole-document replacements on
   `ProviderEntity`; these are single-owner documents where a lost update means a message shows unread.
   Rewriting them would mean editing `Library` services this feature is only *wiring* — and the moment F-014
   edits service internals, its claim ("these work as written, they were merely unreachable") stops being
   verifiable.
7. **No indexes.** Every new query is an equality match that would benefit from one, and **no application code
   in this repository creates an index on any collection** (`agenda-buddy-b0w`, observed on a live database at
   F-021's ship gate). Adding four here would mean inventing an indexing story inside a wiring feature.
8. **No Kafka publishing for messages.** F-007 built per-provider topics; `MessageService` does not use them
   and this feature does not change that.
9. **Slot correctness is F-025.** `Start < End`, future-dating and overlap. Split at Discover because it needs
   its own concurrency design, and filed as `agenda-buddy-ohw`.
10. **Deactivation writes a provider document — including its embedded appointments and therefore its
    customers' email addresses — into the `events` collection.** Unchanged from how every command handler
    audits (ADR-027 kept command payloads; F-016-T18 only reduced *query* payloads), so F-014 does not
    diverge. But F-014 is what makes this handler reachable, so it is what makes the PII land. **F-024.**
11. **The generated OpenAPI specs were not regenerated.** They should be, and the handlers return
    `Results<…>` so the specs will under-report these routes anyway (F-018's T16/T17 own spec drift). Nine new
    routes is the largest spec drift this project has accumulated in one feature — worth doing before F-015
    reads them.

---

## 5. Security scan (CONSTITUTION §7 — always required)

Run by hand, for the **fourth** consecutive feature. **F-017 still owns automating it.**

- **Dependency audit** — unchanged: one vulnerable package solution-wide, `SSH.NET` HIGH in
  `AgendaBuddy.IntegrationTests` only, dispositioned by ADR-030. **F-014 adds no package reference to any
  project.**
- **Secret scan** — clean. The one new secret-shaped thing is `Payments:Stripe:ApiKey`, which is deliberately
  absent from every `appsettings.json` and is documented as an Aspire secret parameter (threat T-206). The
  existing `KeyMaterialHygieneTest` scans tracked files for PEM payloads and would fail on a committed key.
- **New attack surface reviewed** — nine routes, all authenticated, all ownership-guarded, two role-gated. The
  posture is asserted by 15 anonymous-access cases and by every scoping test planting a third party's data.

---

## 6. What a reviewer should look at first

1. **`Booking/Program.cs`'s notes routes** — the provider email comes from the `sub` claim and `NoteRequest`
   has no field for one. If a `providerEmail` parameter ever appears on those routes, threat T-201 is back and
   it is the most sensitive data in the product.
2. **`ChangeAppointmentStatusCommandHandler`** — the transition must keep going through
   `AppointmentEntity.TransitionTo`. A transition table in the handler would put the rules in two places, which
   is how the `AssertOwner`/`AssertOwnerAny` asymmetry F-021 fixed came about.
3. **`UpdateAppointmentCommandHandler`'s missing line** — the deleted
   `appointment.AppointmentStatus = appointmentEntity.AppointmentStatus` is the fix. Restoring it, for any
   reason, reopens threat T-203.
4. **`PaymentGatewayFactory.ModeFor`** — the only thing standing between this repository and a live payment
   credential.
