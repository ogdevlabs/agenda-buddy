# API Contracts — Wire Unreached Services (F-014)

**Date:** 2026-08-23 · **PRD:** [`PRD_F-014_wire-unreached-services_2026-08-23.md`](../../prds/PRD_F-014_wire-unreached-services_2026-08-23.md)

---

## Summary

**Nine new routes. One changed behaviour on an existing route. One changed response shape on a DTO with no
consumer.**

Every new route: **authenticated**, **ownership-guarded**, and **role-checked** where a role distinction
exists. None returns an unscoped collection.

| Route | Method | Service | Auth | Role |
|---|---|---|---|---|
| `/api/v1/booking/appointments/{identifier}/notes` | `GET` | Booking | ✅ | **Provider** |
| `/api/v1/booking/appointments/{identifier}/notes` | `POST` | Booking | ✅ | **Provider** |
| `/api/v1/booking/notes/{id}` | `PUT` | Booking | ✅ | **Provider** |
| `/api/v1/booking/notes/{id}` | `DELETE` | Booking | ✅ | **Provider** |
| `/api/v1/booking/appointments/{identifier}/payment` | `POST` | Booking | ✅ | either |
| `/api/v1/booking/appointments/{identifier}/payment` | `GET` | Booking | ✅ | either |
| `/api/v1/booking/appointments/{identifier}/status` | `POST` | Booking | ✅ | either (completing is **Provider**-only) |
| `/api/v1/messages` | `GET` `POST` | Customer | ✅ | either |
| `/api/v1/messages/thread/{counterpartEmail}` | `GET` | Customer | ✅ | either |
| `/api/v1/messages/{id}/read` | `POST` | Customer | ✅ | either |
| `/api/v1/notifications` | `GET` | Customer | ✅ | either |
| `/api/v1/notifications/{id}/read` | `POST` | Customer | ✅ | either |
| `/api/v1/providers/{email}/report` | `GET` | Provider | ✅ | **Provider** |
| `/api/v1/providers/{email}/deactivate` | `POST` | Provider | ✅ | **Provider** |

**Changed:** `PUT /api/v1/booking/appointments/` — the `appointmentStatus` field in the body is now
**ignored** (requirement 13). No shape change; a behaviour change for any client that was setting it.

⚠️ **Every route below returns `403` rather than `404` for a resource the caller does not own, and the two
are made indistinguishable where the resource identifier is guessable.** Stated once here rather than
repeated per route.

⚠️ As at F-016 and F-021, the generated OpenAPI specs will **under-report** these responses: handlers return
`IResult`/`Results<…>` and only the union members appear. Pre-existing (F-018 T16/T17 own spec drift).

---

## 1. Session notes — `Provider` role, provider-private

The most sensitive data in the product: therapy and coaching notes about named individuals.

### `GET /api/v1/booking/appointments/{identifier}/notes`

The owning provider is taken from the caller's `sub` claim. **It is never accepted from the request** — a
`providerEmail` query parameter would make the guard decorative.

**`200 OK`**
```json
[ { "id": "66c1…", "appointmentIdentifier": "a7f3…", "providerEmail": "coach@example.com",
    "content": "Third session. Shoulder mobility improving.",
    "createdAt": "2026-08-23T09:00:00Z", "updatedAt": "2026-08-23T09:00:00Z" } ]
```

**`401`** anonymous · **`403`** caller is not a `Provider`, or does not own the appointment · **`200` with
`[]`** the appointment has no notes.

> **`[]` and `403` are deliberately different**, and this is the one place the indistinguishability rule is
> relaxed: the caller has already proven they own the appointment, so "no notes yet" tells them nothing they
> did not know. Distinguishing them for a **non-owner** is what leaks, and a non-owner gets `403` either way.

### `POST …/notes` → `201`
```json
{ "content": "Third session. Shoulder mobility improving." }
```
`appointmentIdentifier` comes from the path and `providerEmail` from the token. **A body carrying either is
ignored.** `400` if `content` is empty.

### `PUT /api/v1/booking/notes/{id}` → `200` · `DELETE …` → `204`

`NoteService` already enforces provider ownership internally and throws
`UnauthorizedAccessException`/`KeyNotFoundException`. Both map to **`403`** at the route — deliberately the
same code, so a caller cannot distinguish "someone else's note" from "no such note" (requirement 12, AC-12).

