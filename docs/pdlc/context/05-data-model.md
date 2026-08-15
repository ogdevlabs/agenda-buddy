# 05 — Data Model

**Files:** `Library/Entities/` (13 types), `Library/Data/ProfessionSeedData.cs`, `Library/Data/DevelopmentSeedData.cs`, `Library/Tools/Migrations/` (2), `scripts/seed/`.

**Schema ownership:** MongoDB is schemaless; the C# entity classes are the de facto schema. There is **no migration framework** (no Flyway/Liquibase/EF Migrations equivalent) and **no schema versioning field** on any document. `CONSTITUTION.md` §9 requires schema changes to be recorded in `DECISIONS.md` before implementation.

**Field naming:** every persisted property carries `[BsonElement("snake_case")]` per `CONSTITUTION.md` §2. This is honoured consistently.

---

## Database and collection ownership

| Database | Collection | Entity | Configured by | Read/written by |
|---|---|---|---|---|
| `agenda_buddy` | `providers` | `ProviderEntity` | `LibrarySettings.MongoDB.ProvidersCollection` | Booking, Calendar, Customer, Provider, Services, Profession |
| `agenda_buddy` | `customers` | `CustomerEntity` | `…CustomersCollection` | Booking, Calendar, Customer |
| `agenda_buddy` | `appointments` | `AppointmentEntity` | `…AppointmentsCollection` | Booking, Calendar |
| `agenda_buddy` | `services` | `ServiceEntity` | `…ServicesCollection` | Services |
| `agenda_buddy` | `professions` | `ProfessionEntity` | `…ProfessionsCollection` | Profession |
| `agenda_buddy` | `events` | `EventAndCommands.Persitency.Event` | `…EventsCollection` | all six domain services |
| `IdentityDb` | `credentials` | `CredentialEntity` | `MongoDbSettings.CollectionName` | Identity |
| `IdentityDb` | `device_tokens` | `DeviceTokenEntity` | ⚠️ **hardcoded** `Identity/Extensions/ServiceCollectionExtension.cs:17` | Identity |
| *(none)* | — | `NotificationEntity` | ⚠️ **no collection configured** | nothing |
| *(none)* | — | `MessageEntity` | ⚠️ **no collection configured** | nothing |
| *(none)* | — | `NoteEntity` | ⚠️ **no collection configured** | nothing |
| *(none)* | — | `PaymentEntity` | ⚠️ **no collection configured** | nothing |

⚠️ **Four entities have no collection mapping and no repository registration**: `NotificationEntity`, `MessageEntity`, `NoteEntity`, `PaymentEntity`. Their services are likewise unregistered (`03-services.md`). Features F-006–F-010 shipped the types and their unit tests but never the persistence wiring — there is no `NotificationsCollection` / `MessagesCollection` / `NotesCollection` / `PaymentsCollection` key in any `appsettings.json`.

⚠️ **`device_tokens` is the only hardcoded collection name** (`Identity/Extensions/ServiceCollectionExtension.cs:17`), breaking the config-driven convention every other collection follows.

⚠️ **The `services` collection is configured but never used as a standalone collection.** `ServiceEntity` is only ever persisted **embedded** inside `ProviderEntity.ServiceEntities` (`ProviderEntity.cs:38`). `Services/Extensions/ServiceCollectionExtension.cs:15-17` registers `IRepository<ServiceEntity>` against the `services` collection, but nothing injects it — `ServiceService` takes no repository (`03-services.md`).

---

## ⚠️ Seed script targets the wrong databases

`scripts/seed/seed-mongo.sh` imports into:

| Line | Target | Application reads from |
|---|---|---|
| `:14-15` | `ProviderDb.providers` | ❌ `agenda_buddy.providers` |
| `:22-23` | `CustomerDb.customers` | ❌ `agenda_buddy.customers` |
| `:30-31` | `IdentityDb.credentials` | ✅ `IdentityDb.credentials` |

