# Discover — wire-unreached-services (F-014)

**Date:** 2026-08-23 · **Feature ID:** F-014 · **Slug:** `wire-unreached-services`
**Phase:** Inception / Discover · **Status:** Complete

> ⚠️ **Ran in `solo` mode** — one model reasoning as each role, because this session carries a standing
> instruction not to spawn agents, which overrides STATE's `Party Mode: agent-teams`. Same condition as
> every F-016 and F-021 meeting. Fidelity is lower than independent context windows; findings should be
> read with that in mind. Recorded rather than glossed.

---

## Why this Discover exists

This project has had **two Discover premises collapse on inspection** (the MAUI-workload concern and the
OTLP-suppression inference were both withdrawn as wrong), so the rule since the platform-remediation
program is: verify every premise against the code before writing a requirement. Every claim below was
checked. **Five held, and three things nobody had recorded turned up — one of which changes what this
feature has to contain.**

---

## 1. The recorded premises, verified

### P-1 — Five `Library` services are unreachable ✅ **HOLDS, and the number is exact**

`grep` for each service name across every `.cs` file, excluding its own definition and the test projects:

| Service | Non-test, non-definition references |
|---|---|
| `MessageService` | **0** |
| `NoteService` | **0** |
| `PaymentService` | **0** |
| `ReportingService` | **0** |
| `NotificationService` | **0** — the 11 apparent hits are all `MobileApp`'s own client-side `PushNotificationService`, a different class |

All five have implementations and unit tests (`Library.Tests/Services/*ServiceTest.cs`). None is registered
in any `ServiceCollectionExtension`, none has a configured collection, and no route reaches any of them.

### P-2 — `DeactivateProviderCommand` is undispatched ✅ **HOLDS**

`EventAndCommands/Commands/Provider/DeactivateProviderCommand.cs` +
`DeactivateProviderCommandHandler.cs` + `Events/Provider/DeactivateProviderEvent.cs` all exist. The only
other reference in the repository is its own unit test. No route, and no `RequestCollection` method,
dispatches it.

### P-3 — No collection-name configuration ✅ **HOLDS**

`NotificationsCollection`, `MessagesCollection`, `NotesCollection` and `PaymentsCollection` appear in **no**
`.json` and no `.cs` file. The six domain services configure exactly five collections
(`ProvidersCollection`, `ServicesCollection`, `EventsCollection`, `CustomersCollection`,
`AppointmentsCollection`); Identity configures one (`CollectionName: credentials`).

### P-4 — `IPaymentGateway` is not DI-registerable ✅ **HOLDS, and it is worse than "not registerable"**

`StripePaymentGateway(string apiKey)` takes a raw string, and no Stripe configuration section exists
anywhere in the repository. Two further things the premise did not say:

- **It sets `StripeConfiguration.ApiKey` on every call** (`StripePaymentGateway.cs:11`). That is a
  **process-global static**, mutated per request. With one API key it is merely ugly; it also means the
  gateway is not safely usable from two differently-configured consumers in one process, and it makes the
  key's lifetime the process's rather than the call's.
- **The key is a live payment credential.** This project's standing lesson (`ISSUE-002`) is that a secret
  committed once is permanent. Whatever configuration shape F-014 chooses has to keep it out of
  `appsettings.json` and out of git — which points at the same Aspire secret-parameter mechanism the JWT
  keys already use.

### P-5 — Appointments can be double-booked ✅ **HOLDS** — *and is split out, see §3*

`BookingService.BookAppointmentAsync` is a bare `InsertAsync`. `AppointmentEntity` carries no validation
attribute on `Start` or `End`. There is no `Start < End` check, no future-dating check, and no overlap
check anywhere in the booking path.

---

## 2. What Discover found that nobody had recorded

### F-1 — 🔴 **`ReportingService` would report zeros forever.** This changes F-014's scope.

`ReportingService.GetProviderReportAsync` derives its two headline numbers from appointment status:

```csharp
var completed = appointments.Where(a => a.AppointmentStatus == AppointmentStatus.Completed).ToList();
var booked    = appointments.Where(a => a.AppointmentStatus == AppointmentStatus.Booked).ToList();
...
var estimatedRevenue = completed.Count * totalServiceFee;
```

