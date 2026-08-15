# 14 — Glossary

Every term is defined **from this codebase**, with the `file:line` where it is established. Terms whose meaning here diverges from the industry norm are flagged ⚠️.

---

## Domain terms

| Term | Definition in this codebase | Anchor |
|---|---|---|
| **Provider** | An independent service professional — the primary persona. `ProviderEntity` is the aggregate root: it **embeds** its own service catalogue, its full appointment history, and its subscribed-customer email list. Keyed in practice by `email`, not `_id`. | `Library/Entities/ProviderEntity.cs` |
| **Customer** | A client of a provider. `CustomerEntity` holds appointment **identifiers** (strings), not embedded appointment objects — the inverse of the provider's strategy. | `Library/Entities/CustomerEntity.cs:37` |
| **Appointment** | A booked session between one provider and one customer, `[Start, End)` in UTC. Stored **twice**: standalone in `agenda_buddy.appointments` and embedded in the provider document. Nothing keeps the two copies consistent. | `Library/Entities/AppointmentEntity.cs`; `05-data-model.md` |
| **Identifier** | ⚠️ Not `_id`. A separate GUID string on `AppointmentEntity` (`identifier`) that is the **business key** every update and delete path uses. `_id` is used only by `GetByIdAsync`/`UpdateAsync`. Two parallel key systems. | `AppointmentEntity.cs:24`; `IRepository.cs:9,11` |
| **Day off** | A boolean on `AppointmentEntity`. Provider unavailability is modelled as a **synthetic appointment** with `DayOff = true` and `EmailCustomer = string.Empty`, written one row per day. | `AppointmentEntity.cs:49`; `CalendarService.cs:27-37` |
| **Availability** | ⚠️ **Not** a provider-configured schedule. A computed list of free hourly slots over the next 30 days, hardcoded 09:00–19:00 for **every** provider, with an undocumented 4-hour same-day lead time. No availability field exists on any entity, despite F-005 `provider-availability-schedule` being marked Shipped. | `Library/Tools/SupportTools.cs:37-78` |
| **Appointment status** | `Requested → Booked → Completed`, plus `Confirmed` and `Cancelled` added by F-012. Persisted as an **integer**. ⚠️ `Cancelled` is never assigned by any code path — cancel is a hard delete. `Confirmed` has no transition method. | `Library/Entities/AppointmentStatus.cs`; `03-services.md` |
| **Appointment description** | A denormalised copy of the status's `[Description]` attribute text, stored alongside the enum. No invariant ties the two, so they can disagree. | `AppointmentEntity.cs:45-47` |
| **Service** | A priced offering in a provider's catalogue (`name`, `description`, `fee`, `feeType`, `isActive`). ⚠️ Only ever persisted **embedded** in `ProviderEntity`; the configured standalone `services` collection is unused. ⚠️ An `AppointmentEntity` carries **no reference to the service booked**. | `Library/Entities/ServiceEntity.cs`; `05-data-model.md` |
| **Fee type** | `Hourly` \| `Fixed` \| `Subscription`. | `ServiceEntity.cs:32-36` |
| **Profession** | Reference data — a `{ _id, name }` catalogue seeded at Profession-service startup. ⚠️ Nothing links a provider to a profession, despite F-002 describing that step. | `Library/Entities/ProfessionEntity.cs`; `Library/Data/ProfessionSeedData.cs` |
| **Note** | A provider's private, per-appointment session note. The **only** entity whose ownership is enforced in the domain layer. The most sensitive data in the product, stored unencrypted. | `Library/Entities/NoteEntity.cs`; `Library/Services/NoteService.cs:35-36,49-50` |
| **Notification** | ⚠️ An **in-app Mongo document only**. `NotificationService.SendAsync` inserts a row and dispatches nothing — no email, no push. F-006 shipped as "Email or in-app". | `Library/Services/NotificationService.cs:5-9` |
| **Message** | A provider↔customer message, grouped by `thread_id` = the two participant emails ordinal-case-insensitively sorted and joined by `::`. ⚠️ Persisted in MongoDB, **not** Kafka, despite F-007's stated design. | `Library/Services/MessageService.cs:11-14` |
| **Thread ID** | The deterministic participant-pair key above. Recomputed identically on read. | `MessageService.cs:14,23` |
| **Payment** | A Stripe charge record for one appointment. ⚠️ Written **after** the gateway call succeeds, so a crash mid-flow leaves a charged customer with no local record. `amount` is a `decimal` serialised to BSON `Double`. | `Library/Services/PaymentService.cs:7-22`; `05-data-model.md` |
| **Provider report** | A computed, non-persisted DTO: booking counts, estimated revenue, unique customers, retention rate. ⚠️ Two of its fields are miscalculated (`CancelledAppointments` counts `Confirmed`; `EstimatedRevenue` multiplies by the whole catalogue). | `Library/Entities/ProviderReport.cs`; `Library/Services/ReportingService.cs:27-38` |
| **Retention rate** | Percentage of distinct customer emails with more than one appointment, rounded to 2dp. | `ReportingService.cs:19-24` |
| **Credential** | The auth record: email, BCrypt-12 password hash, single role, `must_reset_password`, and one embedded refresh-token hash. Lives in a **separate database** (`IdentityDb`) from the domain data. | `Library/Entities/CredentialEntity.cs` |
| **Role** | `"Provider"` or `"Customer"` — one per account in v1. Minted into the JWT and validated. ⚠️ **Never used to authorize anything** — `OwnershipGuard.AssertRole` has no callers. | `IdentityService.cs:20,203`; `13-security.md` |
| **Device token** | An FCM registration token, one row per user email. ⚠️ Collected and never used — there is no server-side push-send path. | `Library/Entities/DeviceTokenEntity.cs`; `09-integrations.md` |
| **Kafka topic** | A per-user topic name derived as `provider-<email-localpart>-topic` / `customer-<email-localpart>-topic`, persisted on the provider and customer documents. ⚠️ Created at registration and **never published to or consumed from**. The domain is discarded, so `sarah@gmail.com` and `sarah@outlook.com` collide. | `Kafka/Support/KafkaHelper.cs:10,17` |