---

## 2. Payments

### `POST /api/v1/booking/appointments/{identifier}/payment` → `201`

**Request**
```json
{ "amount": 50.00, "currency": "GBP" }
```

`currency` is optional; `PaymentEntity` defaults it to **`"usd"`** (lower case, as stored). F-014 does not
change that default — it is wrong for a UK-shaped product, but changing it is a product decision and there
is no data to migrate yet.

**Response** — `status` is `Succeeded` under the default non-charging gateway:
```json
{ "id": "66c1…", "appointmentIdentifier": "a7f3…", "providerEmail": "coach@example.com",
  "customerEmail": "ada@example.com", "amount": 50.00, "currency": "GBP",
  "status": "Succeeded", "stripePaymentIntentId": "local_8f2c…", "createdAt": "…" }
```

> ⚠️ **A `Succeeded` status is not proof of settlement.** With no configured key the non-charging gateway
> mints an intent id prefixed **`local_`** and reports success — no money moves and no external service is
> contacted (ARCHITECTURE §4). The prefix is the signal, deliberately in the existing field rather than in a
> new one: a response-only `gatewayMode` field would have to be threaded through a DTO that does not exist,
> and `local_` cannot be produced by Stripe.

**Both participants may pay or read**, guarded with `AssertOwnerAny(user, providerEmail, customerEmail)` —
the same primitive Booking already uses for its three appointment routes. `403` for anyone else, `409` if a
payment already exists for the appointment.

### `GET …/payment` → `200`, or `404` when none exists

`404` is safe here: the caller has already proven they are a participant.

---

## 3. Appointment status — the changed contract

### `POST /api/v1/booking/appointments/{identifier}/status`

```json
{ "status": "Booked" }
```

| Response | When |
|---|---|
| `200` `{ "identifier": "…", "status": "Booked" }` | The transition is legal |
| `409` | Illegal transition — e.g. `Completed` from `Requested`. **The stored status is unchanged** |
| `403` | Caller is neither the provider nor the customer; or the target is `Completed` and the caller is not the provider |
| `400` | `status` is absent or not a member of the enum |

**Legal transitions**, and only these:

| From | To | Who |
|---|---|---|
| `Requested` | `Booked` | provider or customer |
| `Booked` | `Completed` | **provider only** — a customer marking their own session complete is a claim about work delivered |

`Confirmed` and `Cancelled` are **not** in the graph. `Confirmed` is only ever produced by a Calendar
projection; `Cancelled` is never persisted because cancellation deletes. Adding them is a product question
about what they mean, not a wiring gap.

### The existing `PUT /api/v1/booking/appointments/` — behaviour change

`appointmentStatus` in the body is **ignored**; the stored value is preserved. Every other field behaves as
before.

> **Why ignore rather than reject.** A `400` on a field the current client always sends
> (`MobileApp/Views/AppointmentDetailPage.xaml.cs:93` drives status this way) turns a silently-wrong write
> into a hard failure for a caller that has no other route yet. Ignoring is the compatible half of the
> change; the dedicated route is the correct half. The field stays in the request schema — removing it from
> `AppointmentEntity` would break its BSON round trip.

### `DELETE /api/v1/booking/appointments/` — the inversion fixed

Cancelling a **`Booked`** appointment now succeeds (it previously returned a validation problem).
Cancelling a `Completed` one is still refused.

---

## 4. Messages — caller-scoped, both actors

### `GET /api/v1/messages` → `200`

The caller's **inbox**. The recipient is the `sub` claim; there is no parameter, because a parameter would be
a thing to tamper with.

```json
[ { "id": "66c1…", "threadId": "ada@example.com::coach@example.com",
    "senderEmail": "coach@example.com", "recipientEmail": "ada@example.com",
    "body": "See you Thursday.", "isRead": false, "sentAt": "…" } ]
```

> ⚠️ **The field is `body`, not `content`.** `MessageEntity` stores `[BsonElement("body")] Body`, while
> `NoteEntity` stores `[BsonElement("content")] Content`. The two are inconsistent and F-014 does **not**
> rename either — a rename is a data migration for no functional gain. Written down because the first draft
> of this document said `content` for both, which is exactly the class of error F-016 shipped into its own
> api-contracts (two fields that did not exist, which F-015 would have bound to).

