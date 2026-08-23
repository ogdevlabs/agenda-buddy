# PRD: Wire Unreached Services

**Date:** 2026-08-23
**Status:** Approved
**Feature slug:** `wire-unreached-services`
**Feature ID:** F-014
**Episode:** *(assigned after delivery)*

---

## Overview

Five features of this product are marked **Shipped** on code nothing can call. `NotificationService`,
`MessageService`, `NoteService`, `PaymentService` and `ReportingService` all have implementations and unit
tests, and none of them has a DI registration, a configured collection, or an HTTP route — verified at
Discover: **zero** non-test references outside their own definitions. `DeactivateProviderCommand` is the
same shape one layer down: a command, a handler and an event, dispatched by nothing.

F-014 makes them reachable, behind authenticated and ownership-guarded routes.

It also fixes the one thing that would make wiring dishonest. `ReportingService` derives its headline
numbers from `AppointmentStatus.Completed`, and **nothing in production ever sets that status** — the
methods holding the transition rules are dead code, while `UpdateAppointmentCommandHandler` copies whatever
status the client sent. Wiring reporting without fixing that ships a provider dashboard structurally
guaranteed to report zero completed appointments, which is the same "marked delivered, does not work"
defect this feature exists to end.

**Claim: every capability this product says it has can be reached, is owner-scoped, and reports what it
actually knows.**

---

## Problem Statement

**1. Six capabilities are unreachable.** F-006 (notifications), F-007 (messaging), F-008 (session notes),
F-009 (reporting) and F-010 (payments) are all recorded as Shipped in `ROADMAP.md`. A user cannot reach any
of them, and no test could have caught that, because each service's unit tests exercise the service class
directly.

**2. Appointment status is client-asserted and unguarded.** `AppointmentEntity.Book()` and `.Complete()`
encode the transition rules — `Requested → Booked → Completed` — and neither is called anywhere outside
tests. What runs instead is `appointment.AppointmentStatus = appointmentEntity.AppointmentStatus`
(`UpdateAppointmentCommandHandler.cs:51`), binding a public settable enum straight from the request body. A
caller can set `Completed` on an appointment created a second earlier. `MobileApp` already drives status
this way (`AppointmentDetailPage.xaml.cs:93`), so this is a live design, not a hypothetical.

**3. Cancellation refuses the wrong state.** `CancelAppointmentCommandHandler` declines to cancel a
**`Booked`** appointment and a `Completed` one. Refusing a completed appointment is correct; refusing a
booked one means a customer cannot cancel the appointment they actually have. It is invisible today only
because nothing sets `Booked` — **fixing problem 2 activates it**, in the same release, looking like its
fault.

**4. The payment gateway cannot be registered, and holds a live credential badly.**
`StripePaymentGateway(string apiKey)` takes a raw string with no configuration section anywhere, and sets
`StripeConfiguration.ApiKey` — a **process-global static** — on every call.

**5. Four collections have no configured name.** `NotificationsCollection`, `MessagesCollection`,
`NotesCollection` and `PaymentsCollection` appear in no configuration file.

---

## Target User

The `INTENT.md` primary persona — the independent service provider managing 5–50 clients — for four of the
six capabilities, because notes, reporting, payments and deactivation are all provider-facing. Messaging
and notifications are **both** actors: a customer has an inbox for the same reason a provider does.

Two stakeholders shape specific decisions:

- **The provider who reads their own dashboard.** They are the reason this feature refuses to publish a
  revenue figure it cannot compute (requirement 18). A dashboard reading £0 is a bug; a dashboard reading a
  confidently wrong number is worse, because it will be believed.
- **The developer running the stack with no payment provider.** There is no Stripe account, no key, and no
  deployment (ADR-035 defers cloud until every pending feature ships). Payments have to be exercisable
  without a payment provider, or `PaymentService` becomes the seventh unreachable capability.

---

## Requirements

**Reachability — the six capabilities**

1. `NoteService` MUST be reachable on the **Booking** service: create, list-by-appointment, update and
   delete. Notes are **provider-private**; the owning provider MUST be taken from the caller's token and
   never from the request.
2. `PaymentService` MUST be reachable on the **Booking** service: charge for an appointment, and read the
   payment for an appointment.
3. `MessageService` MUST be reachable on the **Customer** service under its own `api/v1/messages` route
   group: send, read a thread, read the caller's inbox, mark read.
4. `NotificationService` MUST be reachable on the **Customer** service under `api/v1/notifications`: list
   the caller's notifications and mark one read. **Storage only** — see requirement 19.
5. `ReportingService` MUST be reachable on the **Provider** service, returning the caller's own report.
6. `DeactivateProviderCommand` MUST be dispatchable from the **Provider** service.
7. Every one of the six MUST have a DI registration and a configured collection name, and MUST resolve its
   database through `MongoConnectionResolver` like every existing repository.

