# Data Model — Wire Unreached Services (F-014)

**Date:** 2026-08-23 · **PRD:** [`PRD_F-014_wire-unreached-services_2026-08-23.md`](../../prds/PRD_F-014_wire-unreached-services_2026-08-23.md)

---

## 1. Scope of change

**No new entity. No new field. One field removed from a DTO. Four collections gain a configured name.**

That is unusual for a feature this size and it is the point: all four entities were written by F-006–F-010
and have never been persisted, because nothing registered a repository for them.

| Collection | Entity | Database | Host service | Exists today? |
|---|---|---|---|---|
| `notes` | `NoteEntity` | `agenda_buddy` | Booking | ❌ never written |
| `payments` | `PaymentEntity` | `agenda_buddy` | Booking | ❌ never written |
| `messages` | `MessageEntity` | `agenda_buddy` | Customer | ❌ never written |
| `notifications` | `NotificationEntity` | `agenda_buddy` | Customer | ❌ never written |

MongoDB creates a collection on first write, so **no migration and no provisioning step** — the first
`POST` to each new route creates its collection.

---

## 2. The four entities, as they actually are

Verified field-by-field against the source, because the first draft of `api-contracts.md` had two of them
wrong — and F-016 shipped exactly that error into its own contracts, describing two fields that did not
exist for F-015 to bind to.

### `NoteEntity` — the most sensitive data in the product

| Field | BSON | Type | Note |
|---|---|---|---|
| `Id` | `_id` | `ObjectId` | |
| `ProviderEmail` | `provider_email` | `string` | The owner. Taken from the token, never the request |
| `AppointmentIdentifier` | `appointment_identifier` | `string` | |
| `Content` | **`content`** | `string` | ⚠️ `MessageEntity` calls its equivalent `body`. Inconsistent; not renamed (a rename is a migration for no gain) |
| `CreatedAt` / `UpdatedAt` | `created_at` / `updated_at` | `DateTime` | Set by `NoteService`, not by the caller |

### `PaymentEntity`

| Field | BSON | Type | Note |
|---|---|---|---|
| `AppointmentIdentifier` | `appointment_identifier` | `string` | The natural key for both routes |
| `ProviderEmail` / `CustomerEmail` | `provider_email` / `customer_email` | `string` | Both are checked by `AssertOwnerAny` |
| `Amount` | `amount` | `decimal` | |
| `Currency` | `currency` | `string` | Defaults to **`"usd"`**, lower case. Wrong for this product's shape; a product decision, and no data exists to migrate |
| `StripePaymentIntentId` | `stripe_payment_intent_id` | `string?` | `local_…` under the non-charging gateway |
| `Status` | `status` | `PaymentStatus` | `Pending` → `Succeeded`/`Failed`, set inside `ChargeAsync` |

### `MessageEntity`

| Field | BSON | Type | Note |
|---|---|---|---|
| `SenderEmail` / `RecipientEmail` | `sender_email` / `recipient_email` | `string` | Sender comes from the token |
| `Body` | **`body`** | `string` | ⚠️ not `content` |
| `ThreadId` | `thread_id` | `string` | **Derived, never supplied**: `MessageService` sorts the two addresses case-insensitively and joins with `::`. That derivation is what makes a thread unrequestable by a non-participant — one side is always the caller's own claim |
| `IsRead` | `is_read` | `bool` | |
| `SentAt` | `sent_at` | `DateTime` | |

### `NotificationEntity`

| Field | BSON | Type | Note |
|---|---|---|---|
| `RecipientEmail` | `recipient_email` | `string` | The only scoping key |
| `Subject` | **`subject`** | `string` | ⚠️ not `title` |
| `Body` | `body` | `string` | |
| `Type` | `type` | `NotificationType` | |
| `AppointmentIdentifier` | `appointment_identifier` | `string` | Empty when not appointment-related |
| `IsRead` / `CreatedAt` | `is_read` / `created_at` | | |

---

## 3. `ProviderReport` loses a field

`ProviderReport` is a **DTO, not a persisted document** — it is computed per request and stored nowhere.

| Field | Change |
|---|---|
| `EstimatedRevenue` | **removed** |
| `RevenueAvailable` | **added**, `bool`, always `false` for now |
| `RevenueUnavailableReason` | **added**, `string` |

**Blast radius, swept before deciding:** `ProviderReport` and `EstimatedRevenue` appear **nowhere** outside
`Library/` and `Library.Tests/`. Zero production consumers, so the shape change is free today. It will not be
free once F-015 binds a client to it.

