# 02 — Entry Points

> **⚠️ F-013 delta (2026-08-18, `v0.1.0`) — this file was written 2026-08-15 and has NOT been re-read since.**
>
> **Stale.** Every one of the seven `Program.cs` files now calls `builder.AddServiceDefaults()` as one of its first statements and `app.MapDefaultEndpoints()` near the end — present exactly once in each (Booking:6,55 · Calendar:7,55 · Customer:6,56 · Provider:7,59 · Services:10,56 · Profession:5,56 · Identity:13,54). Two DI changes matter: `IRequestCollection` is now **Scoped** (as a Singleton consuming a Scoped `IEventStore` it was a captive dependency that DI validation rejected — and validation runs only in `Development`, so six of seven services could not start there); and Profession's seeding moved out of DI-registration-time `.Wait()` into `ProfessionSeedHostedService`.
>
> `file:line` anchors below may have shifted. Authoritative sources for the change: `docs/pdlc/design/aspire-wiring/ARCHITECTURE.md`, `docs/pdlc/episodes/EPISODE_aspire-wiring_2026-08-17.md`. A full targeted rehydration is queued as the first step of F-018.


**Files:** `Booking/Program.cs`, `Calendar/Program.cs`, `Customer/Program.cs`, `Provider/Program.cs`, `Services/Program.cs`, `Profession/Program.cs`, `Identity/Program.cs`, `MobileApp/MauiProgram.cs`.

All seven server entry points are C# **top-level statements** (no `Program` class body) using `WebApplication.CreateBuilder(args)`. There is no `Startup.cs` anywhere. Route registration lives inline in `Program.cs` — see `01-api-surface.md` for the routes themselves; this file documents the **wiring**.

---

## The canonical shape

`Booking/Program.cs` is the representative entry point (`CLAUDE.md` names it as such). Its order:

| Step | Line | Call |
|------|------|------|
| 1 | `:1` | `ServicePointManager.SecurityProtocol = Tls12 \| Tls13` — executes **before** the builder |
| 2 | `:3` | `WebApplication.CreateBuilder(args)` |
| 3 | `:6` | `builder.Services.AddMongoDbRepository(builder.Configuration)` — the per-service DI extension |
| 4 | `:9` | `AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly))` |
| 5 | `:10` | `AddEventStore()` → `AddScoped<IEventStore, EventStore>()` |
| 6 | `:13` | `AddMvcCore()` — registered only for MVC model binders |
| 7 | `:16-18` | Singletons: `IMongoDbConfiguration`, `IKafkaClient`, `IRequestCollection` |
| 8 | `:21-22` | `AddProblemDetails` with a `CustomizeProblemDetails` callback |
| 9 | `:25` | `AddAntiforgery()` |
| 10 | `:28-29` | `AddAgendaBuddyAuthentication()` + `AddAuthorization()` |
| 11 | `:31-32` | `AddEndpointsApiExplorer()` + `AddSwaggerGen()` |
| 12 | `:35` | `builder.Build()` |
| 13 | `:38-80` | **Development-only**: Swagger UI + `UseExceptionHandler` |
| 14 | `:82-86` | `UseAntiforgery` → `UseAuthentication` → `UseAuthorization` → `UseStatusCodePages` → `UseHttpsRedirection` |
| 15 | `:88-166` | Route group + endpoints |
| 16 | `:168` | `app.Run()` |
| 17 | `:171-179` | Local functions `CustomizeProblemDetails`, `GenerateErrorMessage` |

⚠️ **`ServicePointManager` is obsolete (`SYSLIB0014`)** and suppressed solution-wide in `Directory.Build.props:16`. The comment there says removal is deferred pending a `SocketsHttpHandler` migration. It also has no effect on `HttpClient` in .NET Core — **Inference:** this is a .NET Framework habit carried forward and is almost certainly dead code.

⚠️ **`UseHttpsRedirection()` is registered but no HTTPS endpoint is configured** — `appsettings.json` declares only `Http` (HTTP/1) and `gRPC` (h2c) endpoints. **Inference:** in Development the redirect silently no-ops because `launchSettings.json` supplies an `https` profile (`Booking/Properties/launchSettings.json:27` → `https://localhost:8033`), but running via `appsettings.json` alone there is nothing to redirect to.

⚠️ **`UseHttpsRedirection()` is placed after `UseAuthentication`/`UseAuthorization`** (`:83-86`) — requests are authenticated on the insecure channel before the redirect is issued, so bearer tokens are read from plaintext HTTP requests.

---

## Per-service divergence from the canonical shape