Nothing in production ever sets `Completed`. So wiring this service as it stands would ship a provider
dashboard that reports:

| Field | Value it would always show |
|---|---|
| `CompletedAppointments` | **0** |
| `EstimatedRevenue` | **0** |
| `CancelledAppointments` | **0** — the arithmetic subtracts every other bucket from the total, and with everything in `Requested` the remainder is zero |
| `TotalBookings`, `UniqueCustomers`, `RetentionRate` | correct |

**That is the same defect class F-014 exists to fix, reproduced inside F-014's own delivery.** F-006–F-010
are marked `Shipped` on code nothing can call; wiring reporting without addressing status would mark F-009
reachable while it reports a number that is structurally zero. Worse than leaving it unwired, because an
unreachable endpoint is obviously broken and a dashboard reading £0 looks like a business fact.

### F-2 — 🔴 **Appointment status is client-asserted and unguarded.** `Book()`/`Complete()` are dead code.

`AppointmentEntity` has the transition rules:

```csharp
public void Book()      { if (AppointmentStatus == Requested) AppointmentStatus = Booked;    else throw; }
public void Complete()  { if (AppointmentStatus == Booked)    AppointmentStatus = Completed; else throw; }
```

**Neither is called anywhere in production** — only in `Booking.Tests/Lifecycle/AppointmentLifecycleTest.cs`.
What actually happens is `UpdateAppointmentCommandHandler.cs:51`:

```csharp
appointment.AppointmentStatus = appointmentEntity.AppointmentStatus;   // whatever the client sent
```

`AppointmentStatus` is a public settable property on the entity the `PUT` route binds from the request
body, so **a client can assign any status at any time**, including `Completed` on a brand-new appointment.
The two methods that encode the rules cannot run, and the field they protect is caller-owned.

**This is not hypothetical.** `MobileApp/Views/AppointmentDetailPage.xaml.cs:93` already calls
`ExecuteStatusUpdateAsync(AppointmentStatus.Completed)` — the client was written to drive status exactly
this way. It cannot reach the backend yet (F-015), which is the only reason this has not mattered.

So F-004's roadmap claim — *"book, confirm, update, cancel, and complete; with status transitions and
validation rules enforced end-to-end"* — is false in both halves: the validation rules do not exist (P-5)
and the transitions are unenforced (F-2).

### F-3 — 🟠 **A latent inversion in cancellation that activates the moment transitions become real**

`CancelAppointmentCommandHandler`:

```csharp
if (appointment.AppointmentStatus == AppointmentStatus.Booked)    return false;   // refuses BOOKED
if (appointment.AppointmentStatus == AppointmentStatus.Completed) return false;   // refuses COMPLETED
```

Refusing to cancel a **completed** appointment is right. Refusing to cancel a **booked** one is backwards —
a booked appointment is exactly what a customer needs to be able to cancel. Today this is invisible,
because nothing ever sets `Booked`; **fixing F-2 makes it live**, and the symptom would be "customers can
no longer cancel their appointments", appearing in the same release as the status fix and looking like its
fault. Recorded here so it is fixed in the same change rather than discovered as a regression.

Two smaller observations in the same handler: `AppointmentStatus.Cancelled` exists in the enum and is
**never assigned** — cancellation hard-deletes from `appointments` *and* removes the embedded copy from the
provider document — and `AppointmentStatus.Confirmed` is assigned in exactly one place
(`CalendarService.cs:30`), on a projection rather than a persisted appointment.

### F-4 — 🟠 Every write on the booking path replaces the whole provider document

`BookingAppointmentCommandHandler.SearchAndUpdateProviderAppointments` reads the provider, appends the
appointment to `provider.AppointmentEntities`, and calls `UpdateProviderAsync` — a `ReplaceOneAsync`. Two
consequences for F-014, which is about to add five more write paths:

1. **Lost updates.** Two concurrent bookings for one provider both read, both append, both replace; the
   second overwrites the first. The appointment survives in `appointments` and vanishes from the provider
   document, so the two collections disagree — and `ReportingService` reads the *embedded* copy.
