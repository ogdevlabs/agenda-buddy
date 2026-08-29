# Agenda Buddy

Agenda Buddy is a scheduling and appointment management platform for independent service providers (fitness coaches, tutors, therapists, software instructors, etc.) who need to manage clients, services, and appointments in one place. It is built as event-driven microservices on .NET 10, orchestrated locally with .NET Aspire.

## Tech Stack

- **Language:** C# / .NET 10 (`net10.0`)
- **Framework:** ASP.NET Core Minimal APIs (one service per domain)
- **Orchestration:** .NET Aspire — `Aspire.AppHost.Sdk` pinned at 13.4.6, `Aspire.Hosting.*` NuGet packages at 13.5.3 (bumped by Dependabot 2026-08-26, PR #67 — the `Sdk` tag itself is a separate, still-13.4.6 pin), **hosting-only** — `AgendaBuddy.AppHost` + `AgendaBuddy.ServiceDefaults`
- **Database:** MongoDB (MongoDB.Driver **pinned at 2.25.0** — see the Aspire caveat below)
- **Messaging:** Kafka (Confluent) + MediatR (CQRS)
- **Result/validation:** FluentResults (`Result`/`Result<T>` returned by every Booking/Calendar/Customer/Provider/Services/Profession command/query handler, replacing a string-sniffed `"exception"`-prefix convention), Validot (declarative `Specification<T>` DSL — partially migrated, only 3 of Booking's 10 routes use it), GuardClauses (package id `GuardClauses`, not Ardalis — `GuardClause.ArgumentIsNotNull`). Mapster is ADR-049-approved for this line but has no call sites yet
- **Caching:** IDistributedCache (cache-aside pattern, 5-min TTL)
- **Observability:** OpenTelemetry traces/metrics/logs via ServiceDefaults, exported to the Aspire dashboard
- **Testing:** xUnit — **1022 tests total**, in **three separate suites** that no single command runs: **547** across backend test projects (`agenda-buddy-backend.slnf`; the slnf also carries `AgendaBuddy.Gateway` itself, a non-test project), **310** in `AgendaBuddy.IntegrationTests` (real services — including the Gateway — over HTTP against a MongoDB Testcontainer — needs a container runtime), and **165** in `AgendaBuddy.MobileApp.Tests` (158 passing, 7 skipped — the skip is deliberate: `AuthAcceptanceTests` needs a live Identity service reachable at `IDENTITY_BASE_URL`/`localhost:6036` and gracefully `Skip.If`s when nothing's listening, not a bug)
- **Infrastructure:** Aspire AppHost (primary local) · Docker + Docker Compose (legacy fallback) · GitHub Actions CI
- **Security scanning (F-017):** every PR runs `dotnet list package --vulnerable` (dependency audit) and `gitleaks` (secret scan, full PR diff history) unconditionally — see `.gitleaks.toml` and the `security-scan` CI job. Every PR touching a service/`.csproj`/Compose file also builds and Trivy-scans a container image for each of the 7 remaining services via `docker-build-and-scan` — no Dockerfile involved, see the caveat below

> **Aspire caveat:** do **not** add `Aspire.MongoDB.Driver`. It requires MongoDB.Driver ≥ 3.9.0 against the pinned 2.25.0 and fails restore with `NU1605`. The project registers `AddSingleton<IMongoClient>` with a custom `MongoHealthCheck` instead (ADR-013). There is no Aspire workload to install.

> **Container caveat (F-017):** `docker-build-and-scan` builds each service's container image with **.NET SDK container support** (`dotnet publish -t:PublishContainer`) — it never reads hand-written per-service Dockerfiles. Those Dockerfiles serve only the already-broken legacy `docker compose up` path (1 of 7 services wired in) and are not built, scanned, or otherwise exercised by CI. A generalized structural test (`AgendaBuddy.AppHost.Tests/DockerAndComposeHygieneTest.cs`) fails on any Dockerfile with a runtime/SDK major-version mismatch, repo-wide, so a `Library`/`Kafka`/`EventAndCommands`-shaped defect can't recur under a different filename.

> **Naming convention (F-020, 2026-08-27):** every one of this solution's **47 projects** carries the `AgendaBuddy.` prefix — folder, `.csproj` file name, solution reference, and internal C# namespace, matching the pattern `AgendaBuddy.AppHost`/`ServiceDefaults`/`IntegrationTests` set at F-013. There is no unprefixed project left anywhere in `agenda-buddy.sln`. `AgendaBuddy.EventAndCommands`/`AgendaBuddy.EventsAndCommands.Tests` deliberately keep their pre-existing `Event`/`Events` singular/plural inconsistency — F-020 renamed, it did not also fix unrelated naming bugs.

## Project Structure

- `AgendaBuddy.AppHost/` — Aspire composition root: declares MongoDB + Kafka containers and all 7 service projects (8 with the Gateway)
- `AgendaBuddy.ServiceDefaults/` — shared cross-cutting setup referenced by every service (OpenTelemetry, health/liveness, service discovery, HTTP resilience, `PiiRedactingProcessor`)
- `AgendaBuddy.Library/` — shared domain entities, `IRepository<T>` / `MongoDbRepository<T>`, all domain services, tools (CacheAside, EnumHelper, SupportTools), `MongoConnectionResolver`, `MongoHealthCheck`, profession seed data. **No Dockerfile** (F-017, same reason as `AgendaBuddy.EventAndCommands`)
- `AgendaBuddy.Library.ServerAuth/` — server-side auth primitives (JWT validation, ownership guards)
- `AgendaBuddy.EventAndCommands/` — the CQRS kernel's **infrastructure only** as of F-020: `EventStore`/`IEventStore` persistence, `Event`/`QueryAudit` types, `ConfigurationLoader.cs`. **Holds zero command/query handler implementations** — every service's handlers now live in that service's own `*.Core` project (Booking since F-019; Calendar/Customer/Provider/Services/Profession since F-020; Identity never had any here). **No Dockerfile** (F-017 — was a class library with no entry point)
- `AgendaBuddy.Kafka/` — `KafkaClient` for topic creation (Confluent.Kafka); broker address is configuration-driven. **No Dockerfile**
- **Six of seven domain services follow a 4-project Clean Architecture split** (Booking pioneered it in F-019; Calendar, Customer, Provider, Services, Profession got it in F-020): `<Service>.Api` (thin — endpoint/DI wiring only), `<Service>.Core` (MediatR command/query handlers), `<Service>.Domain` (commands/queries/DTOs, its own in-repo `DataResponse<T>` — **not shared across services**, deliberately, see `docs/pdlc/design/api-refactor-rollout/ARCHITECTURE.md` §3), `<Service>.Infrastructure` (deliberately empty for all 6 — YAGNI, nothing has needed it yet). E.g. `AgendaBuddy.Calendar.Api/Core/Domain/Infrastructure`, `AgendaBuddy.Customer.Api/Core/Domain/Infrastructure`, etc. Each service's `<Service>.Tests` stays one project (not split per new project).
- `AgendaBuddy.Identity/` — the seventh domain service, **deliberately excluded from the Clean Architecture split** (Discover 2026-08-27, F-020): it never adopted the `RequestCollection`/CQRS/EventStore shape the other 6 share, dispatches via direct `IdentityService` method calls with zero MediatR, and has its own F-021-era exception taxonomy. Migrating it is a different, larger, unvalidated feature, not this program's.
- `AgendaBuddy.Gateway/` — the eighth process (F-015). A thin YARP reverse proxy in front of the seven services — `AgendaBuddy.MobileApp`'s **only** configured base address. No business logic, no auth validation (JWT passthrough only), no path rewriting. Builds its route/cluster table programmatically from the same Aspire service-discovery config keys (`services__<name>__http__0`) every service already reads — an explicit `api/v1/{service}/**` allowlist, never a catch-all forward (ADR/threat T-302)
- `AgendaBuddy.MobileApp/` — .NET MAUI client, reaching the real backend through the Gateway only. **Deliberately excluded from `agenda-buddy-backend.slnf`** — covered by three dedicated CI jobs instead (`build-android`, `build-ios` on a macOS runner, and `build-mobile-tests`). Its 165 tests (158 passing, 7 skipped) run under `/p:MobileWorkloads=false`
- `*.Tests/` projects mirror the service they test, all now `AgendaBuddy.*`-prefixed (e.g. `AgendaBuddy.Library.Tests/`, `AgendaBuddy.EventsAndCommands.Tests/`)
- `compose/` — Docker Compose data fixtures

## Development

- **Install:** `dotnet restore`
- **Dev server (primary):** `dotnet run --project AgendaBuddy.AppHost` — starts MongoDB, Kafka, all 7 services, and the Gateway — 8 processes total
- **Dev server (legacy):** `docker compose -f docker-compose.yml -f docker-compose.override.yml up -d`
- **Build:** `dotnet build --no-restore` (or `dotnet build agenda-buddy.sln` for the full solution, including `AgendaBuddy.MobileApp`/`AgendaBuddy.IntegrationTests` which the backend slnf excludes)
- **Test (backend, 547 tests):** `dotnet test agenda-buddy-backend.slnf --collect:"XPlat Code Coverage"` — use the solution filter, not the full solution
- **Test (integration, 310 tests):** `dotnet test AgendaBuddy.IntegrationTests/AgendaBuddy.IntegrationTests.csproj` — ⚠️ a **separate command**. `AgendaBuddy.IntegrationTests` is deliberately excluded from the slnf (ADR-031) so the unit gate stays Docker-free, which means the backend command above **does not run it**. Needs a container runtime; `export PATH="$HOME/.rd/bin:$PATH"` first under Rancher Desktop. It has a `ProjectReference` to `AgendaBuddy.MobileApp.csproj` (F-015, for `MobileClientRouteResolutionTest`) — always restore/build with `/p:MobileWorkloads=false`, or it pulls in MobileApp's default android/ios TargetFrameworks and fails with `NETSDK1147` on a machine with no MAUI workloads
- **Test (mobile, 165 tests):** `dotnet test AgendaBuddy.MobileApp.Tests/AgendaBuddy.MobileApp.Tests.csproj /p:MobileWorkloads=false` (158 passing, 7 skipped)
- **Format:** `dotnet format agenda-buddy-backend.slnf` — `.editorconfig` (F-018-T03) encodes the project's actual conventions (4-space indent, Allman braces, file-scoped namespaces, `var` everywhere, no `this.`). `build-and-test` runs `dotnet format --verify-no-changes` as a CI gate
- **Regenerate the OpenAPI specs:** `./scripts/generate-openapi.sh [Service…]` → `docs/api/openapi/`. Its `project_dir()` function maps each service's display name to its actual (now `AgendaBuddy.*`-prefixed) project folder wherever the two differ — check this mapping first whenever a service is renamed; F-020's own migration tasks forgot it 5 times before catching all of them. The committed `docs/api/openapi/*.json` files are sourced from a byte-deterministic mechanism (F-018-T16 — `AgendaBuddy.IntegrationTests/OpenApi/OpenApiSpecGenerator.cs`, resolves `ISwaggerProvider` from DI directly, no HTTP call, no container) and are diffed against a live regeneration by `OpenApiSpecDriftTest` (F-018-T17) inside the `integration` CI job
- **Run the app + iOS simulator:** `./scripts/run-ios.sh` — starts the AppHost, discovers the dynamic ports (including the Gateway's, injected as `MAUI_API_BASE_URL`), boots a simulator, launches `AgendaBuddy.MobileApp`. Its `SERVICES`/`GATEWAY` arrays must list the actual `AgendaBuddy.*`-prefixed project folder names — see `bruno/agenda-buddy/` for a collection that also hits the real services directly, bypassing the Gateway
- **Stop:** `Ctrl-C` on the AppHost (legacy: `docker compose down`). **Known gotcha:** `SIGTERM` on the AppHost does not cascade to the child `dotnet run` processes it spawns per service — they park as orphans and need an explicit `pkill` on the project-path pattern (recurs on every live-AppHost session, documented repeatedly rather than assumed fixed)

### Local-run gotchas

- **`docker` is not on PATH** under Rancher Desktop — it lives at `~/.rd/bin`. Aspire shells out to docker, so `export PATH="$HOME/.rd/bin:$PATH"` first.
- **Never delete `AgendaBuddy.AppHost/Properties/launchSettings.json`.** It sets `DOTNET_ENVIRONMENT=Development`; without it the AppHost runs as `Production`, user secrets never load, every secret parameter goes `ValueMissing`, and all 7 services park in `Waiting` **with nothing logged**.
- **Three AppHost secrets** must exist in user secrets: `Parameters:mongodb-password`, `Parameters:jwt-public-key`, `Parameters:jwt-private-key`. See the README for provisioning on a new machine.
- **Debug the app model** with `Logging__LogLevel__Aspire=Debug` — resource state transitions and parameter states are Debug-level only.
- **MongoDB uses a persistent volume**, so its password must stay stable. If auth breaks: `docker volume rm agendabuddy.apphost-<hash>-mongodb-data`.
- **A service's own Aspire endpoint auto-detection is derived from its `appsettings.json`'s `Kestrel:Endpoints` block, not `launchSettings.json`.** Swap them (e.g. during a project-rename/scaffold) and Aspire silently produces zero `EndpointAnnotation`s for that resource — no compile error, just an empty collection where `AppHostWiring.cs`'s own structural tests expect entries (found live during F-020's Provider migration).
- **Running a service standalone** needs `--no-launch-profile`, else launchSettings overrides `ASPNETCORE_ENVIRONMENT`.
- macOS has no `timeout` — use background + sleep + kill.
- **A full `dotnet build agenda-buddy.sln` fails on two independent things, and having "the SDKs" installed does not mean they're wired correctly:**
  - **iOS:** the `net10.0-ios` target needs `xcode-select -p` pointing at a full `Xcode.app`, not the Command Line Tools (`/Library/Developer/CommandLineTools`) — a fresh macOS setup (or one where CLT was ever installed standalone) defaults to the latter even with Xcode.app present in `/Applications`. Fix: `sudo xcode-select -s /Applications/Xcode.app/Contents/Developer`. `scripts/run-ios.sh` works around this per-process via `DEVELOPER_DIR` (no sudo) rather than changing the global pointer — a plain `dotnet build` gets none of that and needs the global fix. **After fixing it, a build that already failed once needs a clean rebuild** (`rm -rf AgendaBuddy.MobileApp/obj AgendaBuddy.MobileApp/bin`) — the iOS SDK targets cache the (broken) resolved Xcode path in `obj/`, so an incremental rebuild keeps failing with the same error even after `xcode-select -p` is correct.
  - **Android:** the `net10.0-android` target has no explicit `-android36.0`-style suffix in `AgendaBuddy.MobileApp.csproj`, so the exact `android.jar` it compiles against is whatever the installed `android` dotnet workload manifest pins — currently **API 36** (`Microsoft.Android.Sdk.Darwin/36.1.69`), regardless of which Android SDK Platform you last installed through Android Studio. **Having a newer platform installed (e.g. API 37) does not satisfy this**, and neither does a `.1` minor revision (e.g. `android-36.1`, "Android 16 QPR1") — `~/Library/Android/sdk/platforms/android-36/` (the plain base platform) has to exist specifically, or the build fails `XA5207`. Fastest fix, no Android Studio needed: `dotnet build AgendaBuddy.MobileApp/AgendaBuddy.MobileApp.csproj -t:InstallAndroidDependencies -f net10.0-android "-p:AndroidSdkDirectory=$HOME/Library/Android/sdk" -p:AcceptAndroidSDKLicenses=true` — the .NET Android SDK's own installer fetches exactly the platform its pinned manifest needs.
  - **Running the integration suite with `/p:MobileWorkloads=false` can leave `AgendaBuddy.MobileApp/obj` restored to a net10.0-only TFM set**, which then breaks a subsequent plain `dotnet build agenda-buddy.sln`. Fix: `dotnet restore agenda-buddy.sln` (no flag) before investigating further — a restore-cache artifact, not a code defect.

## Architecture

Seven independent ASP.NET Minimal API microservices (Booking, Calendar, Customer, Provider, Services, Profession, Identity) each own their MongoDB collection and expose REST endpoints. All domain entities and services live in the shared `AgendaBuddy.Library` project. Kafka provides async provider-to-customer messaging via per-provider topics.

**Six of the seven services follow a 4-project Clean Architecture split; Identity is deliberately the exception.** `Booking.Api`→`AgendaBuddy.Booking.Api` piloted the shape in F-019; `AgendaBuddy.{Calendar,Customer,Provider,Services,Profession}.Api` got it in F-020, the program's rollout stage. Each service's `*.Api` (endpoints/DI only) dispatches via `IMediator` to handlers in its own `*.Core`, typed against its own `*.Domain`'s commands/queries/DTOs; handlers return `FluentResults.Result<T>`, which `*.Api` maps to that service's own in-repo `DataResponse<T>` envelope (`Success`/`Data`/`Errors`) at the wire boundary — not a string-sniffed `"exception"`-prefix convention. Each `*.Infrastructure` exists but is deliberately empty (YAGNI — nothing needed it yet, for any of the 6). `DataResponse<T>` is intentionally **not** a shared type across services — each `*.Domain` defines its own byte-identical copy; no cross-service code needs the *same* type, only the same *shape* (see `docs/pdlc/design/api-refactor-rollout/ARCHITECTURE.md` §3 for the full reasoning). Validation is mid-migration from `MiniValidator` to Validot's `Specification<T>` DSL, currently limited to 3 of Booking's 10 routes — this is not a blanket program-wide migration and each service's task list discloses its own scope rather than implying full coverage. New handlers still audit via `eventStore.SaveAsync` per CONSTITUTION §3; `EventStoreWriteGuardTest`'s `ScanRoots` covers every migrated service's own `*.Core`, plus `AgendaBuddy.EventAndCommands` (now empty of handler implementations — see the Key Files entry below).

**Identity is not part of this Clean Architecture rollout.** It never adopted the `RequestCollection`/CQRS/EventStore shape the other 6 share — it dispatches via direct `IdentityService` method calls with zero MediatR, and has its own exception taxonomy from F-021 (`AuthValidationException`, `ConflictException`, etc.), not `AgendaBuddyExceptionHandler`. Migrating it to the same shape would be introducing the pattern fresh, not replicating a proven one — a distinct, unvalidated feature with its own threat-model needs, not scoped into F-019/F-020.

Locally, `AgendaBuddy.AppHost` is the composition root — it declares the infrastructure, every service, and the `AgendaBuddy.Gateway`, assigning ports dynamically (no hardcoded host ports). Every service — and the Gateway itself — calls `builder.AddServiceDefaults()` exactly once, which supplies OpenTelemetry, `/health` (readiness, including a 5-second-cached MongoDB check) and `/alive` (liveness), service discovery, and HTTP resilience. **One `IMongoClient` singleton is shared process-wide** by all services and `EventStore`.

**`AgendaBuddy.MobileApp` is the only client, and it reaches the backend through the Gateway, and only the Gateway** (F-015). The Gateway forwards `api/v1/{service}/**` to its matching destination by an explicit allowlist — never a catch-all — resolved live from the same Aspire service-discovery config every service already reads, so it survives a backend restart's dynamic-port reassignment without itself restarting. It does not validate, strip, or terminate the caller's JWT — auth passthrough only. On a destination failure it attaches the failed cluster's name (`failedService`) to a `ProblemDetails` body, so the client can say "Booking is unavailable" rather than a generic error. **The allowlist remains the one place a new backend route group can go silently unreachable from the mobile client** — see the Key Files entry below.

See [docs/pdlc/design/api-refactor-rollout/ARCHITECTURE.md](docs/pdlc/design/api-refactor-rollout/ARCHITECTURE.md) for the full Clean Architecture rollout design and [docs/pdlc/context/](docs/pdlc/context/) for a `file:line`-anchored map of the codebase (⚠️ predates F-020's rename — refresh incrementally, not yet done as of this ship).

## Coding Conventions

- Business logic in the service layer only — not in API handlers
- Repository pattern only — `MongoDbRepository<T>` for all DB access
- Async all the way: every I/O method returns `Task` or `Task<T>`
- MongoDB field names via `[BsonElement("snake_case")]` attributes
- PascalCase for classes, methods, properties; `_camelCase` for private fields
- `[Required]`, `[EmailAddress]` data annotations on entity properties at the API boundary
- Comments stay minimal — no F-XX/T-XX feature/task-ID references. State the invariant or constraint directly; project-history belongs in commit messages, not inline comments. ADR-XXX references are fine to keep

## Key Files

- `AgendaBuddy.Library/Entities/` — all domain entity definitions (AppointmentEntity, ProviderEntity, CustomerEntity, ServiceEntity, ProfessionEntity)
- `AgendaBuddy.Library/Repositories/MongoDbRepository.cs` — generic MongoDB CRUD implementation
- `AgendaBuddy.Library/Tools/CacheAside.cs` — distributed cache-aside extension (use this for all cached reads)
- `AgendaBuddy.Library/Repositories/IRepository.cs` — `FindOneAndUpdateAsync(filter, update)` is the **only** partial-update primitive (ADR-032). Every other write here replaces a whole document. It **never upserts**, which is what stops a failed login for an unknown address creating an account
- `AgendaBuddy.ServiceDefaults/TransportSecurity.cs` — HSTS policy plus `UseAgendaBuddyTransportSecurity()`. **All seven services must call it immediately before `UseAuthentication()`** — a test in `AgendaBuddy.Library.Tests` (`TransportSecurityOrderTest`) fails if any service gets the order wrong or calls `UseHttpsRedirection` directly. Its own service-name list needed updating at every one of F-020's rename/migration tasks — the single most frequently-touched structural test in that feature
- `AgendaBuddy.Identity/Extensions/RateLimitingExtensions.cs` — per-IP limiter on `login` **and** `register`, the two routes that spend BCrypt (262 ms each, measured). `refresh` is deliberately unlimited
- `AgendaBuddy.Library/Tools/ObjectIdJsonConverter.cs` — **register this in any service that returns an entity.** Without it `System.Text.Json` emits `"id": {"timestamp":…,"machine":…}`, which cannot be read back into an `ObjectId` at all. Registered in Booking, Customer and Provider by F-014; Calendar, Services and Profession still emit the broken shape (filed)
- `AgendaBuddy.Library/Services/PaymentGatewayFactory.cs` — payments are **non-charging** unless `Payments:Stripe:ApiKey` is configured. A `Succeeded` payment with a `local_` intent id moved no money (ADR-038)
- `AgendaBuddy.Library/Entities/AppointmentEntity.cs` — `TransitionTo` is the **only** way to change an appointment's status (ADR-037). The `PUT` route ignores the status field; restoring that assignment reopens threat T-203. `ServiceName`/`ServiceDurationMinutes` are additive and nullable (2026-08-29) — null on every appointment booked before a service could be chosen; the duration is copied at booking time so editing the service later does not rewrite history
- `AgendaBuddy.Library/Tools/AvailabilityCalculator.cs` — **the only availability computation anything customer-facing should use.** Replaces `SupportTools<T>.GetThirtyDaysCalendarAvailability`, which (a) excluded booked *start* times only, so a 2-hour appointment blocked 1 hour and the overlap was still offered — a double-booking generator now services carry durations; (b) mixed local `DateTime.Today`/`Now` with UTC-persisted appointments, making results and its own tests timezone-dependent; (c) ignored `day_off`. This one is UTC-only, takes `nowUtc` as a parameter so tests are deterministic, compares whole intervals (half-open, so back-to-back booking still works), and clamps the window to `MaxDays = 90`. ⚠️ Business hours are still **fixed 09:00–19:00 interpreted as UTC** — there is nowhere to store per-provider hours or a timezone (the F-005 gap); this made the maths consistent, it did not add schedule storage
- **`GET /api/v1/calendar/availability/{email}` is deliberately NOT ownership-guarded** (2026-08-29) — a customer must see a provider's free slots to book one, and `AssertOwner` made it answer 403 to every customer. Authentication is still required. Safe because the body is free **start times only** — no appointment, counterparty, service or reason; busy time is inferable only as absence, which is inherent to booking products. Takes `days` (clamped 1–90) and `service` (a service name, sizes each slot to its duration). An empty list is `200`; `404` now means only "no such provider". **The sibling `/appointments/{email}` stays owner-only** — it projects whole appointments carrying customer emails. `CalendarOwnershipTest` holds both halves of that boundary
- `AgendaBuddy.EventAndCommands/Persistence/EventStore.cs` — audit event persistence. Takes an injected `IMongoClient`; it no longer builds one per request scope. **As of F-020, this project holds zero command/query handler implementations** — every service's handlers moved to their own `*.Core` project; `AgendaBuddy.EventAndCommands` is now purely the EventStore/audit kernel plus config bootstrap
- `AgendaBuddy.Booking.Api/Program.cs`, `AgendaBuddy.Calendar.Api/Program.cs`, etc. — representative Minimal API entry points for the 6 Clean-Architecture-split services (endpoint/DI wiring only — see Architecture above)
- `AgendaBuddy.Gateway/Program.cs` — the reverse-proxy pipeline: `AddServiceDefaults()`, transport security before auth (no auth middleware here — passthrough), YARP registration, the `MapFallback` handler that shapes an unmatched path into `gateway-no-route`, and the response transform that shapes a destination failure into `gateway-destination-unreachable` + `failedService`
- `AgendaBuddy.Gateway/AspireServiceDiscoveryProxyConfigProvider.cs` — the explicit `api/v1/{service}/**` route allowlist (never a catch-all — T-302) built from live Aspire service-discovery config, polled every 2 seconds. **A reviewer should check this file first for any new client-facing route** — a route a backend service adds is invisible to `AgendaBuddy.MobileApp` through the Gateway until a line is added here, and nothing fails loudly when it's missing
- `AgendaBuddy.MobileApp/Routing/` — Maui-free, DI-free static route-builder classes (one per `*ApiService`) extracted so route/verb/payload logic is testable under `AgendaBuddy.MobileApp.Tests`'s `net10.0` fallback TFM
- `AgendaBuddy.MobileApp/Infrastructure/GatewayErrorMapper.cs` — maps the Gateway's `failedService` cluster id to a human-readable display name in the error banner ("booking" → "Booking is unavailable right now. Try again.")
- `AgendaBuddy.MobileApp/Infrastructure/ApiBaseUrlResolver.cs` — `MAUI_API_BASE_URL` env var → `ApiBaseUrl` config → the Gateway's pinned local address as fallback
- `AgendaBuddy.Library/LocalGatewayAddress.cs` — **the Gateway's host port is pinned to `6080` for local runs**, and both the AppHost endpoint and the mobile client's fallback read this one constant so they cannot drift. The Gateway is the deliberate exception to AC-1.4 (which keeps the *seven services* on AppHost-assigned ports, enforced by `AppHostWiringTest.NoServiceBindsAHardcodedHostPort` — that test's own service list never included the Gateway). `6080` sits outside the `6030–6039` band a `Local (standalone)` run uses. Only the **host** port is pinned; the port the Gateway listens on stays Aspire-assigned, and nothing is pinned in the Cloud shape. Before this, the mobile fallback named `6036` — *Identity's* standalone port — so any launch that skipped `scripts/run-ios.sh` sent every request to a dead port and surfaced as "invalid email or password"
- `AgendaBuddy.AppHost/Program.cs` + `AgendaBuddy.AppHost/AppHostWiring.cs` — the Aspire app model: every resource, reference, and the run/publish (`DeploymentTarget`) split. The `Gateway` resource `WithReference`/`WaitFor`s all seven services. Aspire derives each service's `Projects.<Name>` type from its `.csproj` file name (dot→underscore) — e.g. `AgendaBuddy.Booking.Api.csproj` → `Projects.AgendaBuddy_Booking_Api`, `AgendaBuddy.Gateway.csproj` → `Projects.AgendaBuddy_Gateway`. Also derives each resource's `EndpointAnnotation`s from that project's `appsettings.json` `Kestrel:Endpoints` block, **not** `launchSettings.json` — get the base/Development appsettings split backwards during a project rename and Aspire silently produces zero endpoints for that resource
- `AgendaBuddy.ServiceDefaults/Extensions.cs` — `AddServiceDefaults()` / `MapDefaultEndpoints()`, called by all 7 services
- `AgendaBuddy.ServiceDefaults/PiiRedactingProcessor.cs` — strips email addresses from span attributes before export. **Do not remove:** `url.path` was leaking real customer emails (threat T-004)
- `AgendaBuddy.Library/MongoConnectionResolver.cs` — resolves the Mongo connection string (Aspire → environment → appsettings) with an actionable failure message
- `AgendaBuddy.IntegrationTests/` — the only integration suite. `Harness/ServiceHostFixture.cs` hosts a real service over HTTP against a MongoDB Testcontainer (container per test class, database per test); `Harness/MongoEndpointGuard.cs` **fails the suite closed** if the resolved endpoint is not this session's own container. `GlobalUsings.cs` has one anchor alias per service (`BookingAnchor`, `CalendarAnchor`, etc., all `<Service>.Configurations.MongoDbConfiguration` or the Booking-specific singular `Configuration` — never `WebApplicationFactory<Program>`, which is ambiguous across all seven assemblies). `Contract/` (Tier 1, one route-contract test per service, status codes only), `Persistence/` (Tier 2, write→read round-trips), `Audit/` (Tier 3, audit-fired assertions plus `EventStoreWriteGuardTest` — a convention-based permanent guard scanning every command/query handler file for `eventStore.SaveAsync(`, with `ScanRoots` covering `AgendaBuddy.EventAndCommands` plus every migrated service's own `*.Core`), and `OpenApi/` (byte-deterministic spec generation + drift check)
- `agenda-buddy-backend.slnf` — the solution filter the backend CI job and local backend test runs target; excludes `AgendaBuddy.MobileApp` **and `AgendaBuddy.IntegrationTests`** by design (ADR-031)
- `docs/api/openapi/` + `scripts/generate-openapi.sh` — generated OpenAPI specs for all 7 services, plus a route index. A **build artifact**, regenerable on demand; do not hand-edit. `index.md` is still owned by `scripts/generate-openapi.sh` alone and goes stale until that script is rerun by hand
- `bruno/agenda-buddy/` — Bruno collection covering all 7 services. Two environments: `Local (Aspire AppHost)` and `Local (standalone)`
- `scripts/run-ios.sh` — one-command local run: AppHost + port discovery + iOS simulator + `AgendaBuddy.MobileApp`
- `azure.yaml` + `.github/workflows/deploy.yml` — cloud deploy path. **Written, unit-tested, never executed**
- `docker-compose.yml` + `docker-compose.override.yml` — legacy Kafka + Zookeeper + Schema Registry + service definitions. Only `AgendaBuddy.Identity`'s entry is active; `Provider`/`Services`' are still commented out (pre-existing, unrelated tech debt)
- `.github/workflows/dotnet.yml` — CI pipeline: restore → build → test → coverage upload, plus AppHost build and startup guards. `security-scan` (dependency audit + `gitleaks` secret scan, `if: always()` — runs on every PR unconditionally) and `docker-build-and-scan` (7-service matrix, `dotnet publish -t:PublishContainer` + Trivy scan). `build-and-test` also runs `dotnet format --verify-no-changes`; `integration` also runs `scripts/verify-container-reaping.sh` and `OpenApiSpecDriftTest`. **Every path filter in this file's `changes` job had to be updated for every one of F-020's 12 project renames** — a recurring theme of that feature's own build loop
- `.gitleaks.toml` — custom rule detecting MongoDB/Atlas-style connection strings with embedded credentials, extending gitleaks' default ruleset
- `scripts/verify-gitleaks-canary.sh` + `scripts/verify-trivy-severity-gate.sh` + `scripts/verify-container-reaping.sh` — CI-wired self-tests proving the security-scan-adjacent tooling actually works
- `scripts/trivy-severity-gate.sh` — the actual severity gate for `docker-build-and-scan`'s Trivy step: fails on HIGH/CRITICAL under an `app/*.deps.json` Trivy report target, warns (does not fail) on anything else
- `.github/dependabot.yml` — weekly NuGet + GitHub Actions dependency-update PRs

### Security controls that default OFF

`Security:RateLimiting:Enabled` and `Security:Hsts:Enabled` are **off unless configured**, and gated on
configuration rather than `IsProduction()` — every service runs as **Production** under the local AppHost,
so the environment name cannot distinguish a laptop from a deployment (ADR-033). The AppHost injects
`Security__Local=true` locally and turns both **on** in the cloud graph; each service warns at startup,
naming the key, when a control is off outside a local run. Full surface in
`docs/pdlc/context/06-configuration.md`.

**CONSTITUTION §7's security scan (dependency audit + secret scan) is automated and always-on in CI**
(`security-scan`, `if: always()`) — see the Key Files entries for `.github/workflows/dotnet.yml`,
`.gitleaks.toml`, and `scripts/`.

**PDLC memory:** `docs/pdlc/memory/` — CONSTITUTION.md, INTENT.md, OVERVIEW.md, DECISIONS.md, ROADMAP.md, STATE.md

## ⚠️ Open risk you should know about

The `agenda_buddy` MongoDB Atlas credential was committed and **is still in git history and still valid** — it was removed from the working tree in F-013, which is not the same as rotating it. The cluster holds client names, emails, phone numbers and appointment records, and has no backups. Rotation is a human-only action and is the hard prerequisite for any cloud deployment. See `docs/issues/ISSUE-002-atlas-credential-rotation.md`.


<!-- BEGIN BEADS INTEGRATION v:1 profile:minimal hash:46cd31e7 -->
## Beads Issue Tracker

This project uses **bd (beads)** for issue tracking. Run `bd prime` to see full workflow context and commands.

### Quick Reference

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --claim  # Claim work
bd close <id>         # Complete work
```

### Rules

- Use `bd` for ALL task tracking — do NOT use TodoWrite, TaskCreate, or markdown TODO lists
- Run `bd prime` for detailed command reference and session close protocol
- Use `bd remember` for persistent knowledge — do NOT use MEMORY.md files

**Architecture in one line:** issues live in a local Dolt DB; sync uses `refs/dolt/data` on your git remote; `.beads/issues.jsonl` is a passive export. See https://github.com/gastownhall/beads/blob/main/docs/core-concepts/sync-concepts.md for details and anti-patterns.

## Agent Context Profiles

The managed Beads block is task-tracking guidance, not permission to override repository, user, or orchestrator instructions.

- **Conservative (default)**: Use `bd` for task tracking. Do not run git commits, git pushes, or Dolt remote sync unless explicitly asked. At handoff, report changed files, validation, and suggested next commands.
- **Minimal**: Keep tool instruction files as pointers to `bd prime`; use the same conservative git policy unless active instructions say otherwise.
- **Team-maintainer**: Only when the repository explicitly opts in, agents may close beads, run quality gates, commit, and push as part of session close. A current "do not commit" or "do not push" instruction still wins.

## Session Completion

This protocol applies when ending a Beads implementation workflow. It is subordinate to explicit user, repository, and orchestrator instructions.

1. **File issues for remaining work** - Create beads for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **Handle git/sync by active profile**:
   ```bash
   # Conservative/minimal/default: report status and proposed commands; wait for approval.
   git status

   # Team-maintainer opt-in only, unless current instructions forbid it:
   git pull --rebase
   bd dolt push
   git push
   git status
   ```
5. **Hand off** - Summarize changes, validation, issue status, and any blocked sync/commit/push step

**Critical rules:**
- Explicit user or orchestrator instructions override this Beads block.
- Do not commit or push without clear authority from the active profile or the current user request.
- If a required sync or push is blocked, stop and report the exact command and error.
<!-- END BEADS INTEGRATION -->