One existing test — `ReportingServiceTest.GetProviderReportAsync_CalculatesEstimatedRevenue`, which asserts
`50m` — has its subject removed. It is **replaced**, not deleted silently: the replacement asserts that no
revenue figure is published and that the reason is stated. Same class of deviation as F-016's ADR-025 and
F-021's ADR-034, and it is called out for the same reason.

---

## 4. Indexes

**F-014 adds no index, and that is a deliberate deferral rather than an oversight.**

Every new query is an equality match on a single field: `provider_email` + `appointment_identifier` (notes),
`appointment_identifier` (payments), `thread_id` or `recipient_email` (messages), `recipient_email`
(notifications). Each would benefit from an index, and none has one.

⚠️ **The wider fact, observed on a live database at F-021's ship gate:** `db.credentials.getIndexes()`
returns exactly `["_id_"]`, and **no application code in this repository creates an index on any
collection** (`agenda-buddy-b0w`). The only `createIndex` lives in `scripts/seed/seed-mongo.sh`, which the
README records as stale. So F-014's four collections would be joining a database that has no indexing story
at all, and adding four indexes here — with no mechanism, no migration runner and no test — would be
inventing that story inside a wiring feature.

Filed with `agenda-buddy-b0w` rather than half-solved. Collection scans on empty collections are free; this
becomes real when data does.

---

## 5. Write patterns

| Operation | Mechanism | Note |
|---|---|---|
| Create a note / message / notification / payment | `InsertAsync` | Unchanged from the services as written |
| Mark read | `UpdateAsync` (whole-document replace) | ⚠️ Pre-existing in `MessageService.MarkReadAsync` and `NotificationService.MarkReadAsync`: read, set `IsRead`, replace. F-014 does **not** rewrite them — see below |
| **Appointment status change** | `FindOneAndUpdateAsync` — `$set` on `appointment_status` | New. Targeted, per ADR-032 |
| **Append an appointment to a provider** | `FindOneAndUpdateAsync` — `$push` | Replaces a read-modify-replace that loses concurrent updates (requirement 20) |

### Why `MarkReadAsync`'s read-modify-write is left alone

It is a whole-document replace, and requirement 20 forbids **new** ones on `ProviderEntity`. These are on
`messages` and `notifications`: single-owner documents with one mutable boolean, where a lost update means a
message shows unread. That is a different risk class from losing an appointment out of a provider's
embedded list.

Rewriting them would mean touching two `Library` services this feature is otherwise only *wiring* — and the
moment F-014 starts editing service internals, its claim ("these capabilities work as written, they were
merely unreachable") becomes unverifiable. **Recorded as known debt** with the honest reason, rather than
fixed opportunistically or left unmentioned.

---

## 6. Data deliberately not stored

| Not stored | Why |
|---|---|
| **A revenue figure** | It cannot be computed: an appointment does not record which service it was booked for. Publishing a wrong one repeats the defect F-014 exists to fix (ARCHITECTURE §5) |
| **Which service an appointment is for** | The fix for the above, and a data-model change touching F-015's contract and F-025's rules. Filed |
| **Notification delivery state** | `IsRead` is the only state. There is no `sent_at`, no delivery receipt and no retry, because nothing delivers anything (requirement 19) |
| **A device-token → notification link** | `DeviceTokenEntity` exists with a registration route and nothing reads it. Still true after F-014 |
| **`AppointmentStatus.Cancelled`** | Cancellation hard-deletes from `appointments` *and* removes the embedded copy, so the state is unreachable. F-024 owns erasure semantics |
| **Payment audit events** | Payments write to `payments` only. The `events` EventStore records commands, and `PaymentService` is not a command handler. Worth revisiting if payments ever become real |

---

## 7. Migration notes

**No migration file, and no data step at all.**

1. All four collections are created by MongoDB on first write.
2. All four entities already carry their `[BsonElement]` attributes — written by F-006–F-010, never
   exercised. F-014 is the first time any of them is serialised, which is itself a risk: **the first
   integration test that round-trips each entity is the first proof its BSON mapping works.** That is why
   AC-1 through AC-6 each assert a read-back rather than only a `201`.
3. `ProviderReport` is not persisted, so removing a field from it migrates nothing.
4. **Rollback is data-compatible.** Reverting F-014 leaves four collections in place, unread. Appointment
   status reverts to client-asserted, and any status a client set through the new route remains valid —
   the values are the same enum.