---

## Platform / internal terms

| Term | Definition in this codebase | Anchor |
|---|---|---|
| **`IRepository<T>`** | The single generic data-access contract: 11 members, `BsonDocument`-filter based. ⚠️ `GetByIdAsync` and `Find` declare non-nullable returns but can yield `null`. | `Library/Repositories/IRepository.cs` |
| **`MongoDbRepository<T>`** | The only implementation. Full-document reads and `ReplaceOneAsync` writes; no projections, no partial updates, no indexes. | `Library/Repositories/MongoDbRepository.cs` |
| **`SupportTools<TEntity>`** | Static `BsonDocument` filter builders. ⚠️ The generic parameter is unused by the filter methods — it is call-site documentation only. Also hosts the availability algorithm. | `Library/Tools/SupportTools.cs` |
| **`CacheAside`** | An `IDistributedCache` extension implementing get-or-create with a 5-minute absolute TTL. ⚠️ Backed by `AddDistributedMemoryCache()` (in-process), guarded by **one static process-wide semaphore**, returns `null` on a 500 ms lock timeout, and its double-check is dead code. No key is ever invalidated on write. | `Library/Tools/CacheAside.cs`; `04-data-access.md` |
| **`EnumHelper<TEnum>`** | Reflection-based `[Description]` ↔ enum conversion. ⚠️ `SaveEnumDescription` computes a value and discards it — dead method. | `Library/Tools/EnumHelper.cs:42-45` |
| **`IDateTimeProvider`** | A clock abstraction with a `SystemDateTimeProvider` implementation. ⚠️ Registered **only in Identity**; every other service calls `DateTime.UtcNow`/`DateTime.Now` statically, which is why only Identity has time-dependent tests. | `Library/Tools/IDateTimeProvider.cs`; `Identity/Program.cs:23` |
| **`OwnershipGuard`** | Static IDOR defence comparing the JWT `NameIdentifier` claim to an entity's email, case-insensitively. Throws `ForbiddenException`. | `Library.ServerAuth/Tools/OwnershipGuard.cs` |
| **`ForbiddenException`** | The authorization failure signal. ⚠️ Its `StatusCode => 403` property is **never read**; correct 403s depend on each endpoint hand-writing a `try/catch`, repeated at 8 sites. | `OwnershipGuard.cs:28-37`; `10-error-handling.md` |
| **`AddAgendaBuddyAuthentication()`** | The shared JWT-bearer registration: RS256-only, issuer-validated, zero clock skew, audience validation off. Fails fast if `JWT_PUBLIC_KEY` is absent. ⚠️ Calls `services.BuildServiceProvider()` internally (ASP0000). | `Library.ServerAuth/AuthenticationExtensions.cs` |
| **`AddMongoDbRepository()`** | ⚠️ **Six different extension methods sharing one name**, one per service, each registering that service's repositories. All read the **root-level** `MongoDB` config section, which exists only in `appsettings.Development.json` — the reason the backend is Development-only. | `Booking/Extensions/ServiceCollectionExtension.cs`; `06-configuration.md` |
| **`IRequestCollection` / `RequestCollection`** | ⚠️ The de facto CQRS dispatcher. Six per-service copies that **manually `new` up** command/query handlers and call `.Handle()` directly, because handlers take domain data as constructor parameters and cannot be resolved by MediatR. | `Booking/Requests/RequestCollection.cs`; `15-cqrs-and-messaging.md` |
| **`EventsHelper` / `EventHelper`** | ⚠️ A pure pass-through static layer between the endpoint and `IRequestCollection` — no validation, mapping, logging, or error handling. Six copies, and the class name itself is inconsistent (`EventsHelper` in 4 services, `EventHelper` in 2). Its methods are named `…Event` but publish nothing and return strings. | `Booking/Events/EventsHelper.cs` |
| **Command** | An `IRequest<T>` write DTO in `EventAndCommands/Commands/`. ⚠️ Its properties are frequently ignored — the handler reads its constructor state instead. | `EventAndCommands/Commands/Booking/BookAppointmentCommand.cs` |
| **Query** | An `IRequest<T>` read DTO in `EventAndCommands/Queries/`. ⚠️ Reads the same collections through the same services as commands — no separate read model. | `EventAndCommands/Queries/Provider/GetProvidersQuery.cs` |
| **Event** *(two meanings — ⚠️ collision)* | **(a)** An `INotification` DTO in `EventAndCommands/Events/` — 19 of them, published by every handler, with **zero `INotificationHandler`s** in the solution, so every publish is a no-op. **(b)** `EventAndCommands.Persitency.Event` — the persisted audit record (`timestamp`, `status`, `type`, `data`). Unrelated types, one word. | `Events/Booking/BookAppointmentEvent.cs`; `Persitency/Event.cs` |
| **`EventStore`** | The audit-trail writer. Every command **and query** handler inserts a `"Success"`/`"Failed"` document with the JSON-serialised payload. ⚠️ Constructs its own `MongoClient` per scope; `GetEventsAsync` filters on the event's own `_id`, so it can never return an aggregate's stream. | `EventAndCommands/Persitency/EventStore.cs`; `15-cqrs-and-messaging.md` |
| **`Persitency`** | ⚠️ A **known misspelling** of "Persistency" in the `EventAndCommands` directory and namespace. `CONSTITUTION.md` §9 forbids renaming it until a dedicated refactor. | `EventAndCommands/Persitency/` |
| **`ConfigurationLoader`** | ⚠️ **Dead code.** The only reader of the `LibrarySettings.MongoDB` config shape, referenced solely by its own unit test. Builds a private `ConfigurationBuilder` from the assembly location. | `EventAndCommands/ConfigurationLoader.cs`; `06-configuration.md` |
| **`LibrarySettings` / `MongoDbSettings`** | POCOs for the nested Mongo config. ⚠️ Constructed only by `ConfigurationLoader`; **no `IOptions<T>` binding exists anywhere** in the solution. Note `MongoDbSettings` is also the name of Identity's *config section*, which does not use this class. | `EventAndCommands/LibrarySettings.cs` |
| **`ProblemDetailsServiceEndpointFilter`** | An `IEndpointFilter` re-wrapping `ProblemHttpResult`/`ProblemDetails` so `IProblemDetailsService` (and the `requestId` extension) applies. ⚠️ Does **not** match `ValidationProblem`, which is the most common error path — so most 400s carry no `requestId`. Six duplicated copies, all `[ExcludeFromCodeCoverage]`. | `Booking/Extensions/ProblemDetailsServiceEndpointFilter.cs` |
| **`requestId`** | A ProblemDetails extension member set from `Activity.Current?.Id ?? TraceIdentifier`. ⚠️ Returned to clients but stored nowhere — no log sink, no trace backend, so it cannot be looked up. | `Booking/Program.cs:171-174`; `12-observability.md` |
| **`AcceptsJson()`** | Decides between the ProblemDetails and plain-text error branches. ⚠️ Returns `false` when no `Accept` header is present, so header-less clients get `text/plain` errors. | `Booking/Extensions/HttpContextExtensions.cs:13,40` |
| **`IKafkaClient` / `KafkaClient`** | ⚠️ A one-method interface — `CreateTopicIfNotExist` only. Hardcodes `BootstrapServers = "localhost:9092"`, returns errors as strings beginning `"Exception"`, and treats an already-existing topic as failure. | `Kafka/KafkaClient.cs` |
| **`"exception"` prefix** | ⚠️ The solution's failure-signalling convention: handlers and `KafkaClient` return a `string` whose lowercase form starts with `"exception"` to mean failure, which six endpoints test with `.StartsWith("exception")`. Coexists with `null!` and `string.Empty` as two further failure encodings. | `Booking/Program.cs:110`; `10-error-handling.md` |
| **`ISecureStorageService`** | The mobile client's secure-storage abstraction (`MauiSecureStorageService` → Keychain / Android Keystore). Holds `"jwt"` and `"refresh_token"`. | `MobileApp/Infrastructure/ISecureStorageService.cs` |
| **`JwtDelegatingHandler`** | Mobile `DelegatingHandler` attaching the bearer token and purging it on 401. ⚠️ Exposes a **static** `UnauthorizedAccess` event that is subscribed and never unsubscribed. | `MobileApp/Infrastructure/JwtDelegatingHandler.cs` |
| **`IUserSessionService`** | Mobile singleton caching the **client-side, unverified** JWT payload decode (`Email`, `Role`, `IsProvider`, `IsCustomer`). | `MobileApp/Services/UserSessionService.cs` |
| **`SeedDataProvider`** | ⚠️ Hardcoded mobile fixtures substituted whenever an API call returns zero results **or** throws `HttpRequestException`. It is what the app actually renders, because every domain route 404s. | `MobileApp/Services/SeedDataProvider.cs`; `16-mobile-client.md` |
| **`"AgendaBuddyApi"` / `"AgendaBuddyApiNoAuth"`** | The two named mobile `HttpClient`s — with and without the JWT handler. Both fall back to `http://localhost:6036/` (Identity, plaintext). | `MobileApp/MauiProgram.cs:30,36` |
| **`ApiBaseUrl`** | ⚠️ The mobile client's single base address for a backend that binds seven ports with no gateway. Configured as `https://localhost` / `https://localhost:5001` — neither of which any service serves. | `MobileApp/appsettings.json:2` |