**Authorization — F-016's posture, extended to six new route families**

8. Every new route MUST require authentication. No new route may be anonymous.
9. Every new route MUST be **ownership-guarded** with `OwnershipGuard`, against the caller's `sub` claim.
   No route may accept an identity in its body or path and trust it.
10. Notes and reporting MUST additionally require the **`Provider`** role (`AssertRole`). Messages,
    notifications and payments are reachable by both roles.
11. No new route may return a collection that is not scoped to the caller. There MUST be no
    "list all notes", "list all messages" or "list all payments" route.
12. A note, message, notification or payment belonging to someone else MUST answer **403**, and MUST NOT
    reveal through status code or body whether the resource exists.

**Appointment status — server-owned**

13. `AppointmentStatus` MUST become **server-owned**: the value in a request body MUST be ignored on the
    existing `PUT`, which MUST preserve the stored status.
14. Status changes MUST happen through a dedicated route that names the target state, and MUST be applied
    through `AppointmentEntity`'s own transition methods so the rules run. An illegal transition MUST
    answer **409**.
15. Cancellation MUST be permitted for a `Booked` appointment and refused for a `Completed` one.
16. A status change MUST be permitted only for the appointment's provider or its customer, and completing
    an appointment MUST be **provider-only**.

**Payments**

17. The payment gateway MUST be DI-registered, and MUST default to a **non-charging** implementation. The
    Stripe implementation MUST be selected only when a key is explicitly configured, and the key MUST NOT
    live in `appsettings.json`. `StripeConfiguration.ApiKey` MUST NOT be assigned per call from request
    handling.

**Honesty of the reporting surface**

18. The wired report MUST NOT publish `EstimatedRevenue`. The figure is
    `completed × sum(all active service fees)`, and it cannot be corrected by arithmetic because an
    appointment does not record which service it is for. The response MUST state that revenue is
    unavailable and why, rather than omitting it silently.
19. `NotificationService` MUST be documented as **storage without delivery**: no email, no push. F-022's
    recorded dependency on it is satisfied only when "send" means "deliver".

**Data-access discipline**

20. New write paths MUST NOT introduce whole-document replacement on `ProviderEntity`. Appending to an
    embedded collection MUST use the partial-update primitive (ADR-032).

---

## Assumptions

1. **No new service.** Six capabilities land on three existing services, chosen by data ownership. A
   service is a deployment unit, not a URL prefix — Identity already hosts two unrelated route groups, so
   `api/v1/messages` living in the Customer process is a precedent, not a novelty.
2. **`ProviderReport` has no consumer.** Verified: the mobile client cannot reach any of this (F-015).
   Changing its shape is free **today** and costs a client rewrite later, which is why requirement 18 is
   cheap now.
3. **The integration harness can host all three services.** F-016 built it and F-021 extended it with
   per-instance configuration; `Booking`, `Customer` and `Provider` all have anchors or can be given one.
4. **Payments will never be charged for real in this repository's lifetime.** No account, no key, no
   deployment. Requirement 17's default is what makes the capability testable rather than theoretical.
5. **Cache invalidation still does not exist** (`agenda-buddy-xrw`). Any read F-014 adds that goes through
   `CacheAside` can be up to five minutes stale. New routes should not cache until that is fixed.
6. **`AppointmentStatus.Confirmed` and `.Cancelled` stay unused.** `Confirmed` is assigned only on a
   Calendar projection; `Cancelled` is never assigned because cancellation hard-deletes. F-014 does not
   introduce them into the transition graph — that is a product question about what the states mean.

---

## Acceptance Criteria

**Reachability**

1. Given an authenticated provider, when they create a note against their own appointment, then it is
   stored and returned, and `GET` returns it. 🧪 test-first
2. Given an authenticated caller, when they send a message, then the recipient's inbox contains it and both
   participants' thread returns it. 🧪 test-first
3. Given an authenticated caller, when they list their notifications, then only notifications addressed to
   them are returned. `[security]` 🧪 test-first
4. Given an authenticated provider, when they request their report, then it is computed from their own
   appointments. 🧪 test-first
5. Given an authenticated provider, when they deactivate themselves, then the command is dispatched and the
   audit event is written. 🧪 test-first
6. Given an appointment, when a charge is requested, then a payment is stored against that appointment and
   is readable by identifier. 🧪 test-first
7. Given each of the six capabilities, when the hosting service starts, then its repository resolves and
   its collection name comes from configuration. 🧪 test-first

**Authorization** — every criterion here is `[security]`

8. Given an anonymous caller, when any new route is requested, then the response is **401** — asserted
   against a running service for **every** new route, not a sample. `[security]` 🧪 test-first