2. **F-021 added the tool for this.** `IRepository<T>.FindOneAndUpdateAsync` (ADR-032) can express
   "append this to the array" as a `$push` against a filter, with no read and no replacement. F-014 should
   not add new read-modify-write paths when a targeted one now exists.

### F-5 — 🟠 **Revenue cannot be computed from the current data model at all**

`ReportingService`'s formula is:

```csharp
var totalServiceFee = provider.ServiceEntities.Where(s => s.IsActive && s.Fee.HasValue).Sum(s => s.Fee!.Value);
var estimatedRevenue = completed.Count * totalServiceFee;
```

That is *completed appointments × the sum of every active service's fee*. A provider offering three
services at 50, 80 and 100 with two completed appointments would be reported as having earned **460** —
the appointment count times the catalogue total, which is not revenue under any definition.

**And it cannot be fixed by changing the formula, because `AppointmentEntity` does not record which service
the appointment is for.** Verified: its fields are `Identifier`, `EmailProvider`, `EmailCustomer`, `Start`,
`End`, `AppointmentStatus`, `AppointmentDescription`, `DayOff` — there is no service reference, no fee and
no amount. The information needed to compute revenue **does not exist in the stored data**.

So fixing F-1 (status transitions) makes this number non-zero and *still* wrong, which is arguably worse
than zero: 0 reads as "no data yet", while 460 reads as a fact. The options are a data-model change
(appointments reference the service they booked — which is also what `PaymentEntity.Amount` would want) or
not publishing a revenue figure until one exists. This is a **product decision**, and it is the one thing
in F-014 that cannot be resolved by wiring.

Two mitigating facts: `ProviderReport` is an internal DTO with **no consumer yet** — the mobile client
cannot reach any of this (F-015) — so changing its shape costs nothing today and costs a client rewrite
later. And `ServiceEntity.FeeType` exists, so a fee is already known to be per-session or otherwise, which
a real revenue calculation would need.

---

## 3. Scope decision: F-014 splits, and the cut is not where the roadmap put it

**Recorded scope was:** wire the six capabilities, **plus** "prevent double-booking (`Start < End`,
future-dated, no slot overlap)", absorbed at the 2026-08-18 program Discover because *"`INTENT.md` names
double-booking a core user frustration, and F-004 is marked Shipped while permitting it, which is the same
'shipped but doesn't work' class this feature exists to fix."*

That reasoning is **thematic, not technical** — same defect class, no dependency. Findings F-1/F-2 supply a
real dependency in a different place, so the cut moves:

**F-014 keeps** the six capabilities **and** server-enforced appointment status (F-2) **and** the
cancellation inversion (F-3) — because `ReportingService` is one of the six, and wiring it without F-2
ships a dashboard whose two headline numbers are structurally zero. That is a dependency, not a theme.

**F-025 `booking-correctness` takes** the slot rules: `Start < End`, future-dating, and overlap prevention.
Filed as `agenda-buddy-ohw`. Separated because it is a **different shape of work** — F-014 registers and
routes existing code, while overlap prevention needs a concurrency decision that deserves its own design
(a read-then-insert is racy; the candidates are an atomic conditional write, a unique slot key, or an
explicitly accepted and documented race). It has no technical dependency on F-014.

**Cost of being wrong about this:** low and symmetric. Both features touch
`BookingAppointmentCommandHandler`, so if the maintainer prefers one feature, merging F-025 back is a PRD
section rather than a re-plan. The sequence stays as the roadmap has it — F-014 next, F-025 after — rather
than being reordered on a Discover's authority. **A case exists for the reverse:** F-025 is smaller and
closes the *booking corruption* half of `INTENT.md`'s "Zero Sev-1 bugs — no data loss or booking
corruption bugs", whose *data loss* half F-021 just closed. One line in `ROADMAP.md` flips it.

---

## 4. The six capabilities, and where each one lands

No new service. Each capability goes to the service that already owns its data or its actor:

| Capability | Host service | Why there | Sensitivity |
|---|---|---|---|
| `NoteService` | **Booking** | Notes hang off an appointment identifier, and appointments live in Booking | 🔴 **highest in the product** — therapy/coaching session notes, provider-private |
| `PaymentService` | **Booking** | F-010's premise is "collect fees at booking time" | 🔴 money + a live gateway credential |
| `MessageService` | **Customer** | F-007 is provider↔customer messaging; the customer service already owns the subscription relationship and the per-provider Kafka topic | 🟠 message bodies between two named people |
| `NotificationService` | **Customer** | Recipient-addressed, same actor set as messaging; keeping both in one service avoids a second new route family in Booking | 🟠 |
| `ReportingService` | **Provider** | A provider's own metrics, keyed by provider email | 🟠 revenue |
| `DeactivateProviderCommand` | **Provider** | It mutates a provider | 🟠 destructive |

**Every one of these is owner-scoped data, and F-016 is the reason that has to be said out loud.** Its
central finding was that five routes returned PII to anonymous callers; the mitigation was
`OwnershipGuard` on every route plus `AssertRole` where a role distinction exists. F-014 adds **six new
route families to that surface**, two of which carry the most sensitive data the product holds. The
threat model at Design owns this, and the default posture is: authenticated, ownership-guarded,
role-checked where provider-only, and no route returns a list that is not scoped to the caller.

---

## 5. What this feature must not repeat

Three lessons from the last three episodes, each with a concrete consequence here:

1. **From F-016:** *a security control no test can reach is a control that can ship unexercised.* Every new
   route gets an integration test that drives it over HTTP against a real MongoDB, not just a unit test on
   the service behind it. The harness exists and hosts any of the seven services.
2. **From F-021:** *the harness catches what unit tests structurally cannot.* Its Identity tests found a
   500-instead-of-401 that no unit test could see, because every unit test set an environment variable the
   harness deliberately does not. Payments will have the same shape — a gateway that must not be called
   for real in a test.
3. **From all three:** *every real defect in this project was found by running the software, not by
   reviewing it* (METRICS observation 2, restated at 001, 002 and 003). F-014's ship gate must exercise
   each new route against a live stack, and F-1 above is precisely the kind of defect that reads fine in
   review and reports £0 in production.

---

## 6. Open questions for Define

1. **Does `PaymentService` charge on a real gateway, ever, in this repository's lifetime?** There is no
   Stripe account, no key, and no deployment (ADR-035 defers cloud indefinitely). The honest options are:
   register a **fake/no-op gateway by default** and the Stripe one only when a key is configured, or wire
   Stripe and leave it unreachable. The first is testable and cannot accidentally charge anyone; the
   second matches what F-010 claimed. **Recommendation: fake by default, Stripe behind configuration** —
   the same shape as F-021's config-gated controls, and it makes `PaymentService` verifiable without a
   payment provider.
2. **Does the status transition become a dedicated route or stay on the `PUT`?** A `PUT` that binds the
   whole entity is what lets a client assert `Completed`. Either the status field becomes server-owned and
   ignored on input, or status changes move to their own endpoint. The mobile client already calls
   something shaped like the latter (`ExecuteStatusUpdateAsync`).
3. **Do notifications get *sent* anywhere, or only stored?** `NotificationService.SendAsync` inserts a
   document; there is no email, no push, and `DeviceTokenEntity` exists with a registration route but
   nothing reads it. F-022 (password reset) is recorded as depending on `NotificationService` — that
   dependency is only satisfied if "send" eventually means "deliver".
4. **What does the reporting endpoint publish for revenue?** See F-5: the number is
   `completed.Count × sum(all active service fees)`, and it cannot be corrected by arithmetic because an
   appointment does not record which service it was for. Three options, in increasing cost:
   **(a)** omit `EstimatedRevenue` from the wired endpoint and say why — free today, since `ProviderReport`
   has no consumer; **(b)** publish it with the formula stated in the response so nobody mistakes it for
   accounting; **(c)** add a service reference to `AppointmentEntity` and compute it properly — correct,
   but it is a data-model change that F-015's mobile contract and F-025's booking rules both touch.
   **Recommendation: (a) for this feature, and file (c).** Publishing a number this feature knows to be
   wrong repeats exactly the mistake F-014 exists to fix — code marked delivered that does not do what its
   name says.
