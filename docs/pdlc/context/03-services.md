# 03 — Services (domain / business-logic layer)

**Files:** `Library/Services/` — 13 implementations + 13 `I*` interface declarations.

Per `CONSTITUTION.md` §2, all business logic must live here and not in API handlers. That holds for the *domain* operations below, but the **orchestration** logic (find provider → mutate embedded list → write both) lives in the `EventAndCommands` command handlers, not here — see `15-cqrs-and-messaging.md`.

Every service uses a **C# 12 primary constructor** taking one or more `IRepository<T>`. All are registered `Scoped` by the per-service `AddMongoDbRepository` extension (`02-entry-points.md`).

---

## Inventory

| Service | Interface | Repositories injected | Registered in |
|---|---|---|---|
| `BookingService` | `IBookingService` | `IRepository<AppointmentEntity>` | Booking |
| `CalendarService` | `ICalendarService` | `IRepository<AppointmentEntity>` | Calendar |
| `ProviderService` | `IProviderService` | `IRepository<ProviderEntity>` | all except Identity |
| `CustomerService` | `ICustomerService` | `IRepository<CustomerEntity>` | Booking, Calendar, Customer |
| `ProfessionService` | `IProfessionService` | `IRepository<ProfessionEntity>` | Profession |
| `ServiceService` | `IServiceService` | *(none)* | Services |
| `NotificationService` | `INotificationService` | `IRepository<NotificationEntity>` | ⚠️ **nowhere** |
| `MessageService` | `IMessageService` | `IRepository<MessageEntity>` | ⚠️ **nowhere** |
| `NoteService` | `INoteService` | `IRepository<NoteEntity>` | ⚠️ **nowhere** |
| `PaymentService` | `IPaymentService` | `IRepository<PaymentEntity>`, `IPaymentGateway` | ⚠️ **nowhere** |
| `StripePaymentGateway` | `IPaymentGateway` | *(ctor takes `string apiKey`)* | ⚠️ **nowhere** |
| `ReportingService` | `IReportingService` | `IRepository<ProviderEntity>` | ⚠️ **nowhere** |
| `DeviceTokenService` | `IDeviceTokenService` | `IRepository<DeviceTokenEntity>` | Identity (`Program.cs:25`) |

### ⚠️ Six services are unreachable

`NotificationService`, `MessageService`, `NoteService`, `PaymentService`, `StripePaymentGateway`, and `ReportingService` are **not registered in any service collection and have no HTTP route**. They exist, are unit-tested (`Library.Tests/Services/*`), and correspond to shipped roadmap features F-006 (notifications), F-007 (messaging), F-008 (journal/notes), F-009 (reporting), F-010 (payments) — but none is wired into a running process.

**Inference:** F-006 through F-010 landed the domain layer and its tests without landing the API surface or DI wiring. The mobile app's Messaging and Notifications pages are backed by `SeedDataProvider` (`16-mobile-client.md`), which is consistent with this.

---

## Per-service detail

### `BookingService` (`Library/Services/BookingService.cs`)

| Method | Line | Semantics |
|---|---|---|
| `BookAppointmentAsync(AppointmentEntity)` | `:5` | `InsertAsync` — no validation, no duplicate check, no status transition |
| `UpdateAppointmentAsync(string identifier, AppointmentEntity)` | `:10` | `UpdateByIdentifierAsync` — full document replace |
| `CancelAppointmentAsync(string identifier)` | `:15` | ⚠️ **`DeleteByIdentifierAsync` — a hard delete** |
| `SearchAppointmentAsync(string identifier)` | `:20` | `Find(new BsonDocument("identifier", identifier))` |

⚠️ **"Cancel" physically deletes the row** (`:17`) rather than setting `AppointmentStatus.Cancelled`. The `Cancelled` enum value exists (`Library/Entities/AppointmentStatus.cs:9`) but nothing ever assigns it. Consequences: no cancellation history, and `ReportingService`'s cancelled-appointment count can never be derived from data (see below).

⚠️ `BookAppointmentAsync` never calls `AppointmentEntity.Book()` (`AppointmentEntity.cs:51`), so the status stays `Requested` after booking. The domain method that enforces the `Requested → Booked` transition is **never called anywhere in the solution**.

### `CalendarService` (`Library/Services/CalendarService.cs`)

| Method | Line | Semantics |
|---|---|---|
| `GetAllAppointmentsAsync()` | `:5` | Unfiltered `GetAllAsync()` — full collection scan |
| `GetCalendarAppointmentsAsync(BsonDocument filter)` | `:10` | Caller-supplied filter passthrough |
| `CheckCalendarAvailabilityAsync()` | `:15` | `FindAllAsync(new BsonDocument("day_off", false))` — ⚠️ **not scoped to a provider** |
| `BlockCalendarPeriodAsync(email, start, end)` | `:21` | Inserts one `day_off` appointment per day in a loop |

