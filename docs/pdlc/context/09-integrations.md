# 09 — Integrations

> **⚠️ F-013 delta (2026-08-18, `v0.1.0`) — this file was written 2026-08-15 and has NOT been re-read since.**
>
> **Partially stale.** `KafkaClient` no longer hardcodes the broker: `Kafka/KafkaClient.cs:38` tries `ConnectionStrings:kafka` then `Kafka:BootstrapServers`, falling back to `localhost:9092` (`:18`). Under the AppHost, Kafka and MongoDB run as **Aspire-managed containers**. The substantive finding is unchanged: Kafka still only creates topics; nothing produces or consumes.
>
> `file:line` anchors below may have shifted. Authoritative sources for the change: `docs/pdlc/archive/design/aspire-wiring/ARCHITECTURE.md`, `docs/pdlc/episodes/EPISODE_aspire-wiring_2026-08-17.md`. A full targeted rehydration is queued as the first step of F-018.


Four external systems are referenced. Only **one** is genuinely load-bearing.

| System | SDK | Status | Anchor |
|---|---|---|---|
| MongoDB Atlas | `MongoDB.Driver` 2.25.0 | ✅ **live** — the only real integration | `Library/Repositories/MongoDbRepository.cs` |
| Confluent Kafka | `Confluent.Kafka` (transitive) | ⚠️ **vestigial** — topic creation only, no produce/consume | `Kafka/KafkaClient.cs` |
| Stripe | `Stripe.net` 45.0.0 | ⚠️ **unreachable** — implemented, never registered | `Library/Services/StripePaymentGateway.cs` |
| Firebase Cloud Messaging | `Plugin.Firebase.CloudMessaging` 3.0.0 | ⚠️ **client half only, Android only** | `MobileApp/Services/PushNotificationService.cs` |

---

## Runtime integrations

### 1. MongoDB Atlas — live

**Cluster:** `cluster-agenda-buddy.rqtcadc.mongodb.net`, user `agenda_buddy`, options `retryWrites=true&w=majority`.

**Client construction:** eager, at DI-registration time, one `MongoClient` per service process (`Booking/Extensions/ServiceCollectionExtension.cs:9` → `Booking/Configuration/MongoDbConfiguration.cs:7`). Driver defaults for pool size, timeouts, and read/write concern — **nothing is tuned** (`06-configuration.md`).

**Databases:** `agenda_buddy` (six domain services) and `IdentityDb` (Identity). See `05-data-model.md` for the collection map.

**TLS:** `ServicePointManager.SecurityProtocol = Tls12 | Tls13` is set at the top of five `Program.cs` files and `ConfigurationLoader.cs:7`. ⚠️ This is a .NET Framework API with **no effect on the MongoDB driver's TLS negotiation** on .NET Core — the driver uses `SslStream` with OS defaults. **Inference:** dead ceremony carried over from an older codebase; `Directory.Build.props:16` suppresses the resulting `SYSLIB0014` obsolescence warning solution-wide (`07-build.md`).

**Failure handling:** only Identity handles Mongo unavailability. `IdentityService.IsMongoDown` (`Identity/Services/IdentityService.cs:228-229`) matches `MongoConnectionException or MongoException or TimeoutException` and every public method wraps its repository call to rethrow `ServiceUnavailableException` → HTTP 503 (`Identity/Program.cs:115,126,139,151`). ⚠️ **The six domain services have no equivalent** — a Mongo outage surfaces as an unhandled exception, which in production means no handler at all (`10-error-handling.md`).

⚠️ `IsMongoDown` catches the base `MongoException`, which includes **`MongoWriteException` (duplicate key)** and other logical errors. A duplicate-email registration inside the `try` at `:67-74` would be reported as `503 service_unavailable` rather than `409 Conflict` — except that the explicit `existing is not null` check at `:47` normally catches duplicates first, so this is latent rather than live.

⚠️ **No retry or circuit breaker** beyond the driver's built-in `retryWrites`. No Polly anywhere in the solution.

### 2. Confluent Kafka — vestigial

**The entire integration is 44 lines:** `Kafka/KafkaClient.cs`.

```csharp
public async Task<string> CreateTopicIfNotExist(string topicName)   // :8
{
    var config = new AdminClientConfig { BootstrapServers = "localhost:9092" };  // :12  ⚠️
    using var adminClient = new AdminClientBuilder(config).Build();              // :15
    ...
    await adminClient.CreateTopicsAsync(new[] { topic }, new CreateTopicsOptions
    {
        OperationTimeout = TimeSpan.FromSeconds(5),                              // :27
        RequestTimeout = TimeSpan.FromSeconds(10)                                // :28
    });
    return $"Topic '{topicName}' created successfully";                          // :31
}
```

