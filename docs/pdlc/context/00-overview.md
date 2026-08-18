# Codebase Context — Agenda Buddy

**Generated:** 2026-08-15
**Scope:** Every relevant committed source file, configuration, and CI/CD asset in this repository was read in full to produce these notes. Coverage gaps are stated explicitly at the bottom of this file.

This folder is an **agent-readable mechanical map** of the codebase: what exists, where, with which inputs/outputs and side effects. It is intentionally low-level — for the high-level product/state view see `docs/pdlc/memory/OVERVIEW.md`.

## Document Index

| # | File | What it documents |
|---|------|-------------------|
| 00 | `00-overview.md` | This index, the repo snapshot, and the top cross-cutting findings |
| 01 | `01-api-surface.md` | Every HTTP route across the 7 ASP.NET services, with auth and status codes; the mobile↔backend contract mismatch |
| 02 | `02-entry-points.md` | The seven `Program.cs` top-level programs, their DI wiring order, and per-service middleware pipeline divergence |
| 03 | `03-services.md` | The 13 `Library/Services/*` domain services — method semantics, validation, side effects |
| 04 | `04-data-access.md` | `IRepository<T>` / `MongoDbRepository<T>`, the `BsonDocument` filter convention, and query-shape risks |
| 05 | `05-data-model.md` | The 13 entities, their `[BsonElement]` snake_case mappings, embedded-document topology, and database/collection ownership |
| 06 | `06-configuration.md` | The `LibrarySettings.MongoDB` vs root-`MongoDB` config split, hardcoded ports, and the committed Atlas credential |
| 07 | `07-build.md` | 23 projects, `Directory.Build.props` CVE pins, the MAUI multi-TFM conditional build, `global.json` |
| 08 | `08-cicd-deploy.md` | The 5-job path-filtered GitHub Actions pipeline, the 8 Dockerfiles, and Docker Compose topology |
| 09 | `09-integrations.md` | MongoDB Atlas, Kafka/Confluent, Stripe, Firebase Cloud Messaging — and what is *not* integrated |
| 10 | `10-error-handling.md` | The duplicated `UseExceptionHandler` block, ProblemDetails wiring, and the Development-only error pipeline |
| 11 | `11-testing.md` | 256 test attributes across 11 test projects; what is covered and what is not |
| 12 | `12-observability.md` | Logging configuration only — no metrics, no tracing, no health checks (all negative findings) |
| 13 | `13-security.md` | JWT RS256 posture, `OwnershipGuard` IDOR defence, BCrypt hashing — and the committed database credential |
| 14 | `14-glossary.md` | Domain, platform, build, and workflow terms defined from this codebase |
| 15 | `15-cqrs-and-messaging.md` | The CQRS kernel, the `RequestCollection`/`EventsHelper` hand-wiring that bypasses MediatR dispatch, the EventStore audit trail |
| 16 | `16-mobile-client.md` | The .NET MAUI client — DI, JWT handler, API services, and its seed-data fallback |

## Repo Snapshot

| | |
|---|---|
| Project / name | Agenda Buddy (`agenda-buddy.sln`) |
| Language / runtime | C# on **.NET 10** — `global.json:3` pins SDK `10.0.0`, `rollForward: latestMajor`, `allowPrerelease: true`. Every production `csproj` targets `net10.0` |
| Framework | ASP.NET Core 10 Minimal APIs (7 services) + .NET MAUI (1 mobile client) |
| Build | `dotnet` MSBuild; solution-wide `Directory.Build.props` for transitive CVE pins |
| API style | REST, route groups under `api/v1/<domain>`; Swashbuckle 10.2.3 Swagger UI in Development only. **No committed OpenAPI spec file** |
| Persistence | MongoDB via `MongoDB.Driver` 2.25.0. Database `agenda_buddy` (6 domain services) + `IdentityDb` (Identity) |
| Messaging | Confluent Kafka — **topic creation only**; no producers or consumers |
| Source files | 243 production `.cs` + 84 test `.cs`, 11 `.xaml`, 23 `.csproj` |
| Default branch | `main` (PR-protected per `docs/pdlc/memory/CONSTITUTION.md` §6) |

