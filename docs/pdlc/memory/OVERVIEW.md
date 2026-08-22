# Overview
<!-- pdlc-template-version: 2.1.0 -->
<!-- This file is the living, aggregated record of everything this product does and has shipped.
     It is updated automatically by PDLC after every successful merge to main (during Reflect sub-phase).
     Use it to orient yourself after time away, onboard a new teammate, or brief Claude in a fresh session.
     Do not edit manually — let PDLC maintain it. If you need to correct something, update and note the reason. -->

**Project:** Agenda Buddy
**Last updated:** 2026-08-22T15:10:00Z

---

## Project Summary

Agenda Buddy is a scheduling and appointment management platform for independent service providers — fitness coaches, tutors, therapists, software instructors, and other one-to-one session specialists. It lets providers manage their client roster, service catalog, and appointment calendar in one place, replacing the patchwork of spreadsheets and general-purpose calendar apps most solo practitioners currently rely on.

---

## Active Functionality

*Pre-PDLC functionality — documented at initialization. Future entries tracked per episode.*

- Providers can register with a profile (name, email) and be assigned a Kafka topic for async messaging
- Providers can define a service catalog with name, description, fee, and fee type (Hourly/Fixed/Subscription)
- Providers can be updated, deactivated, and looked up by email
- Customers can register with a profile (name, email) and subscribe to providers
- Appointments can be created (Requested), updated, and cancelled; status transitions: Requested → Booked → Completed
- Calendar availability can be checked; existing appointments can be listed for a provider
- 100+ profession categories are seeded and queryable for provider classification
- All command mutations are persisted as events (success/fail) to the EventStore for audit
- Read-heavy queries use a cache-aside distributed cache with 5-minute TTL and stampede protection
- GitHub Actions CI runs build + test + coverage on every push/PR to main

**Added by F-013 (`v0.1.0`, 2026-08-18):**

- A developer can start the entire stack — MongoDB, Kafka, and all seven services — with one command: `dotnet run --project AgendaBuddy.AppHost`
- Every service exposes `/health` (readiness, includes a cached MongoDB connectivity check) and `/alive` (liveness), so an orchestrator restarts a dead process but not one whose database is merely unreachable
- Every service exports OpenTelemetry traces, metrics and structured logs to the Aspire dashboard, with service discovery and HTTP resilience applied uniformly
- Exported spans are PII-redacted: email addresses are stripped from `url.path`, `url.query`, `url.full`, `http.url`, `http.target` and the span display name before any exporter sees them
- Services resolve their MongoDB connection from Aspire, environment, or `appsettings.json` — and start outside `Development` (verified in `Staging`)
- All seven services and `EventStore` share one `IMongoClient` singleton, ending the per-HTTP-request connection pool
- A cloud deployment path exists in code (`azure.yaml`, `deploy.yml`, `DeploymentTarget.Cloud`) — **written and unit-tested, never executed**

**Added by F-016 (`v0.2.0`, 2026-08-18):**

- The five PII read routes now **require authentication** — `providers`, `providers/{email}`, `customers`, `customers/{email}`, `services/{email}`. `professions*` stays anonymous as reference data
- A provider's record is **projected to `ProviderSummary`** (`email`, `firstName`, `lastName`, `services`) for anyone who is not its owner, so an authenticated customer browsing for a coach no longer receives every provider's appointment book and client roster
- Both Calendar routes are **ownership-guarded before the cache read**, closing the IDOR
- Both list endpoints are **paginated** with a clamped, capped page size — an uncapped page size would restore the full-dataset dump. Response shape: `{items, totalCount, page, pageSize}`
- `ForbiddenException` maps centrally to **403** (previously a forgotten `try/catch` returned 500), and `AssertRole` is wired on provider creation. `POST /api/v1/professions` was **deleted** rather than role-gated (ADR-025)
- Read queries **no longer serialise full PII into the `events` audit collection**
- **`AgendaBuddy.IntegrationTests`** — the project's first integration suite: 99 tests hosting real services over HTTP against a MongoDB Testcontainer, with a fail-closed endpoint guard. Deliberately excluded from `agenda-buddy-backend.slnf` so the unit gate stays Docker-free (ADR-031)