`IKafkaClient` (`Kafka/IKafkaClient.cs:5`) declares **exactly one method** — `CreateTopicIfNotExist`. There is **no producer, no consumer, no message schema, and no message ever sent**. Topics are created and then never used.

⚠️ **`BootstrapServers` is hardcoded to `localhost:9092`** (`:12`). `CONSTITUTION.md` §9 lists this as a known issue: *"Kafka `BootstrapServers` must be moved to configuration before any non-local deployment."* Still outstanding. `KafkaClient` takes no constructor parameters, so it cannot read configuration without a signature change.

⚠️ **Errors are returned as magic strings, not exceptions.** `:36` returns `"Exception Topic '…' already exists."` and `:40` returns `$"Exception: {e.Message}"`. Callers then string-sniff: `AddProviderCommandHandler.cs:18` treats success as `!kafkaTopic.ToLower().StartsWith("exception")`, and `Provider/Program.cs:121` does the same. A topic name legitimately beginning with "exception" would be misclassified, and the control flow is untypeable.

⚠️ **A topic already existing is treated as failure.** `:35-36` returns an `"Exception …already exists"` string, so `AddProviderCommandHandler.cs:34-46` writes a **Failed** audit event and `Provider/Program.cs:124-126` returns `400 ValidationProblem` with a "Kafka Error". **Failure scenario:** a provider is created, later deleted, then re-created with the same email → the topic already exists → provider creation returns 400 even though the operation is idempotent and fine.

⚠️ **`CreateTopicsException` handling reads `e.Results[0]` unguarded** (`:35`) — an empty `Results` collection throws `IndexOutOfRangeException` from inside the `catch`, escaping the method.

⚠️ **A new `AdminClient` is built and disposed per call** (`:15`). Each construction opens a fresh connection to the broker; the Confluent guidance is to reuse a long-lived admin client.

⚠️ **A 5-second `OperationTimeout` runs synchronously inside the HTTP request** for `POST /api/v1/providers` and `POST /api/v1/customers`. With Kafka down, provider registration blocks for up to 10 s (`RequestTimeout`) and then fails — Kafka is a **hard dependency of user registration** for a feature that does nothing.

#### Topic naming — `Kafka/Support/KafkaHelper.cs`

```csharp
CreateCustomerTopicName(email) => "customer-" + email[..email.IndexOf('@')].ToLower() + "-topic"   // :10
CreateProviderTopicName(email) => "provider-" + email[..email.IndexOf('@')].ToLower() + "-topic"   // :17
```

⚠️ **The domain is discarded, so topic names collide across domains.** `sarah@gmail.com` and `sarah@outlook.com` both map to `provider-sarah-topic`. Two distinct providers would share one topic — a cross-tenant data path if messages were ever published. `CONSTITUTION.md` §3 mandates "Kafka per-provider topics… maintain this convention"; the convention as implemented is not per-provider, it is per-email-localpart.

⚠️ **`email.IndexOf('@')` returns −1 for a malformed address**, making `Substring(0, -1)` throw `ArgumentOutOfRangeException`. Both call sites (`Provider/Program.cs:111`, `Customer/Program.cs:104`) invoke it **before** `MiniValidator` has confirmed the email — actually `Provider/Program.cs:106` validates first, so the guard holds there; `Customer/Program.cs:99` also validates first. Latent, not live.

⚠️ **Topic names are not sanitised for Kafka's legal character set** (`[a-zA-Z0-9._-]`). An email local part containing `+` (e.g. `sarah+work@x.com`) produces an invalid topic name → `CreateTopicsException` → 400 on registration.

⚠️ `KafkaHelper` is a non-static `public class` with only static members (`:5`) — should be `static class`. Its `using Confluent.Kafka;` (`:1`) is unused.

⚠️ **`kafka_topic` is stored on both `ProviderEntity` (`:36`) and `CustomerEntity` (`:31`)** — persisted state for an integration that transports nothing.

#### Broker topology

`docker-compose.override.yml` runs Confluent 7.2.1 with **ZooKeeper** (`:31`) rather than KRaft, plus a Schema Registry (`:56-64`) that no code uses — no Avro/Protobuf schema, no `Confluent.SchemaRegistry` package reference. `kafka-init-topics` (`:71-79`) creates a single topic `agenda-buddy-topic` and produces `compose/data/message.json` into it — a fixture unrelated to the per-provider topics the application creates. See `08-cicd-deploy.md` for the `kafka0` idle container and the `kafka-ui` wrong-port defect.

### 3. Stripe — implemented but unreachable

`Library/Services/StripePaymentGateway.cs` implements `IPaymentGateway` with three operations against the Stripe SDK:

| Method | Line | Stripe call |
|---|---|---|
| `CreatePaymentIntentAsync` | `:9` | `PaymentIntentService.CreateAsync` with `AutomaticPaymentMethods { Enabled = true }` (`:17`) |
| `ConfirmPaymentIntentAsync` | `:23` | `PaymentIntentService.GetAsync`, success if status is `"succeeded"` or `"processing"` (`:26`) |
| `RefundPaymentIntentAsync` | `:29` | `new RefundService().CreateAsync(...)` (`:31-32`) |

⚠️ **Not registered in any service collection.** No `IPaymentGateway` or `IPaymentService` binding exists, no `PaymentEntity` repository is configured (`05-data-model.md`), and no HTTP route reaches payments. F-010 `payment-integration` is marked Shipped; the gateway, the service, the entity, and their unit tests exist, but nothing is wired.

⚠️ **No API key configuration.** The constructor takes a raw `string apiKey` (`:5`) — an unregisterable primitive, and there is **no `Stripe` section in any `appsettings.json`** and no `STRIPE_*` environment variable anywhere. There is no configured way to supply the key. **Inference:** this is why the class was never DI-registered.

⚠️ **`StripeConfiguration.ApiKey` is set only inside `CreatePaymentIntentAsync`** (`:11`) — a process-wide static. `ConfirmPaymentIntentAsync` and `RefundPaymentIntentAsync` never set it, so a refund in a fresh process authenticates with no key. `_intents` is also constructed at `:7`, before the key is ever assigned.

⚠️ **No webhook endpoint.** Stripe's asynchronous lifecycle (`payment_intent.succeeded`, disputes, refund completion) requires a webhook receiver; there is none, and no signature-verification code. `ConfirmPaymentIntentAsync` polls once immediately after creation (`PaymentService.cs:17`) and accepts `"processing"` as success — so a payment that later fails is recorded locally as `Succeeded` forever.

⚠️ **No idempotency key** on `CreateAsync` (`:12-18`). A retried charge double-charges.

⚠️ **Amount conversion truncates:** `(long)(amount * 100)` at `:14`. `decimal 19.999` → `1999` cents.

### 4. Firebase Cloud Messaging — client half, Android only

`MobileApp/Services/PushNotificationService.cs`, guarded by `#if FIREBASE` which `MobileApp.csproj:32` defines **only for `net10.0-android`**.

Flow (`RegisterTokenAsync`, `:27`):
1. `CrossFirebaseCloudMessaging.Current.CheckIfValidAsync()` then `GetTokenAsync()` (`:35-37`).
2. Platform resolved from `DeviceInfo.Platform` (`:38`).
3. `PostTokenAsync(token, platform)` → `POST device-token` on the authenticated `"AgendaBuddyApi"` client (`:60-64`).
4. Server side: `Identity/Program.cs:154-170` validates platform ∈ {`android`, `ios`}, extracts the email from the JWT `sub`/`NameIdentifier` claim, and calls `IDeviceTokenService.UpsertAsync`.

Called from `AuthService.LoginAsync:47` and `RegisterAsync:73`.

⚠️ **iOS is explicitly excluded from the package**: `MobileApp.csproj:48` — `Condition="'$(UseMaui)' == 'true' AND '$(TargetFramework)' != 'net10.0-ios'"`. So `FIREBASE` is never defined for iOS, `RegisterTokenAsync` hits the `#else return;` at `:48`, and **iOS devices never register for push** — even though the server accepts `"ios"` as a platform (`Identity/Program.cs:159`) and `PushNotificationService.cs:38` contains iOS platform-detection code that can never execute.

⚠️ **There is no server-side send path.** Nothing consumes `device_tokens`: no FCM Admin SDK reference, no HTTP call to `fcm.googleapis.com`, no service that reads `IRepository<DeviceTokenEntity>` other than the upsert. Tokens are collected and never used. The `NotificationService` (`Library/Services/NotificationService.cs:5`) only inserts a Mongo document — it does not dispatch anything (`03-services.md`).

⚠️ **No `google-services.json` and no `GoogleService-Info.plist`** in the repo, and `MobileApp/Platforms/Android/` contains only `AndroidManifest.xml`, `MainActivity.cs`, `MainApplication.cs`, and `Resources/values/colors.xml`. Firebase cannot initialise without the config file. `[unknown — outside repo]` whether it is injected at build time — CI does not do so (`.github/workflows/dotnet.yml:118-136`).

⚠️ **All failures are swallowed silently** — `catch (Exception) { return; }` at `:40-43` and `catch (Exception) { }` at `:66-69` with the comment "best-effort; do not crash the app". Correct intent, but there is no logging, so a permanently broken push setup is invisible.