## Architecture in one paragraph

Seven independent ASP.NET Core Minimal API processes (Booking, Calendar, Customer, Provider, Services, Profession, Identity) each own a `Program.cs` that registers `MongoDbRepository<T>` instances against **the same `agenda_buddy` MongoDB database** (Identity uses its own `IdentityDb`). All domain logic lives in the shared `Library` project. Write paths go through per-service `RequestCollection` classes that **manually construct** CQRS handlers from `EventAndCommands` and call `.Handle()` directly; every handler persists a success/fail audit document to a Mongo `events` collection. A .NET MAUI client (`MobileApp`) is the only consumer. **The services never call each other over HTTP** — there is no inter-service communication, no API gateway, and no service discovery; they are seven front-ends over one shared database.

## Top cross-cutting findings

These are elaborated with `file:line` anchors in the concern files. Listed most severe first.

1. ⚠️ **A live MongoDB Atlas credential is committed to the repository** in 14 files — 9 `appsettings*.json`, `EventAndCommands/appsettings.json`, and `docker-compose.override.yml:114`. Directly violates `CONSTITUTION.md` §4. See `13-security.md`, `06-configuration.md`.
2. ⚠️ **The backend only starts in the `Development` environment.** Every service's `AddMongoDbRepository` reads the **root-level** `MongoDB` config section (e.g. `Booking/Extensions/ServiceCollectionExtension.cs:10`), but that section exists only in `appsettings.Development.json`. In any other environment the connection string and collection names resolve to `null`. See `06-configuration.md`.
3. ⚠️ **The mobile client cannot reach the backend.** `MobileApp` uses a single `ApiBaseUrl` while the backend binds seven different ports (6030–6036) with no gateway; and every mobile route omits the `api/v1/` prefix and targets verbs the backend does not expose (`GET booking?date=…` — Booking has no GET at all). The ViewModels consequently fall back to `MobileApp/Services/SeedDataProvider.cs`. See `01-api-surface.md`, `16-mobile-client.md`.
4. ⚠️ **`Library`, `Kafka`, and `EventAndCommands` Dockerfiles publish `net10.0` output onto a `dotnet/runtime:8.0` base image** (`Library/Dockerfile:13`, `Kafka/Dockerfile:13`, `EventAndCommands/Dockerfile:12`) — a leftover the F-011 .NET 10 upgrade missed. Those images cannot run. See `08-cicd-deploy.md`.
5. ⚠️ **MediatR is registered but never used to dispatch.** There is no `mediator.Send(...)` anywhere; `RequestCollection` news up handlers by hand. And there are **zero `INotificationHandler` implementations**, so every `mediator.Publish(...)` call is a no-op. See `15-cqrs-and-messaging.md`.
6. ⚠️ **Kafka is decorative.** `Kafka/KafkaClient.cs:12` hardcodes `BootstrapServers = "localhost:9092"`, and the only operation is `CreateTopicIfNotExist`. No message is ever produced or consumed. See `09-integrations.md`.
7. ⚠️ **`AddDistributedMemoryCache()` is used everywhere** (e.g. `Provider/Program.cs:10`) — the "distributed" cache-aside pattern is per-process memory, so cache state cannot be shared across replicas. Compounded by `Library/Tools/CacheAside.cs:13`, a single **static** `SemaphoreSlim(1,1)` that serializes every cache miss process-wide and returns `default!` on a 500 ms timeout. See `03-services.md`, `04-data-access.md`.
8. ⚠️ **No health checks, no OpenTelemetry, no metrics, no tracing, no resilience/retry policies** anywhere in the solution. See `12-observability.md`.
9. ⚠️ **The seed script writes to databases the APIs never read.** `scripts/seed/seed-mongo.sh:14,22` imports into `ProviderDb` and `CustomerDb`, while every service reads `agenda_buddy`. See `05-data-model.md`.
10. ⚠️ **Read queries write to the audit EventStore.** `EventAndCommands/Queries/Provider/GetProvidersQueryHandler.cs:25` serializes the entire provider list (PII included) into a Mongo document on every GET. See `15-cqrs-and-messaging.md`.