**Failure scenario:** run `scripts/seed/seed-mongo.sh`, then log in with `sarah.mitchell@agendabuddy.dev` / `DevPass123!` (`:44-46`). Authentication **succeeds** (credentials landed in the right place) but every provider and customer lookup returns nothing, because the provider/customer documents are in databases no service opens. **Inference:** this is the proximate cause of the mobile client's `SeedDataProvider` fallback existing at all.

Also note `:17,25,33` pass `--drop`, so the script is destructive by design.

---

## Entity reference

### `ProviderEntity` (`Library/Entities/ProviderEntity.cs`) — the aggregate root

| Field | Column | Type | Nullable | Notes |
|---|---|---|---|---|
| `Id` | `_id` | `ObjectId` | no | `:25` |
| `FirstName` | `first_name` | `string` | `[Required]` | `:27` |
| `LastName` | `last_name` | `string` | `[Required]` | `:29` |
| `Email` | `email` | `string` | `[Required] [EmailAddress]` | `:34` — the de facto business key |
| `KafkaTopic` | `kafka_topic` | `string?` | yes | `:36` — set from `KafkaHelper.CreateProviderTopicName` |
| `ServiceEntities` | `services` | `List<ServiceEntity>` | no, `[]` | `:38` — **embedded** |
| `AppointmentEntities` | `appointments` | `List<AppointmentEntity>` | no, `[]` | `:40` — **embedded** |
| `SubscribedCustomerCollection` | `subscribed_customer_collection` | `List<string>` | no, `[]` | `:42` — customer emails |
| `IsActive` | `is_active` | `bool` | no, `true` | `:44` — soft-delete flag |

`#pragma warning disable CS8618` at `:1` suppresses the uninitialised-non-nullable warnings for the parameterless constructor Mongo needs.

⚠️ **This is an unbounded embedded array design.** Every appointment a provider ever books is appended to `appointments` inside the provider document (`BookingAppointmentCommandHandler.cs:52`). MongoDB's 16 MB document limit becomes a hard ceiling on a provider's lifetime appointment count, and **every** provider read deserialises the entire history. `INTENT.md` targets 5–20 sessions/week per provider ⇒ ~1,000/year ⇒ the limit is years away but the read cost is immediate and grows monotonically.

⚠️ **Appointments are stored twice** — once in the `appointments` collection and once embedded in the provider. Nothing keeps them consistent: `UpdateAppointmentCommandHandler.cs:51-57` updates the embedded copy *and* the standalone copy in two separate non-transactional writes. A failure between them leaves them divergent permanently.

⚠️ **No availability/schedule field.** F-005 `provider-availability-schedule` is marked Shipped, but there is nowhere to store a provider's hours or days off. Availability is hardcoded 09:00–19:00 in `SupportTools.GetThirtyDaysCalendarAvailability` (`04-data-access.md`).

⚠️ **No `ProviderEntity` → `ProfessionEntity` link.** `INTENT.md` and F-002 describe a provider "defining their profession", and the `professions` collection is seeded, but no field references it.

### `AppointmentEntity` (`Library/Entities/AppointmentEntity.cs`)

| Field | Column | Type | Notes |
|---|---|---|---|
| `Id` | `_id` | `ObjectId` | `:23` |
| `Identifier` | `identifier` | `string` | `:24` — `init`-only, defaults to a fresh `Guid`. **The business key** used by all update/delete paths |
| `EmailProvider` | `email_provider` | `string` | `:28` — `required`, `[EmailAddress]` |
| `EmailCustomer` | `email_customer` | `string` | `:32` — `required`, `[EmailAddress]` |
| `Start` | `start` | `DateTime` | `:36` — `[BsonDateTimeOptions(Kind = Utc)]` |
| `End` | `end` | `DateTime` | `:40` — UTC |
| `AppointmentStatus` | `appointment_status` | `AppointmentStatus` | `:43` — defaults `Requested`; serialised **as an int** (no `[BsonRepresentation(BsonType.String)]`) |
| `AppointmentDescription` | `appointment_description` | `string` | `:46-47` — a denormalised copy of the enum's `[Description]` |
| `DayOff` | `day_off` | `bool` | `:49` |