This table is the important content of this file: the seven services were copy-pasted and then drifted.

| Concern | Booking | Calendar | Customer | Provider | Services | Profession | Identity |
|---|---|---|---|---|---|---|---|
| `ServicePointManager` prologue | ✅ `:1` | ❌ **absent** | ❌ **absent** | ✅ `:1` | ✅ `:4` | ✅ `:1` | ✅ `:8` |
| `AddDistributedMemoryCache()` | ❌ **absent** | ✅ `:9` | ✅ `:9` | ✅ `:10` | ✅ `:11` | ✅ `:8` | ❌ absent |
| `AddEventStore()` | ✅ `:10` | ✅ `:14` | ✅ `:13` | ✅ `:14` | ✅ `:14` | ✅ `:12` | ❌ absent |
| `AddSingleton<IKafkaClient>` | ✅ `:17` | ❌ absent | ✅ `:20` | ✅ `:21` | ❌ absent | ❌ absent | ❌ absent |
| `AddSingleton<IRequestCollection>` | ✅ `:18` | ✅ `:19` | ✅ `:21` | ✅ `:22` | ✅ `:19` | ✅ `:19` | ❌ absent |
| `AddAntiforgery` / `UseAntiforgery` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ **deliberate** `:87` |
| `AddAuthorization()` | ✅ `:29` | ✅ `:7` | ✅ `:32` | ✅ `:33` | ✅ `:31` | ✅ `:31` | ✅ `:29` |
| `UseHttpsRedirection` | ✅ unconditional | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ **conditional** `:91` |
| `UseHttpLogging` | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ **documented** `:81-86` |
| MSBuild SDK | `.Web` | `.Web` | `.Web` | `.Web` | `.Web` | ⚠️ **`.Worker`** | `.Web` |
| `IDateTimeProvider` registered | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ `:23` |

### Notable individual findings

- ⚠️ **`Booking` has no distributed cache registered** yet is the only service whose `Program.cs` never injects `IDistributedCache` — internally consistent, but it means `CacheAside` is unavailable there while `CONSTITUTION.md` §3 mandates it "for all read-heavy queries". Booking has no read queries at all (`01-api-surface.md`), so the gap is latent rather than live.
- ⚠️ **`Profession.csproj:1` uses `Microsoft.NET.Sdk.Worker`** while `Profession/Program.cs:2` calls `WebApplication.CreateBuilder`. **Inference:** this compiles only because `Swashbuckle.AspNetCore` and `Microsoft.AspNetCore.OpenApi` package references transitively supply the ASP.NET Core framework reference. It is fragile — dropping either package would break the build for a non-obvious reason.
- ⚠️ **`Calendar/Program.cs:7` calls `AddAuthorization()` first, before anything else**, and never calls `AddAuthentication` explicitly — it relies on `AddAgendaBuddyAuthentication()` at `:30`. Ordering is legal but the file reads as if authorization were configured without authentication.
- ⚠️ **`Identity` registers no `IRequestCollection` and no `IEventStore`** — it bypasses the CQRS kernel entirely and calls `IdentityService` directly from the route handlers (`Identity/Program.cs:24`, `:110`). Identity is architecturally a different shape from the other six services; `CONSTITUTION.md` §3's CQRS constraint does not hold there.
- ⚠️ **Identity registers `IDeviceTokenService` (`:25`) but no other service does**, and `Library/Services/DeviceTokenService.cs` lives in the shared Library — so the device-token capability is Identity-only despite being shared code.
- The `Development`-only guard at `:38` wraps **both** Swagger *and* `UseExceptionHandler`. See `10-error-handling.md` — in production there is no exception handler at all.

---

## The `AddMongoDbRepository` extension (one per service)

Each service has its own copy in `<Service>/Extensions/ServiceCollectionExtension{s}.cs`. They differ only in which repositories they register.

| Service | File | Repositories registered | Domain services registered |
|---|---|---|---|
| Booking | `Booking/Extensions/ServiceCollectionExtension.cs:12-22` | `ProviderEntity`, `AppointmentEntity`, `CustomerEntity` | `ProviderService`, `BookingService`, `CustomerService` |
| Calendar | `Calendar/Extensions/ServiceCollectionExtension.cs:11-21` | `ProviderEntity`, `AppointmentEntity`, `CustomerEntity` | `ProviderService`, `CalendarService`, `CustomerService` |
| Customer | `Customer/Extensions/ServiceCollectionExtensions.cs:12-17` | `ProviderEntity`, `CustomerEntity` | `ProviderService`, `CustomerService` |
| Provider | `Provider/Extensions/ServiceCollectionExtension.cs:11-13` | `ProviderEntity` | `ProviderService` |
| Services | `Services/Extensions/ServiceCollectionExtension.cs:11-17` | `ProviderEntity`, `ServiceEntity` | `ProviderService`, `ServiceService` |
| Profession | `Profession/Extensions/ServiceCollectionExtensions.cs:12-18` | `ProfessionEntity`, `ProviderEntity` | `ProfessionService`, `ProviderService` |
| Identity | `Identity/Extensions/ServiceCollectionExtension.cs:12-17` | `CredentialEntity`, `DeviceTokenEntity` | *(none — `IdentityService` registered in `Program.cs`)* |