## Drift against the memory bank

`docs/pdlc/memory/CONSTITUTION.md` §1 and `CLAUDE.md` both state **".NET 8"**; the code is **.NET 10** (F-011 shipped the upgrade). `CONSTITUTION.md` §3 describes "CQRS via MediatR" as a live constraint, but MediatR dispatch is bypassed (finding 5). `INTENT.md` "Out of Scope" still lists mobile app, payments, journal/notes, and messaging as future — all four have shipped (F-006 through F-012).

## How to read this catalog

- Every claim is anchored to a `file:line` reference where possible.
- Inferred behaviour is marked **Inference:**.
- Gaps that cannot be determined from the repo are marked **[unknown — outside repo]**.
- Problems are flagged inline with ⚠️ and a one-line reason.
- Generated from a point-in-time scan at commit `e94a8b7`; treat anything older than ~30 days as worth re-checking against `git log`. Use `skills/hydrate-context/SKILL.md` targeted-refresh mode to re-sync a drifted area.

## Coverage: read in full vs sampled

**Read in full:** all 7 `Program.cs`; all 12 production `.csproj`; all 17 `appsettings*.json`; `docker-compose.yml`, `docker-compose.override.yml`, `Directory.Build.props`, `global.json`, `.github/workflows/dotnet.yml`; all 5 read Dockerfiles (`Booking`, `Identity`, `Library`, `Kafka`, `EventAndCommands`); all 13 `Library/Entities`; all 13 `Library/Services` implementations; `Library/Tools/*` (5); `Library/Repositories/*` (2); `Library/Data/*` (2); `Library/Tools/Migrations/*` (2); `EventAndCommands` kernel (`ConfigurationLoader`, `LibrarySettings`, `ServiceCollectionExtensions`, `Persitency/*`); all 11 commands + 11 command handlers; `Kafka/*` (3); `Library.ServerAuth/*` (2); all of `Identity/` (4); `MobileApp` bootstrap + auth + push + 3 API services; all 7 per-service `MongoDbConfiguration` + `ServiceCollectionExtension`; `Booking`/`Provider` `RequestCollection` + `EventsHelper`; `scripts/seed/seed-mongo.sh`.

**Sampled (pattern confirmed uniform, not every file read line-by-line):**
- `EventAndCommands/Queries/**` — 1 of 10 handlers read (`GetProvidersQueryHandler`); the remaining 9 follow an identical publish→query→audit shape.
- `EventAndCommands/Events/**` — 1 of 19 read (`BookAppointmentEvent`); all are property-only `INotification` DTOs.
- Per-service `Requests/RequestCollection.cs` + `Events/EventsHelper.cs` — read for Booking and Provider; Calendar, Customer, Services, Profession follow the same hand-construction pattern.
- `MobileApp/ViewModels/**` (9 files) and `MobileApp/Views/**` (11 `.xaml` + code-behind) — **not read**. Their behaviour is inferred from `MobileApp.Tests/ViewModels/*` and the git log. `16-mobile-client.md` marks these gaps.
- `MobileApp/Services/*ApiService.cs` — 3 of 8 read (`Auth`, `Booking`, `Calendar`); the route-prefix defect is confirmed in all three.
- `MobileApp/Platforms/**`, `Library/Services/I*.cs` interface declarations, remaining 6 `launchSettings.json`, remaining 3 Dockerfiles (`Calendar`, `Customer`, `Profession`, `Provider`, `Services` — same shape as `Booking/Dockerfile`) — **not read**.
- Test bodies — inventoried by attribute count and file name, not read line-by-line (`11-testing.md`).