Domain methods: `Book()` (`:51`) enforces `Requested → Booked`; `Complete()` (`:59`) enforces `Booked → Completed`. Both throw `InvalidOperationException` otherwise.

⚠️ **Neither `Book()` nor `Complete()` is ever called** anywhere in the solution. Status is assigned by direct property write (`UpdateAppointmentCommandHandler.cs:51`, `CalendarService.cs:30`), bypassing the invariants entirely.

⚠️ **No transitions exist for `Confirmed` or `Cancelled`** (`AppointmentStatus.cs:8-9`, added by F-012). There is no `Confirm()` or `Cancel()` method, and `Cancel` is implemented as a hard delete (`03-services.md`). The enum and the domain model disagree.

⚠️ **`AppointmentStatus` persists as an integer.** Adding `Confirmed`/`Cancelled` *after* `Completed` (`:7-9`) was ordinal-safe, but any future insertion in the middle of the enum silently reinterprets stored documents. No `[BsonRepresentation(BsonType.String)]` guard.

⚠️ **`AppointmentDescription` (`:46`) is a denormalised duplicate of the status** whose default is computed at construction from `Requested`, and which is only refreshed if the caller sets it. `UpdateAppointmentCommandHandler.cs:52` copies whatever the client sent — so a client can set `appointment_status: Cancelled` with `appointment_description: "Appointment Booked"`. No invariant ties them.

⚠️ **No service reference and no fee snapshot.** An appointment does not record *which* `ServiceEntity` was booked. This is why `ReportingService`'s revenue calculation cannot be correct (`03-services.md`).

⚠️ **No overlap/double-booking constraint.** Nothing in the entity, the service, or the handler prevents two appointments with the same `email_provider` and overlapping `[start, end)`. `INTENT.md` names double-bookings as a core user frustration.

### `CustomerEntity` (`Library/Entities/CustomerEntity.cs`)

`_id`, `first_name`, `last_name`, `email` (all `[Required]`, email also `[EmailAddress]`), `kafka_topic`, `subscribed_provider_collection` (`List<string>?`), `appointment_identifier_collection` (`List<string>?`).

⚠️ All four content fields are declared `string?` / `List<string>?` **and** `[Required]` (`:23-37`) — the nullable reference annotation and the validation attribute contradict each other. `MiniValidator` enforces `[Required]` at the boundary, so the `?` is misleading to readers.

⚠️ **Asymmetric with `ProviderEntity`:** the customer holds appointment *identifiers* (`:37`) while the provider holds full embedded *appointment objects*. Two different relationship strategies for the same association.

### `ServiceEntity` (`Library/Entities/ServiceEntity.cs`)

`_id`, `name` `[Required]`, `description` `[Required]`, `fee` (`decimal?`, default `0`), `feeType`, `isActive` (default `true`). Enum `FeeType { Hourly, Fixed, Subscription }` (`:32-36`).

⚠️ **`feeType` and `isActive` break the snake_case convention** (`:27`, `:29`) — they are `[BsonElement("feeType")]` / `[BsonElement("isActive")]` in camelCase while every other field in every other entity is snake_case. A direct `CONSTITUTION.md` §2 violation, and it means a Mongo query written from the convention will silently match nothing.

⚠️ `Fee` is `decimal?` defaulting to `0` — "free" and "unpriced" are indistinguishable. `ReportingService.cs:28` filters on `s.Fee.HasValue`, which is true for the `0` default.

### `ProfessionEntity` (`Library/Entities/ProfessionEntity.cs`)