---

## Build / tooling terms

| Term | Definition in this codebase | Anchor |
|---|---|---|
| **`MobileWorkloads`** | MSBuild switch. `false` collapses `MobileApp` to a plain `net10.0` library so it builds and tests without MAUI workloads. Required by CI and by any `dotnet build` on a machine without the workloads. | `MobileApp/MobileApp.csproj:9,17` |
| **`MobilePlatform`** | MSBuild switch (`android` \| `ios` \| empty) restricting the build to one mobile TFM without cascading a TFM override to `Library`. | `MobileApp.csproj:11-20` |
| **`MOBILE`** | Compile constant defined only for `net10.0-android` and `net10.0-ios`. Guards `MauiProgram.cs` and `AppShell.xaml.cs` — ⚠️ so the tested `net10.0` assembly contains no DI wiring. | `MobileApp.csproj:31` |
| **`FIREBASE`** | Compile constant defined only for `net10.0-android`. ⚠️ Consequently **iOS never registers for push**. | `MobileApp.csproj:32`; `PushNotificationService.cs:32-49` |
| **`SYSLIB0014`** | Suppressed obsolescence warning for `ServicePointManager`. ⚠️ The suppressed calls are inert on .NET Core — the suppression protects dead code. | `Directory.Build.props:11-16` |
| **`ASPDEPR002`** | Suppressed deprecation warning for `WithOpenApi`, which all seven services still call. | `Directory.Build.props:13-16` |
| **Transitive CVE pin** | An explicit `PackageReference` in `Directory.Build.props` added solely to force a vulnerable transitive package forward — `Snappier`, `SharpCompress`, `Newtonsoft.Json`, `Microsoft.OpenApi`. ⚠️ Three of four exist because `MongoDB.Driver` is held at 2.25.0. | `Directory.Build.props:18-28` |
| **`GlobalUsings.cs`** | Per-project global using directives (in place of MSBuild `<Using>` items). ⚠️ `Booking/GlobalUsings.cs:25` has two directives on one line. | `Booking/GlobalUsings.cs` |
| **`[ExcludeFromCodeCoverage]`** | Applied to **58 production files** — every entity, every `RequestCollection`, every endpoint filter, every command/query DTO. ⚠️ Reported coverage is computed over a deliberately narrowed denominator. | `11-testing.md` |
| **`Category=Acceptance`** | The only xUnit trait in the solution, on `AuthAcceptanceTests`. ⚠️ Excluded by `--filter "Category!=Acceptance"` in **both** CI test jobs, so its 7 tests never run. | `MobileApp.Tests/Acceptance/AuthAcceptanceTests.cs:8` |