⚠️ `CheckCalendarAvailabilityAsync` returns every non-day-off appointment **across all providers**. It takes no provider argument. Cross-tenant leak if used directly; it happens not to be reachable via HTTP (Calendar's routes go through `SupportTools.GetThirtyDaysCalendarAvailability` instead).

⚠️ `BlockCalendarPeriodAsync:23` — `(int)(endDate - startDate).TotalDays` **truncates**, so a 7.5-day range blocks 7 days. `:25-38` issues one `InsertAsync` per day (N round-trips, no `InsertManyAsync`), and there is no transaction — a partial failure leaves a half-blocked calendar. Returns hardcoded `true` (`:40`) regardless of outcome.

⚠️ Blocked days are written with `AppointmentStatus.Confirmed` and `EmailCustomer = string.Empty` (`:30,35`), but `AppointmentEntity.EmailCustomer` carries `[EmailAddress]` + `required` (`AppointmentEntity.cs:30-32`). An empty string passes `[EmailAddress]` but the entity is semantically a non-appointment stored in the appointments collection.

### `ProviderService` (`Library/Services/ProviderService.cs`)

Thin repository passthrough. Only real logic: `UpdateProviderAsync:22-25` reads the existing document first and throws `ArgumentException("Provider not found")` if absent — a read-then-write with **no optimistic concurrency**, so concurrent updates silently clobber. Given that `ProviderEntity` embeds the entire appointment list (`05-data-model.md`), two simultaneous bookings against one provider will lose one appointment.

`DeleteProviderAsync:28` hard-deletes. Note the shipped F-011-era `DeactivateProviderCommand` sets `IsActive = false` instead (`EventAndCommands/Commands/Provider/DeactivateProviderCommandHandler.cs:30`) — so soft-delete and hard-delete both exist, with only the soft path reachable via HTTP.

### `CustomerService` (`Library/Services/CustomerService.cs`)

Same shape as `ProviderService`. `UpdateCustomerAsync:17-19` read-then-write, throws `ArgumentException("Customer Not Found")`.

### `ProfessionService` (`Library/Services/ProfessionService.cs`)

⚠️ `GetProfessionCollectionAsync:12` — `(List<ProfessionEntity>)await professionRepository.GetAllAsync()`. A **hard cast** of `IEnumerable<T>` to `List<T>`. It works only because `MongoDbRepository.GetAllAsync` happens to return the result of `ToListAsync()` (`MongoDbRepository.cs:22`). Any change to that return path becomes an `InvalidCastException` at runtime rather than a compile error.

### `ServiceService` (`Library/Services/ServiceService.cs`)

⚠️ **Both methods `throw new NotImplementedException()`** (`:7`, `:12`). It is nonetheless registered as `Scoped` in the Services API (`Services/Extensions/ServiceCollectionExtension.cs:20`). Nothing calls it — the real work happens in `AddServicesToProviderCommandHandler` / `UpdateServicesFromProviderCommandHandler`. Dead code that reads as a live seam.

### `NotificationService` (`Library/Services/NotificationService.cs`)

`SendAsync:5` — misleading name: it **only inserts a Mongo document** (`:8`). There is no email transport, no push dispatch, no Kafka publish. F-006 shipped as "Email or in-app notifications"; only the in-app persistence half exists. `GetForRecipientAsync:11` filters on `recipient_email`. `MarkReadAsync:17` read-modify-write.

### `MessageService` (`Library/Services/MessageService.cs`)

`SendMessageAsync:7` computes a deterministic `ThreadId` by ordinal-case-insensitive sort of the two participant emails joined by `::` (`:11-14`) — a sound design, and `GetThreadAsync:20-23` recomputes it identically. ⚠️ `GetInboxAsync:28` filters only on `recipient_email`, so a user's own sent messages never appear in their inbox. No Kafka involvement despite F-007 being specified as "using the existing Kafka infrastructure".

### `NoteService` (`Library/Services/NoteService.cs`)

The **only service that enforces ownership in the domain layer**: `UpdateAsync:35-36` and `DeleteAsync:49-50` compare `note.ProviderEmail` and throw `UnauthorizedAccessException`. Sets `CreatedAt`/`UpdatedAt` to `DateTime.UtcNow` (`:8-9`, `:39`). Correct per F-008's "visible only to the provider" requirement — though `GetByAppointmentAsync:14` takes `providerEmail` as a filter parameter rather than asserting it, so callers could omit the scoping.

### `PaymentService` (`Library/Services/PaymentService.cs`)

`ChargeAsync:7` sequence: set `Pending` → `CreatePaymentIntentAsync` → `ConfirmPaymentIntentAsync` → set `Succeeded`/`Failed` → `InsertAsync`.

⚠️ **The payment record is persisted only *after* the gateway call completes** (`:20`). If the process dies between `:17` (charge confirmed at Stripe) and `:20` (insert), the customer is charged with **no local record** — an unreconcilable orphan. A `Pending` row should be written before contacting the gateway.

`RefundAsync:30` guards correctly: only `Succeeded` payments (`:36`), must have an intent id (`:39`).

### `StripePaymentGateway` (`Library/Services/StripePaymentGateway.cs`)

⚠️ **`StripeConfiguration.ApiKey` is a Stripe SDK static global**, assigned inside `CreatePaymentIntentAsync:11` only. `ConfirmPaymentIntentAsync:23` and `RefundPaymentIntentAsync:29` **never set it**. A refund issued in a fresh process (or after another component overwrote the static) authenticates with the wrong key or none. Also inherently unsafe under multi-tenant/multi-key use — the static is process-wide.

⚠️ `_intents = new PaymentIntentService()` at `:7` is constructed **before** the API key is ever set.

⚠️ `CreatePaymentIntentAsync:14` — `(long)(amount * 100)` truncates toward zero; `19.999m` becomes `1999` cents. Should round.

⚠️ The constructor takes a raw `string apiKey` (`:5`) — an unregistered primitive dependency, which is why the class cannot be DI-registered without a factory. This is likely why it is unwired.

### `ReportingService` (`Library/Services/ReportingService.cs`)

Reads one provider by email (`:9-11`) and computes everything from the **embedded** `AppointmentEntities` / `ServiceEntities` lists — no separate queries.

⚠️ **`CancelledAppointments:37-38` is derived by subtraction**: `Total − Completed − Booked − Requested`. Since F-012 added `Confirmed` and `Cancelled` to the enum (`AppointmentStatus.cs:8-9`), every **`Confirmed`** appointment is now counted as **cancelled**. The mobile seed data uses `Confirmed` heavily (`MobileApp/Services/SeedDataProvider.cs:20,28`), so this misreports as soon as real confirmed appointments exist. Compounded by `BookingService.CancelAppointmentAsync` hard-deleting cancellations, which makes a correct count impossible from this data.

⚠️ **`EstimatedRevenue:27-30` is `completedCount × sum(all active service fees)`** — it multiplies the count of completed appointments by the *total* of the provider's whole catalogue, rather than summing the fee of the service actually booked. A provider with 3 services at $75/$25/$250 earns $350 per completed appointment by this formula. `AppointmentEntity` has no service reference (`05-data-model.md`), so per-appointment revenue is **not derivable from the current model** — this is a data-model gap, not just an arithmetic slip.

`RetentionRate:19-24` — share of distinct customer emails with >1 appointment, rounded to 2dp. Sound.

### `DeviceTokenService` (`Library/Services/DeviceTokenService.cs`)

`UpsertAsync:5` — read-then-insert-or-update, keyed on email.

⚠️ **`GetByEmailAsync:32-33` calls `repository.GetAllAsync()` and filters in memory.** Every device-token registration loads the entire `device_tokens` collection into the process. O(n) per login. Should be a `FindOneAsync(new BsonDocument("user_email", …))` — the repository already offers it (`IRepository.cs:13`).

⚠️ One token per email (`:15` replaces in place) — a user with both a phone and a tablet keeps only the most recently registered device.

---

## Cross-service conventions

- **Async all the way:** honoured in every service — every method returns `Task`/`Task<T>`, no `.Result`/`.Wait()` inside `Library/Services`. (The one `.Wait()` in the solution is `Profession/Extensions/ServiceCollectionExtensions.cs:24`, outside this layer.)
- **Filters:** built as raw `BsonDocument` literals, either inline or via `SupportTools<T>` helpers. No `Builders<T>.Filter` fluent API in the service layer (only inside `MongoDbRepository`). See `04-data-access.md`.
- **Transaction scope: none.** No `IClientSessionHandle`, no `StartTransaction`, anywhere in the solution. Multi-document writes (appointment + provider's embedded list) are non-atomic by construction.
- **Concurrency: none.** No version/etag field on any entity; all updates are last-write-wins full-document replaces.
- **`XML doc comments`:** `CONSTITUTION.md` §5 requires them on all public service methods. Only `NoteService`-adjacent code and the two migration classes carry them; **most public service methods have no doc comments** — an unmet Definition-of-Done item.
- **Caching:** the services themselves are cache-agnostic. Cache-aside is applied at the *endpoint* level via `CacheAside.GetOrCreateAsync` (`Provider/Program.cs:137`, `Calendar/Program.cs:103`, etc.), not in this layer. See `04-data-access.md` for the `CacheAside` defects.