`sealed`. `_id` + `name` (`required`). Seeded by `Library/Data/ProfessionSeedData.cs` via `Profession/Extensions/ServiceCollectionExtensions.cs:35`. No uniqueness constraint on `name` in code — the duplicate check is an application-level read in `Profession/Program.cs:100-108`, which is racy (two concurrent POSTs both pass).

### `CredentialEntity` (`Library/Entities/CredentialEntity.cs`)

`#pragma warning disable CS8618` at `:1`.

| Field | Column | Notes |
|---|---|---|
| `Id` | `_id` | `:9-11` — `[BsonId] [BsonRepresentation(BsonType.ObjectId)]` on a **`string`** (the only entity to do this; all others use `ObjectId`) |
| `Email` | `email` | `:16` — `[Required] [EmailAddress]`; unique index created by `seed-mongo.sh:39` |
| `PasswordHash` | `password_hash` | `:20` — BCrypt, work factor 12 |
| `Role` | `role` | `:25` — `"Provider"` or `"Customer"`, single role in v1 |
| `MustResetPassword` | `must_reset_password` | `:29` — default `false` |
| `RefreshToken` | `refresh_token` | `:33` — embedded `RefreshTokenDocument?`, null when no session |

`RefreshTokenDocument` (`:38-47`): `hash` (SHA-256 hex of the opaque token — raw token never stored) and `expiry` (UTC).

⚠️ **The doc comment at `:44-45` claims "TTL index on this field in MongoDB"** but no TTL index is created anywhere in the repo — `seed-mongo.sh:39` creates only the unique email index. Expired refresh tokens are never reaped. `[verify against live Atlas cluster]`.

⚠️ **Only one refresh token per account** (single embedded document, not an array) — so a second device login invalidates the first. See `13-security.md` for the rotation data-loss risk.

### `DeviceTokenEntity` (`Library/Entities/DeviceTokenEntity.cs`)

`_id` (string + `[BsonRepresentation]`), `user_email` `[Required] [EmailAddress]`, `token` `[Required]`, `platform` `[Required]`, `registered_at`, `updated_at` (both UTC). One row per user (`03-services.md`), so multi-device push is not supported. No unique index on `user_email`, and the upsert is a racy read-then-write.

### `NotificationEntity` (`Library/Entities/NotificationEntity.cs`)

`_id`, `recipient_email` `[Required]` (⚠️ **no `[EmailAddress]`** unlike every other email field), `subject` `[Required]`, `body`, `type` (`NotificationType` enum, `:49-55`), `appointment_identifier`, `created_at` (UTC), `is_read`. Enum has 4 values — `AppointmentBooked/Updated/Cancelled/Completed` — with **no `AppointmentConfirmed`**, so F-012's `Confirmed` status has no matching notification type.

### `MessageEntity` (`Library/Entities/MessageEntity.cs`)

`_id`, `sender_email`, `recipient_email` (both `[Required] [EmailAddress]`), `body` `[Required]`, `sent_at` (UTC), `is_read`, `thread_id`. `thread_id` is computed in the service, not the entity (`MessageService.cs:11-14`). ⚠️ No length cap on `body`.

### `NoteEntity` (`Library/Entities/NoteEntity.cs`)

`_id`, `provider_email` `[Required] [EmailAddress]`, `appointment_identifier` `[Required]`, `content` `[Required]`, `created_at`, `updated_at` (both UTC). Ownership enforced in `NoteService` (`03-services.md`). ⚠️ Notes are described as "private session notes" (F-008) but stored unencrypted — see `13-security.md`.

### `PaymentEntity` (`Library/Entities/PaymentEntity.cs`)

`_id`, `appointment_identifier` `[Required]`, `provider_email` + `customer_email` (`[Required] [EmailAddress]`), `amount` (`decimal`), `currency` (default `"usd"`), `stripe_payment_intent_id` (`string?`), `status` (`PaymentStatus` enum `:55-61`), `created_at` (UTC).