⚠️ `HandleNotificationTap` (`:72`) navigates via `AppShell.NavigateToAppointmentAsync`, but grep shows **no caller** — no `FirebaseCloudMessagingImplementation` notification-tap subscription. The deep-link path is unwired.

The T-002 threat mitigation is honoured: `STATE.md`'s handoff records "Push payload body is PII-free generic text", and no PII appears in this file.

---

## Build-time integrations

- **NuGet** — `api.nuget.org`, cached in CI by `**/*.csproj` hash (`.github/workflows/dotnet.yml:75-81`). No private feed, no `nuget.config`, no lock files (`packages.lock.json` absent) — restores are not reproducible.
- **MAUI workloads** — `dotnet workload install maui-android` / `maui-ios` downloaded per CI run, uncached (`:133`, `:156`).
- **Docker Hub / MCR** — base images `mcr.microsoft.com/dotnet/{aspnet,sdk,runtime}`, `confluentinc/cp-*:7.2.1`, `mongo:7`, `provectuslabs/kafka-ui:latest`. None pinned by digest.
- **GitHub Actions** — `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/cache@v4`, `actions/upload-artifact@v4`, `dorny/paths-filter@v3`, `dorny/test-reporter@v1`. Third-party actions pinned by tag, not SHA.

---

## ⚠️ NOT integrated (negative findings)

These absences are findings in their own right:

- **No service-to-service communication.** Grep for `HttpClient`/`BaseAddress` across all non-`MobileApp` code returns **nothing**. The seven "microservices" never call each other. They integrate solely by sharing the `agenda_buddy` database — a shared-database integration pattern, not a microservices one. There is no service discovery, no service registry, no `IHttpClientFactory` on the server side at all.
- **No API gateway / reverse proxy.** No YARP, no nginx/Envoy config, no Ingress. The `PATH_BASE` env vars in Compose (`:115`, `:139`) anticipate path-prefix routing that no code implements (`06-configuration.md`).
- **No email provider.** F-006 shipped as "**Email** or in-app notifications"; there is no SMTP client, no SendGrid/SES/Postmark SDK, no email template. `NotificationService.SendAsync` writes a document (`03-services.md`). The email half of F-006 does not exist.
- **No distributed cache backend.** `AddDistributedMemoryCache()` in five services (`02-entry-points.md`) — no Redis, no `StackExchange.Redis`, no SQL cache. "Distributed" is in-process.
- **No message queue in use** despite the Kafka infrastructure — no outbox, no saga, no event bus.
- **No secrets manager.** No Key Vault, no AWS Secrets Manager, no Doppler, no `dotnet user-secrets` usage (three projects declare a `UserSecretsId` and none reads secrets). Hence the credential in `appsettings.json` (`06-configuration.md`).
- **No observability backend.** No OpenTelemetry exporter, no Application Insights, no Datadog/Sentry/Seq. See `12-observability.md`.
- **No feature-flag service.**
- **No CDN or object storage** — no file/avatar upload capability anywhere.
- **No calendar interop.** `INTENT.md` positions the product against "generic calendar tools", but there is no iCal/CalDAV export, no Google Calendar or Outlook sync — a notable product-level gap for a scheduling platform.

## Integration-surface sketch

```
                       ┌──────────────────────────────┐
   MobileApp (MAUI) ───┤ ApiBaseUrl (single base URL)  │
     │                 └──────────────────────────────┘
     │                        │  ⚠️ only Identity's 3 auth routes resolve;
     │                        │     all domain paths 404 (01-api-surface.md)
     │                        ▼
     │            ┌───────────────────────┐
     │            │ Identity      :6036   │──┐
     │            └───────────────────────┘  │
     │            ┌───────────────────────┐  │
     │            │ Booking       :6033   │  │
     │            │ Calendar      :6032   │  │   ⚠️ no HTTP between services
     │            │ Customer      :6034   │  ├──▶ MongoDB Atlas
     │            │ Provider      :6030   │  │    (agenda_buddy + IdentityDb)
     │            │ Services      :6031   │  │
     │            │ Profession    :6035   │──┘
     │            └───────────┬───────────┘
     │                        │ CreateTopicIfNotExist only
     │                        ▼
     │            ┌───────────────────────┐
     │            │ Kafka broker :9092    │  ⚠️ topics created, never used
     │            │ (+ ZooKeeper, Schema  │
     │            │  Registry — unused)   │
     │            └───────────────────────┘
     │
     └──▶ Firebase FCM  ⚠️ Android only; no server-side send path
          Stripe        ⚠️ code exists, unregistered, no API key config
```