---

## Shipped Features

*F-001–F-012 are marked `Shipped` in ROADMAP.md but predate PDLC ship tracking — no episode files, no CHANGELOG entries, no tags. `v0.1.0` is the first PDLC-tracked release.*

| # | Feature | Date Shipped | Episode | PR |
|---|---------|-------------|---------|-----|
| — | Pre-PDLC baseline | 2024-04-16 → 2026-07-30 | — | — |
| 001 | F-013 aspire-wiring (`v0.1.0`) | 2026-08-18 | [EPISODE_aspire-wiring_2026-08-17.md](../episodes/EPISODE_aspire-wiring_2026-08-17.md) | [#35](https://github.com/ogdevlabs/agenda-buddy/pull/35) |
| 002 | F-016 secure-public-endpoints (`v0.2.0`) | 2026-08-18 | [EPISODE_secure-public-endpoints_2026-08-18.md](../episodes/EPISODE_secure-public-endpoints_2026-08-18.md) | [#38](https://github.com/ogdevlabs/agenda-buddy/pull/38) |

---

## Architecture Summary

- **.NET Aspire orchestration** *(added F-013)*: `AgendaBuddy.AppHost` is the composition root for local development — it declares MongoDB and Kafka as container resources and all seven services as projects, assigning ports dynamically. `AgendaBuddy.ServiceDefaults` is referenced by every service and supplies OpenTelemetry, health/liveness endpoints, service discovery, HTTP resilience, and the `PiiRedactingProcessor`. Docker Compose remains as a legacy fallback.
- **Seven ASP.NET Minimal API microservices**: Booking, Calendar, Customer, Provider, Services, Profession — plus **Identity** — each with its own test project. *(The "six" count predates Identity.)*
- **Shared Library project**: all domain entities (`AppointmentEntity`, `ProviderEntity`, `CustomerEntity`, `ServiceEntity`, `ProfessionEntity`), the generic `IRepository<T>` / `MongoDbRepository<T>`, domain services, and tools (CacheAside, EnumHelper) live here and are consumed by all services
- **CQRS via MediatR**: the shared `EventAndCommands` project holds all commands, queries, and their handlers; each handler calls Library services and persists an audit event to EventStore
- **Kafka**: Confluent stack (Kafka + Zookeeper + Schema Registry + Kafka UI) run via Docker Compose; per-provider topics created on-demand
- **MongoDB**: document store for all domain data; embedded sub-documents for provider services and appointments. One `IMongoClient` singleton is shared process-wide by all services and `EventStore` *(F-013)*; connection strings resolve via `MongoConnectionResolver` (Aspire → environment → appsettings).
- **Cache-aside pattern**: `CacheAside` extension on `IDistributedCache` with semaphore-guarded double-checked locking

---

## Known Tech Debt

- ~~[Added 2026-07-30] `EventAndCommands/Persitency/` is a typo (should be `Persistence`)~~ — **RESOLVED by F-016** (T01, absorbed from F-018's plan). CONSTITUTION §9's prohibition against renaming it is retired.
- ~~[Added 2026-07-30] `KafkaClient` hardcodes `BootstrapServers = "localhost:9092"`~~ — **RESOLVED by F-013**: now configuration-driven
- ~~[Added 2026-07-30] No authentication or authorization layer~~ — see the F-016 caveat below; auth exists but is not uniformly enforced
- [Added 2026-07-30] `topicName` computed but never used in `Booking/Program.cs` and other services — dead code cleanup needed
- [Added 2026-07-30] `provider` and `services-api` containers are commented out in `docker-compose.yml` — wire them in or remove the commented blocks
- [Added 2026-07-30] Customer and Profession command handlers have no test coverage in `EventsAndCommands.Tests` — coverage gap

**Added by F-013 (2026-08-18):**

- ⚠️ **[HIGHEST RESIDUAL RISK] The `agenda_buddy` Atlas credential is unrotated.** Removed from 17 tracked files, but **9 commits still carry it in git history and it remains valid**. **Corrected 2026-08-18:** the cluster holds **only synthetic / development data** — earlier records claiming real client names, emails and phone numbers were inferred from the schema and are wrong. It **has no backups**, so the residual risk is destruction of dev data and Atlas resource abuse, not a personal-data breach. Re-graded MEDIUM. Human-only action: `docs/issues/ISSUE-002-atlas-credential-rotation.md` (`agenda-buddy-41s`). Hard prerequisite for any cloud deployment.
- **CONSTITUTION §7's security-scan gate is still not automated.** Run by hand at the v0.1.0 ship (0 vulnerable packages; working tree clean) but CI has one credential grep, not a scanner, and neither `gitleaks` nor `trufflehog` is installed. Owned by **F-017**.
- **No `.editorconfig`.** `dotnet format` found 69 whitespace findings at the ship gate; they were fixed, but nothing prevents the drift returning.
- ~~**No integration-test harness.**~~ — **RESOLVED by F-016**, which absorbed eight of F-018's tasks to build `AgendaBuddy.IntegrationTests` (99 tests, Testcontainers) *before* rewriting any endpoint. ⚠️ Two things remain: `Integration — real services + MongoDB` is **not yet a required status check** on `main`, so the job can fail and a PR still merge; and §7's Integration checkbox stays unchecked pending 10 consecutive green runs.
- **7 `MongoDbConfiguration` classes + 7 interfaces** are kept alive solely by 3 tests. Delete with the tests, or convert those tests to the new path.
- **7 near-identical `ServiceCollectionMongoResolutionTest.cs`** (~150 lines each) — collapse to a shared theory when one next needs editing.
- **`AppHostWiring` mutates Aspire-produced `EndpointAnnotation`s** — revisit on any Aspire major upgrade.
- **`scripts/seed/seed-mongo.sh` is stale** — hardcodes `mongo:27017` and targets `ProviderDb`/`CustomerDb`, which no service reads.
- **`docs/pdlc/context/` describes pre-Aspire wiring** in places — refreshed incrementally at ship.
- ~~**`agenda-buddy-prr`** — `MobileApp` does not compile under `/p:MobileWorkloads=false`~~ — **RESOLVED and verified 2026-08-18.** The bead is closed and `MobileApp.Tests` passes 67 (7 skipped) locally. CI already runs three dedicated mobile jobs (`build-android`, `build-ios` on `macos-latest`, `build-mobile-tests`). MobileApp stays out of `agenda-buddy-backend.slnf` **by design**, not because it is broken. A stale comment in `.github/workflows/dotnet.yml` still claims otherwise.
- **Two advisory test gaps from Echo**: the guarded legacy `MongoDbConfiguration` ctor throw, and `ProfessionSeedHostedService.StartAsync` (which swallows exceptions, so a seeding bug surfaces only as an empty catalogue).
- **Six shipped-but-unreachable capabilities** (NotificationService, MessageService, NoteService, PaymentService, ReportingService, DeactivateProviderCommand) — implemented and unit-tested but with no DI registration, collection config, or HTTP route, so F-006–F-010 read as Shipped while being unreachable. Owned by **F-014**.
- **The mobile client cannot reach the backend** — wrong route prefixes, no gateway for 7 ports, and a seed-data fallback that masks all of it. Owned by **F-015**.
- ~~**PII exposure on public endpoints**~~ — **RESOLVED by F-016** (`v0.2.0`), and demonstrated live at the Ship/Verify gate rather than by inspection: all five routes 401 anonymous, non-owners receive `ProviderSummary` only, both Calendar routes ownership-guarded.

**Added or exposed by F-016 (2026-08-18, recorded at the ship gate 2026-08-22):**

- ⚠️ **No cache invalidation exists anywhere in the solution** (`agenda-buddy-xrw`, P2). Nine `CacheAside.GetOrCreateAsync` read sites, zero `RemoveAsync`, 5-minute absolute TTL — so a provider who completes onboarding is absent from the discovery list for up to five minutes. Pre-existing; **found by running the system** at the Verify gate, where review had only inferred it (finding I-1). `CacheAside.cs:38` also re-reads the cached value after taking the semaphore and discards it, leaving the double-checked lock incomplete.
- **`GET /api/v1/customers` returns the full `CustomerEntity`** — including `SubscribedProviderCollection`, `AppointmentCollection` and `KafkaTopic` — to any Provider-role caller. Accepted at review (I-2) per ADR-026's deferral of owner-scoping, now quantified against the real payload.
- **Authorization failures are entirely unlogged** — there is no log sink at all, so IDOR probing leaves no trace. Advisory A-1; belongs to F-021/F-024.
- **`SSH.NET 2024.2.0` (HIGH, `GHSA-q939-rpr3-3284`)** enters the dependency graph via Testcontainers in `AgendaBuddy.IntegrationTests`. Unreachable — Testcontainers only loads it for Docker-over-SSH, which this project does not use — and the unreachability is *tested* by `ContainerRuntimeGuardTest`. Disposition: ADR-030.
- **The standards-readiness gate has now blocked five consecutive gates and has never executed once.** Marked `enforcing`, sources unreachable under this `gh` auth, no vendored cache. Needs a reachable source or an explicit retirement decision — recommended folding into **F-017**.

---

## Decision Log Summary

1. **Microservices over monolith** (ADR-001) — six independent services, each deployable separately; adds operational complexity but enables independent scaling
2. **MongoDB + document model** (ADR-002) — nested provider/customer/appointment data fits documents well; no migration to relational planned
3. **CQRS + MediatR** (ADR-003) — clean separation of reads/writes in the shared EventAndCommands kernel; enables independent testing of each operation
4. **Event sourcing via EventStore** (ADR-004) — all command results persisted for audit; do not remove or bypass
5. **Cache-aside pattern** (ADR-006) — added for read performance with stampede protection; use `CacheAside.GetOrCreateAsync` for all cache interactions
6. **JWT asymmetric RSA signing** (ADR-008) — plus four F-001 decisions on auth scope and accepted risks (ADR-009 through ADR-012)
7. **.NET Aspire for local orchestration** (ADR-013) — AppHost + ServiceDefaults for local development. Notably, the Aspire MongoDB *client* integration was **rejected**: `Aspire.MongoDB.Driver` requires driver ≥ 3.9.0 against a pinned 2.25.0. The escape hatch is `AddSingleton<IMongoClient>` plus a custom `MongoHealthCheck`; Aspire is used hosting-only.

8. **Authenticated-by-default on PII reads** (ADR-022 … ADR-029, F-016) — authentication plus an owner/non-owner projection, pagination as a *security* control (an uncapped page size restores the full-dataset dump), a central `ForbiddenException` → 403 mapping, and `POST /api/v1/professions` deleted rather than role-gated (ADR-025). Owner-scoping of `GET /api/v1/customers` is explicitly deferred (ADR-026).
9. **A Testcontainers integration suite, excluded from the unit gate** (ADR-030, ADR-031, F-016) — `AgendaBuddy.IntegrationTests` hosts real services over HTTP with a fail-closed endpoint guard, and stays out of `agenda-buddy-backend.slnf` so the unit gate needs no container runtime.

See `docs/pdlc/memory/DECISIONS.md` for full ADR entries.