⚠️ `amount` is `decimal` in C# and will serialise to BSON `Double` by default (no `[BsonRepresentation(BsonType.Decimal128)]`) — **floating-point storage for money**. Rounding drift on read/write.

⚠️ No unique index or constraint on `appointment_identifier`, yet `PaymentService.GetByAppointmentAsync` / `RefundAsync` both use `FindOneAsync` on it and assume one payment per appointment.

### `ProviderReport` (`Library/Entities/ProviderReport.cs`)

Not persisted — a computed DTO returned by `ReportingService`. It is the only type in `Library/Entities/` with **no `[BsonElement]` attributes**, which is correct but makes the folder's contents heterogeneous.

### `Event` (`EventAndCommands/Persitency/Event.cs`)

The audit record. `_id` (`ObjectId`), `timestamp`, `status` (`"Success"`/`"Failed"`), `type` (command/query name), `data` (a **JSON string** of the serialised entity).

⚠️ **`Id` is set to a fresh `ObjectId.GenerateNewId()` per event** (e.g. `BookingAppointmentCommandHandler.cs:22`), but `IEventStore.GetEventsAsync(ObjectId aggregateId)` (`IEventStore.cs:6`) filters `e.Id == aggregateId` (`EventStore.cs:21`). Since `Id` is the event's own primary key and not an aggregate id, **`GetEventsAsync` can only ever return a single event or none** — it cannot retrieve an aggregate's event stream. The event-sourcing read path is non-functional. (Nothing calls `GetEventsAsync`, so this is latent.)

⚠️ **`data` stores PII as an opaque JSON blob** — full provider records, customer records with emails, and on query paths the entire provider list (`15-cqrs-and-messaging.md`). Unindexed, unbounded, never pruned, no retention policy.

---

## Seeding paths

Three separate, inconsistent seeding mechanisms:

| Mechanism | Anchor | Wired in? | Targets |
|---|---|---|---|
| `ProfessionSeedData` | `Profession/Extensions/ServiceCollectionExtensions.cs:29-37` | ✅ **yes** — runs at DI registration via `.Wait()` | `agenda_buddy.professions` |
| `SeedDevelopmentAccounts` + `DevelopmentSeedData` | `Library/Tools/Migrations/SeedDevelopmentAccounts.cs` | ❌ **never invoked** (only its own file and `Library.Tests` reference it) | would target `providers`, `customers`, `credentials` |
| `SeedAuthCredentials` | `Library/Tools/Migrations/SeedAuthCredentials.cs` | ❌ **never invoked** | would backfill `credentials` from existing providers/customers |
| `scripts/seed/seed-mongo.sh` | `scripts/seed/seed-mongo.sh` | ✅ manual | ⚠️ `ProviderDb`, `CustomerDb`, `IdentityDb` — two of three wrong |

⚠️ **`SeedAuthCredentials` is the F-001 auth-migration path and it is dead code.** Its doc comment (`:9-13`) describes a one-time migration seeding a credential for every existing provider and customer with `MustResetPassword=true`. Nothing calls it, so any pre-auth provider/customer records have no credentials and cannot log in. There is also no login-time handling of `MustResetPassword` — `IdentityService.LoginAsync` (`Identity/Services/IdentityService.cs:79-121`) never reads the flag, so even if seeded, forced reset would not happen.

⚠️ **`DevelopmentSeedData.DefaultPassword = "DevPass123!"`** is a committed literal (`Library/Data/DevelopmentSeedData.cs:151`) inside the shipped `Library` assembly, not a test project. It is echoed by `seed-mongo.sh:46`.

## What is missing

- No indexes defined in application code (one, in a shell script).
- No TTL index despite a documented expectation.
- No unique constraints on `providers.email`, `customers.email`, `professions.name`, or `device_tokens.user_email` — all duplicate checks are racy application reads.
- No schema version field on any document; no migration runner.
- No soft-delete on appointments; no audit of who changed what (the `Event` record has no actor field).
- No optimistic-concurrency token on any entity.