### `POST /api/v1/messages` → `201`
```json
{ "recipientEmail": "coach@example.com", "body": "Can we move to 4pm?" }
```
`senderEmail` comes from the token. **A body carrying a different sender is ignored, not rejected** — there
is no legitimate reason to send one, and rejecting it tells a prober that the field is inspected.

### `GET /api/v1/messages/thread/{counterpartEmail}` → `200`

The thread between the caller and one counterpart. `MessageService` derives `threadId` by sorting the two
addresses, so the caller cannot request a thread they are not in: **one participant is always the `sub`
claim.**

### `POST /api/v1/messages/{id}/read` → `204`

`403` unless the caller is the message's **recipient** — a sender marking their own message read is
meaningless, and allowing it would let a sender probe existence.

---

## 5. Notifications — caller-scoped, storage only

### `GET /api/v1/notifications` → `200`
```json
[ { "id": "66c1…", "recipientEmail": "ada@example.com", "type": "AppointmentBooked",
    "subject": "Appointment confirmed", "body": "Thursday 4pm with Coach",
    "appointmentIdentifier": "a7f3…", "isRead": false, "createdAt": "…" } ]
```

Field names verified against `NotificationEntity`: `subject`, `body`, `type`, `appointment_identifier`,
`is_read`, `created_at`. **There is no `title`.**

Recipient is the `sub` claim. `POST /api/v1/notifications/{id}/read` → `204`, `403` for anyone else's.

> ⚠️ **There is no route that creates a notification, and that is deliberate.** Notifications are produced
> by domain events, not by clients. `NotificationService.SendAsync` is reachable **in-process** to whatever
> writes one; F-014 exposes only the read side. **Nothing writes one yet** — no domain event calls
> `SendAsync`, so this list is empty until something does. Requirement 19: storage without delivery, and for
> now without production either. Stated because a `GET` that always returns `[]` looks broken otherwise.

---

## 6. Reporting — `Provider` role, own report only

### `GET /api/v1/providers/{email}/report` → `200`

`{email}` must equal the caller's `sub` claim — it is in the path for symmetry with the other provider
routes, not as a selector.

```json
{ "providerEmail": "coach@example.com", "totalBookings": 12, "completedAppointments": 4,
  "cancelledAppointments": 1, "uniqueCustomers": 7, "retentionRate": 42.86,
  "revenueAvailable": false,
  "revenueUnavailableReason": "Appointments do not record which service they were booked for, so revenue cannot be computed from stored data.",
  "generatedAt": "2026-08-23T09:00:00Z" }
```

**`EstimatedRevenue` is gone** (ARCHITECTURE §5). `revenueAvailable` is a `bool` rather than a nullable
number so a client cannot render `null` as `0`.

`403` non-owner or non-`Provider` · `404` no such provider — safe, since the path email must already equal
the caller's own claim.

---

## 7. Provider deactivation

### `POST /api/v1/providers/{email}/deactivate` → `202`

Ownership-guarded and `Provider`-role: **a provider deactivates themselves.** There is no administrative
role in this product (`AllowedRoles` is exactly `{Provider, Customer}` — ADR-025), so there is nobody else
who could legitimately call it.

`202 Accepted` because the command dispatches an event and writes an audit record; the response carries the
handler's result string, matching how Provider's other command routes behave.

> ⚠️ **What "deactivate" does is whatever `DeactivateProviderCommandHandler` already does** — F-014 dispatches
> it, it does not redesign it. If its semantics turn out to be wrong, that is a finding for the Build phase,
> not a silent redesign inside a wiring feature.

---

## 8. Configuration surface

| Key | Type | Default | Effect |
|---|---|---|---|
| `MongoDbSettings:NotesCollection` | string | `notes` | Booking |
| `MongoDbSettings:PaymentsCollection` | string | `payments` | Booking |
| `MongoDbSettings:MessagesCollection` | string | `messages` | Customer |
| `MongoDbSettings:NotificationsCollection` | string | `notifications` | Customer |
| `Payments:Stripe:ApiKey` | string (**secret**) | *unset* | Selects `StripePaymentGateway`. **Never in `appsettings.json`** — an Aspire secret parameter, as the JWT keys are |

All four collection names resolve through `MongoConnectionResolver.ResolveSetting`, so every legacy
configuration shape keeps working — the same mechanism the existing five collections use.