---

## Workflow / deployment terms

| Term | Definition in this codebase | Anchor |
|---|---|---|
| **`changes` job** | The `dorny/paths-filter` gate computing `api` / `mobile` / `mobile-tests` / `library` booleans. ⚠️ `library` is never consumed, and edits to `global.json`, any `Dockerfile`, `docker-compose*.yml`, `scripts/`, or the workflow itself trigger **no job at all**. | `.github/workflows/dotnet.yml:17-57` |
| **`kafka-init-topics`** | A one-shot Compose container creating `agenda-buddy-topic` and producing a fixture message into it — unrelated to the per-user topics the application creates. | `docker-compose.override.yml:71-79` |
| **`kafka0`** | ⚠️ A Compose container running `tail -f /dev/null` alongside the real `broker`. Does nothing. | `docker-compose.override.yml:66-69` |
| **`events` / `kafka-library` / `common-library`** | ⚠️ Compose "services" built from **class-library** projects with no `ENTRYPOINT`. Their Dockerfiles publish `net10.0` output onto a `dotnet/runtime:8.0` base — they cannot run. | `docker-compose.yml:58-76`; `08-cicd-deploy.md` |
| **`PATH_BASE`** | ⚠️ A Compose environment variable set for `events` and `identity`. **No `UsePathBase` call exists in any `Program.cs`** — the path-prefix routing a gateway would need is configured but unimplemented. | `docker-compose.override.yml:115,139` |
| **`DOCKER_REGISTRY`** | Compose image-prefix variable, defaulting to empty via `${DOCKER_REGISTRY-}`. No workflow ever pushes an image. | `docker-compose.yml:61,67,73,81` |
| **`seed-mongo.sh`** | The only working seed path. ⚠️ Imports providers into `ProviderDb` and customers into `CustomerDb` — databases **no service reads** (all six read `agenda_buddy`). Only the `IdentityDb.credentials` import lands correctly. Uses `--drop`, so it is destructive. | `scripts/seed/seed-mongo.sh:14,22,30`; `05-data-model.md` |
| **`DevPass123!`** | ⚠️ The committed default password for the six seeded development accounts, literal in the shipped `Library` assembly. | `Library/Data/DevelopmentSeedData.cs:151` |
| **`F-NNN`** | A PDLC roadmap feature id. F-001…F-012 shipped; **F-013 `aspire-wiring`** is the active claim this catalog was hydrated for. | `docs/pdlc/memory/ROADMAP.md` |
| **PDLC memory bank** | `docs/pdlc/memory/` — CONSTITUTION (how the project is built), INTENT (why), OVERVIEW, DECISIONS, ROADMAP, STATE, METRICS, CHANGELOG, DEPLOYMENTS. The **high-level** counterpart to this `docs/pdlc/context/` catalog. | `CLAUDE.md` |