9. Given a valid token for a different subject, when a note, message, notification, payment or report
   belonging to someone else is requested, then the response is **403** and no data is returned.
   `[security]` 🧪 test-first
10. Given a `Customer`-role token, when a notes route or the report route is requested, then the response is
    **403**. `[security]` 🧪 test-first
11. Given any new list route, when it is called, then the result contains only the caller's own records —
    asserted with another principal's records present in the same database. `[security]` 🧪 test-first
12. Given a note that does not exist and a note belonging to someone else, when each is requested, then the
    two responses are indistinguishable. `[security]` 🧪 test-first

**Appointment status**

13. Given a `PUT` that carries `appointmentStatus: Completed` on a `Requested` appointment, when it is
    applied, then the stored status is still `Requested`. `[security]` 🧪 test-first
14. Given a `Requested` appointment, when it is transitioned to `Booked` and then `Completed`, then both
    succeed; and when `Completed` is requested directly from `Requested`, then the response is **409** and
    the status is unchanged. 🧪 test-first
15. Given a `Booked` appointment, when its customer cancels it, then the cancellation succeeds; given a
    `Completed` one, then it is refused. 🧪 test-first
16. Given a customer, when they attempt to complete their own appointment, then the response is **403**.
    `[security]` 🧪 test-first

**Payments and honesty**

17. Given no configured payment key, when the container is built, then the registered `IPaymentGateway` is
    the non-charging one, and a charge records a payment without contacting any external service.
    `[security]` 🧪 test-first
18. Given a report response, when it is inspected, then it carries no revenue figure and carries an
    explicit statement that revenue is unavailable because appointments do not record a service. 🧪 test-first
19. Given the booking path, when an appointment is appended to a provider, then the write is a targeted
    update and not a whole-document replacement. 🧪 test-first

*Threat IDs for the `[security]` criteria are assigned at the Design threat model; each will be linked to a
test named `test_TNNN_…`.*

---

## User Stories

**F-014-US-01 — A provider's session notes are private to them** *(AC-1, AC-9, AC-10, AC-12)*
**Given** a coach who keeps notes on each client session,
**When** any other provider, any customer, or an anonymous caller asks for those notes,
**Then** they are refused, and cannot tell whether the notes exist.

**F-014-US-02 — A customer and provider can actually message each other** *(AC-2, AC-11)*
**Given** a customer subscribed to a provider,
**When** either sends a message,
**Then** it appears in the other's inbox and in their shared thread — **And** in nobody else's.

**F-014-US-03 — A provider sees a dashboard that does not lie to them** *(AC-4, AC-18)*
**Given** a provider with completed appointments,
**When** they open their report,
**Then** the counts reflect their real appointments,
**And** where a number cannot be computed from what the system stores, the report says so instead of
printing a plausible figure.

**F-014-US-04 — An appointment's state is the server's, not the caller's** *(AC-13, AC-14, AC-16)*
**Given** a client that can send any JSON it likes,
**When** it asserts that a brand-new appointment is `Completed`,
**Then** the server keeps the real state, **Because** the transition rules live on the server or they do
not exist.

**F-014-US-05 — A customer can cancel the appointment they actually have** *(AC-15)*
**Given** a booked appointment,
**When** the customer cancels it,
**Then** it is cancelled — which is only true once cancellation stops refusing exactly that state.

**F-014-US-06 — Payments are exercisable without a payment provider** *(AC-17)*
**Given** a developer with no Stripe account,
**When** they charge for an appointment locally,
**Then** a payment record is created and **no external call is made**,
**Because** a capability that can only be tested with a live payment credential is a capability that will
never be tested.

---

## Testing Approach: Test-Driven Development (TDD)

Tests are written first, for every acceptance criterion, red before green. The build loop enforces this at
the TDD gate; the only exceptions are pure scaffolding and configuration, and even those require an
explicit override.

**Test layers:** **Unit** (required by §7) and **Integration** (required *by this PRD*, because AC-8 through
AC-12 are claims about routes and every one of them would pass vacuously as a unit test on a service class —
which is precisely how the five capabilities came to be marked Shipped while unreachable). Plus §7's
always-required dependency-audit and secret-scan gate.

⚠️ **Two capabilities need a test seam that does not exist yet:**
- **Payments** must be chargeable in a test without an external call. Requirement 17's non-charging default
  is that seam, and it must land before AC-6 or AC-17 can be written.
- **`DeactivateProviderCommand`** writes an audit event through `EventStore`; asserting AC-5 means reading
  the `events` collection back, which the harness can do (`ServiceHost.Database`) and no unit test can.

---

## Non-Functional Requirements

**Security**
- Six new route families is the largest single expansion of the authenticated surface since F-016 halved
  it. Every route is authenticated, ownership-guarded, and role-checked where a role distinction exists —
  and AC-8 asserts the 401 on **every** route rather than a sample, because a forgotten
  `RequireAuthorization()` is invisible in review.