**Shared mechanics (all seven):**
1. `new MongoDbConfiguration(configuration).MongoClient()` — constructs a `MongoClient` **eagerly at registration time**, outside DI.
2. `client.GetDatabase(configuration.GetSection("MongoDB")["DatabaseName"])` — ⚠️ **root-level** `MongoDB` section (Identity uses `MongoDbSettings`).
3. `AddScoped<IRepository<T>>(_ => new MongoDbRepository<T>(database, collectionName))`.

### ⚠️ Critical: the config-section mismatch

`Booking/Extensions/ServiceCollectionExtension.cs:10,14,18,22` (and the equivalent lines in Calendar, Customer, Provider, Services, Profession) read `configuration.GetSection("MongoDB")`. But `appsettings.json` nests the settings under `LibrarySettings.MongoDB` (`Booking/appsettings.json:21-22`). The **root-level** `MongoDB` key exists only in `appsettings.Development.json` (`Booking/appsettings.Development.json:2`, and the same in all six).

**Failure scenario:** run any of the six domain services with `ASPNETCORE_ENVIRONMENT` set to anything other than `Development`. `appsettings.Development.json` is not loaded, `GetSection("MongoDB")["ConnectionString"]` returns `null`, and `new MongoClient(null!)` at `MongoDbConfiguration.cs:7` throws at startup. **The backend is Development-only.** Cross-reference `06-configuration.md`.

Identity is the exception: `Identity/appsettings.json:21` defines `MongoDbSettings` at the root, which is exactly what `Identity/Configurations/MongoDbConfiguration.cs:7` reads. **Identity is the only service configured correctly for a non-Development environment.**

### ⚠️ Blocking async during DI registration

`Profession/Extensions/ServiceCollectionExtensions.cs:24` calls `SeedDataAsync(database, configuration).Wait()` inside `AddMongoDbRepository`. This:
- blocks the startup thread on a Mongo round-trip,
- violates `CONSTITUTION.md` §2 "async all the way down",
- runs a **write** (`InsertManyAsync`, `:35`) as a side effect of *service registration*, so it also fires in any test that builds the Profession service collection.

It is the only seeding path actually wired into the application — see `05-data-model.md`.

---

## `MobileApp/MauiProgram.cs` — the client entry point

Guarded entirely by `#if MOBILE` (`:1`, `:81`), so the `net10.0` fallback slice compiles it away. `CreateMauiApp()` (`:13`):

| Line | Registration |
|---|---|
| `:17-19` | `UseMauiApp<App>()` + `UseMauiCommunityToolkit()` |
| `:22` | `AddDebug()` logging, `#if DEBUG` only |
| `:26` | `AddTransient<ISecureStorageService, MauiSecureStorageService>()` |
| `:29-33` | Named `HttpClient` **`"AgendaBuddyApi"`** with `AddHttpMessageHandler<JwtDelegatingHandler>()` |
| `:36-39` | Named `HttpClient` **`"AgendaBuddyApiNoAuth"`** (login/register — no token yet) |
| `:42` | `AddSingleton<IUserSessionService, UserSessionService>()` — decoded JWT cached across pages |
| `:45-50` | 6 API services as `Transient` |
| `:51` | `AddSingleton<PushNotificationService>()` |
| `:54-62` | 9 ViewModels as `Transient` |
| `:65-73` | 9 Views as `Transient` |
| `:76` | `AddSingleton<AppShell>()` |

⚠️ **Both HTTP clients fall back to `http://localhost:6036/`** when `ApiBaseUrl` is unset (`:32`, `:38`) — port 6036 is **Identity**. So the hardcoded fallback points every domain call at the auth service. Combined with the missing `api/v1/` prefix, no domain endpoint is reachable. See `01-api-surface.md` and `16-mobile-client.md`.

⚠️ **No resilience handler** on either client — no `AddStandardResilienceHandler()`, no Polly. A single network blip surfaces as a failed page load.