---

## Terms used in the memory bank that do not match the code

Worth knowing when reading `CONSTITUTION.md`, `CLAUDE.md`, or `INTENT.md` against this catalog:

| Documented term | Actual state |
|---|---|
| ".NET 8" | ⚠️ The code is **.NET 10** (`global.json:3`). F-011 shipped the upgrade; the docs were not updated. |
| "CQRS via MediatR" | ⚠️ Folder separation only. `mediator.Send` is never called; handlers are hand-constructed (`15-cqrs-and-messaging.md`). |
| "Event-driven microservices" | ⚠️ 19 `INotification` types, **zero handlers**. Services never call each other; they share one database. |
| "Kafka provides async provider-to-customer messaging" | ⚠️ Topics created, never published to or consumed from. Messaging is MongoDB-based and unreachable. |
| "Event sourcing (audit trail)" | ⚠️ Append-only audit log. No aggregate id, no replay, no actor; `GetEventsAsync` cannot return a stream. |
| "Cache-aside pattern … `IDistributedCache`" | ⚠️ Backed by `AddDistributedMemoryCache()` — per-process memory, no invalidation. |
| "Repository pattern only" | ⚠️ Violated by `EventStore`, `Profession`'s DI-time seeding, and every `ServiceCollectionExtension`. |
| "Six independent microservices" | ⚠️ **Seven** ASP.NET services (Identity is the seventh, added by F-001) plus one MAUI client. |
| `INTENT.md` "Out of Scope": mobile app, payments, journal/notes, messaging | ⚠️ All four shipped (F-006–F-012). |