- **Session notes are the most sensitive data in the product.** Therapy and coaching notes about named
  individuals. They get the strictest posture: `Provider` role, ownership, and indistinguishable
  not-found/forbidden responses.
- No payment credential may enter `appsettings.json` or git. The Aspire secret-parameter mechanism the JWT
  keys already use is the model.

**Performance**
- No new route may return an unbounded collection where the underlying data grows per user. Message and
  notification lists are per-caller and expected to be small, but the pagination primitive from F-016
  exists and should be used where a list can grow without limit.

**Operability**
- Turning the payment key off must return the system to a non-charging state with no code change.

**Constraints from `CONSTITUTION.md`**
- Business logic stays in the `Library` service layer — F-014 adds routes and DI, not domain logic. The one
  exception is the status transition, which belongs on the **entity** (where the methods already are).
- All data access through the repository pattern.
- New persisted fields carry `[BsonElement("snake_case")]`.

---

## Out of Scope

- **Slot correctness** — `Start < End`, future-dating, and overlap prevention. Split to **F-025**
  (`agenda-buddy-ohw`) at Discover: different shape of work, needs its own concurrency design, and no
  technical dependency on this feature.
- **Notification delivery** — no email, no push. Storage only (requirement 19). `DeviceTokenEntity` and its
  registration route already exist and nothing reads them; that stays true.
- **A correct revenue figure** — needs an appointment-to-service reference, which is a data-model change
  that F-015's contract and F-025's rules both touch. Filed, not built.
- **Cache invalidation** (`agenda-buddy-xrw`) — F-014 avoids caching its new reads rather than fixing it.
- **Introducing `Confirmed`/`Cancelled` into the transition graph** — a product question about what those
  states mean, not a wiring gap.
- **Soft-delete for cancellation** — cancellation still hard-deletes. F-024 owns erasure semantics.
- **Kafka publishing for messages** — `MessageService` stores; the per-provider Kafka topic F-007 built is
  not wired to it, and this feature does not change that.

---

## Known Risks

| # | Risk | Disposition |
|---|---|---|
| R1 | **Six new route families at once** is a large authorization surface to get right in one feature, and F-016 exists because this exact surface was got wrong. | AC-8 asserts the 401 on **every** route, not a sample; AC-11 asserts scoping with a second principal's data present. The integration harness makes both cheap. |
| R2 | **Making status server-owned is a breaking contract change** for any client that sets it — and `MobileApp` does. | It cannot reach the backend yet (F-015), so the change is free now and expensive later. Same argument that made F-016's breaking changes cheap. |
| R3 | **Fixing status activates the cancellation inversion** (Discover F-3). | Both fixed in the same feature, requirement 15, with AC-15 asserting both directions. |
| R4 | **The non-charging gateway could ship as the only gateway**, leaving payments permanently fake. | Accepted and visible: the Stripe implementation stays registered-when-configured, and the startup warning pattern from F-021 (ADR-033) applies — a deployment with no key gets told. |
| R5 | **Requirement 18 changes a DTO's shape**, and doing it later means changing a published contract. | Free today (assumption 2). The alternative is publishing a number known to be wrong, which is the defect class this feature exists to fix. |
| R6 | **Solo-mode meetings.** Every Discover meeting ran as one model reasoning as each role. | Recorded, as at F-016 and F-021. Fidelity is lower; read findings accordingly. |

---

## Standards Alignment

_Not assessed._ The `nordstrom-standards-readiness` plugin is installed, its six source repositories do not
resolve under this `gh` authentication, there is no local `.nordstrom-standards/` cache, and no prior
`docs/standards-readiness/` report exists to `--delta` against. **Eighth consecutive skip**, and the gate
has never once executed. The standing recommendation — give it a reachable source or retire it explicitly —
is now the oldest unaddressed process finding in the project. Folded into F-017.

---

## Design Docs

Produced at Design, in `docs/pdlc/design/wire-unreached-services/`.

---

## Related Episodes

- **Episode 003 — `identity-hardening` (`v0.3.0`)**: supplied `FindOneAndUpdateAsync`, which requirement 20
  depends on, and the configuration-gating pattern requirement 17 follows.
- **Episode 002 — `secure-public-endpoints` (`v0.2.0`)**: built the harness AC-8 through AC-12 need, and is
  the reason this feature's authorization posture is stated as requirements rather than assumed.

---

## Approval

**Approved by:** ogdevlabs *(autonomous session — the maintainer's standing instruction was to proceed
without asking; the four Define-level questions and their answers are recorded in the Discover log §6 and
resolved in requirements 17, 13–14, 19 and 18 respectively)*
**Date:** 2026-08-23
