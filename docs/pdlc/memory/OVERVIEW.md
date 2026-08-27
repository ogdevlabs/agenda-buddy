# Overview
<!-- pdlc-template-version: 2.1.0 -->
<!-- This file is the living, aggregated record of everything this product does and has shipped.
     It is updated automatically by PDLC after every successful merge to main (during Reflect sub-phase).
     Use it to orient yourself after time away, onboard a new teammate, or brief Claude in a fresh session.
     Do not edit manually — let PDLC maintain it. If you need to correct something, update and note the reason. -->

**Project:** Agenda Buddy
**Last updated:** 2026-08-27T02:22:00Z

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

**Added by F-021 (`v0.3.0`, 2026-08-22):**

- Token refresh is now a **single atomic update** — the credential document is never deleted and re-inserted, closing a window where a fault could destroy an account irrecoverably
- **Rate limiting** on `login` and `register` (per-IP, sliding window, 429 + `Retry-After`) and **self-clearing account lockout** after repeated failed logins
- **HSTS**, off by default, and `UseHttpsRedirection` now runs before `UseAuthentication` in all seven services
- **Credential-mutation logging** — create, rotate, lock, reset and session-end recorded with a one-way `acct_<hash>` reference, never the address
- `OwnershipGuard.AssertOwner`'s null-claim pass fixed — a token with no `sub` claim no longer passes ownership checks against an entity with no email

**Added by F-014 (`v0.4.0`, 2026-08-23):**

- Six previously-unreachable capabilities now have routes: **session notes** and **payments** on Booking, **messages** and **notifications** as new top-level groups on Customer, **provider reporting** and **self-deactivation** on Provider — all authenticated and ownership-guarded
- **Appointment status is server-owned** — `PUT` ignores a client-asserted status; a dedicated route applies transitions through the entity's own `Book()`/`Complete()` methods, with illegal transitions answering 409
- **Payments are non-charging by default** — a recording gateway unless `Payments:Stripe:ApiKey` is configured
- The provider report **no longer publishes a revenue figure** it cannot compute correctly — `revenueAvailable: false` plus a reason, instead of a plausible-but-wrong number

**Added by F-015 (`v0.5.0`, 2026-08-24):**

- `MobileApp` now reaches the real backend, on every screen, with **zero fabricated fallback** — `SeedDataProvider` is deleted entirely
- A new **Gateway** process (YARP reverse proxy, the eighth AppHost resource) is `MobileApp`'s single, only configured address, with an explicit `api/v1/{service}/**` route allowlist (never a catch-all) resolved live from Aspire service discovery
- Every `MobileApp` route, verb, and payload is corrected against the real backend contract — including a status-route swap onto F-014's server-owned transition endpoint, and hiding (not disabling) the customer-facing "mark complete" control
- **Logout calls the server**; a 401 mid-session transparently refreshes and retries once; a non-idempotent write is never silently auto-retried on an ambiguous timeout
- A destination failure surfaces as a named, human-readable error ("Booking is unavailable right now. Try again."), not a generic error
- Messaging and Notifications screens are reachable through the Gateway (found broken, fixed in the same gate that found it — see episode 005)

**Added by F-017 (`v0.6.0`, 2026-08-26):**

- Every pull request now gets an **automated dependency-vulnerability audit and secret scan** (`security-scan` CI job, runs unconditionally on every PR) — closing CONSTITUTION §7's mandatory-but-unimplemented gate, previously satisfied "by hand" at every ship since F-013
- A **canary test empirically proves** the configured gitleaks ruleset detects an Atlas-credential-shaped secret and redacts it from CI logs, rather than just asserting the configuration exists — closing the exact detection gap that let the real Atlas credential ship undetected (`ISSUE-002`)
- Every service-touching PR builds each of the 7 remaining services via **.NET SDK container support** (no Dockerfile) and scans the image with **Trivy**, failing only on project-introduced HIGH/CRITICAL findings
- `Library/Dockerfile`, `Kafka/Dockerfile`, and `EventAndCommands/Dockerfile` — three broken class-library images that published `net10.0` onto a `dotnet/runtime:8.0` base and could never run — are **deleted**; a generalized structural test guards against this defect class recurring under a different filename
- `dotnet publish -t:PublishContainer` now **succeeds for all 7 services** — previously blocked for all of them by an `EventAndCommands.csproj` publish conflict, not just the three broken Dockerfiles
- Both new third-party GitHub Actions (`gitleaks-action`, `trivy-action`) are **pinned to full commit SHAs**, closing a supply-chain substitution risk
- `.github/dependabot.yml` — weekly NuGet + GitHub Actions dependency-update PRs; its first run opened 17 PRs at once, 16 consolidated into one and merged (PR #67), 1 excluded for a real conflict (`CommunityToolkit.Maui`, still open as PR #61)

**Added by F-019 (`v0.8.0`, 2026-08-27):**

- **Booking is now the pilot for a 4-project Clean Architecture split** — `Booking.Api` (thin, endpoints/DI
  only), `Booking.Core` (MediatR handlers), `Booking.Domain` (commands/queries/DTOs), `Booking.Infrastructure`
  (empty — YAGNI). F-020 will replicate this shape across the other 6 services.
- Every Booking command/query handler now dispatches through a **real `mediator.Send`**, not a
  hand-constructed call — `RequestCollection`, the workaround that existed only because handlers took
  per-request values as constructor parameters, is deleted.
- Handlers return **`FluentResults.Result`/`Result<T>`** instead of a string-sniffed `"exception"`-prefixed
  convention, mapped to a new **`DataResponse<T>`** envelope (`data`/`errors`/`success`) at the wire boundary.
- Validation migration from `MiniValidator` to **Validot**'s declarative `Specification<T>` DSL started: 3
  of Booking's 10 routes (Book, and the 2 note-content routes).
- A real, pre-existing bug — `PUT /appointments/`'s response echoed the client's forged
  `AppointmentStatus` even though the database write already correctly ignored it — is **fixed**;
  confirmed live under real traffic, not just in the integration suite.

---

## Shipped Features

*F-001–F-012 are marked `Shipped` in ROADMAP.md but predate PDLC ship tracking — no episode files, no CHANGELOG entries, no tags. `v0.1.0` is the first PDLC-tracked release.*

| # | Feature | Date Shipped | Episode | PR |
|---|---------|-------------|---------|-----|
| — | Pre-PDLC baseline | 2024-04-16 → 2026-07-30 | — | — |
| 001 | F-013 aspire-wiring (`v0.1.0`) | 2026-08-18 | [EPISODE_aspire-wiring_2026-08-17.md](../episodes/EPISODE_aspire-wiring_2026-08-17.md) | [#35](https://github.com/ogdevlabs/agenda-buddy/pull/35) |
| 002 | F-016 secure-public-endpoints (`v0.2.0`) | 2026-08-18 | [EPISODE_secure-public-endpoints_2026-08-18.md](../episodes/EPISODE_secure-public-endpoints_2026-08-18.md) | [#38](https://github.com/ogdevlabs/agenda-buddy/pull/38) |
| 003 | F-021 identity-hardening (`v0.3.0`) | 2026-08-22 | [EPISODE_identity-hardening_2026-08-22.md](../episodes/EPISODE_identity-hardening_2026-08-22.md) | [#39](https://github.com/ogdevlabs/agenda-buddy/pull/39) |
| 004 | F-014 wire-unreached-services (`v0.4.0`) | 2026-08-23 | [EPISODE_wire-unreached-services_2026-08-23.md](../episodes/EPISODE_wire-unreached-services_2026-08-23.md) | [#40](https://github.com/ogdevlabs/agenda-buddy/pull/40) |
| 005 | F-015 api-gateway-and-mobile-contract (`v0.5.0`) | 2026-08-24 | [EPISODE_api-gateway-and-mobile-contract_2026-08-24.md](../episodes/EPISODE_api-gateway-and-mobile-contract_2026-08-24.md) | [#41](https://github.com/ogdevlabs/agenda-buddy/pull/41) |
| 006 | F-017 container-and-cd-hardening (`v0.6.0`) | 2026-08-26 | [006_container-and-cd-hardening_2026-08-26.md](../episodes/006_container-and-cd-hardening_2026-08-26.md) | [#48](https://github.com/ogdevlabs/agenda-buddy/pull/48) |
| 007 | F-018 api-refactor-foundations (`v0.7.0`) | 2026-08-26 | [007_api-refactor-foundations_2026-08-26.md](../episodes/007_api-refactor-foundations_2026-08-26.md) | [#69](https://github.com/ogdevlabs/agenda-buddy/pull/69) |
| 008 | F-019 api-refactor-pilot-booking (`v0.8.0`) | 2026-08-27 | [EPISODE_api-refactor-pilot-booking_2026-08-27.md](../episodes/EPISODE_api-refactor-pilot-booking_2026-08-27.md) | none — merged directly (`fb91cb1`); `gh pr create` blocked, see episode's Links section |

---

## Architecture Summary

- **.NET Aspire orchestration** *(added F-013)*: `AgendaBuddy.AppHost` is the composition root for local development — it declares MongoDB and Kafka as container resources and all seven services plus the Gateway as projects, assigning ports dynamically. `AgendaBuddy.ServiceDefaults` is referenced by every service (and the Gateway) and supplies OpenTelemetry, health/liveness endpoints, service discovery, HTTP resilience, and the `PiiRedactingProcessor`. Docker Compose remains as a legacy fallback.
- **Seven ASP.NET Minimal API microservices**: Booking, Calendar, Customer, Provider, Services, Profession — plus **Identity** — each with its own test project. *(The "six" count predates Identity.)*
- **Booking is a 4-project Clean Architecture pilot** *(added F-019)*: `Booking.Api` (thin — endpoints/DI
  only), `Booking.Core` (MediatR command/query handlers), `Booking.Domain` (commands/queries/DTOs, the
  `DataResponse<T>` envelope), `Booking.Infrastructure` (empty — YAGNI). The other 6 services keep the
  original one-project-per-service shape until F-020 replicates this split across them.
- **`Gateway`, an eighth process** *(added F-015)*: a thin YARP reverse proxy in front of all seven services — `MobileApp`'s only configured base address. Explicit `api/v1/{service}/**` route allowlist, built from live Aspire service-discovery config, never a catch-all. No business logic, no auth validation — JWT passthrough only.
- **Shared Library project**: all domain entities (`AppointmentEntity`, `ProviderEntity`, `CustomerEntity`, `ServiceEntity`, `ProfessionEntity`), the generic `IRepository<T>` / `MongoDbRepository<T>`, domain services, and tools (CacheAside, EnumHelper) live here and are consumed by all services
- **CQRS via MediatR**: the shared `EventAndCommands` project holds all commands, queries, and their handlers; each handler calls Library services and persists an audit event to EventStore
- **Kafka**: Confluent stack (Kafka + Zookeeper + Schema Registry + Kafka UI) run via Docker Compose; per-provider topics created on-demand
- **MongoDB**: document store for all domain data; embedded sub-documents for provider services and appointments. One `IMongoClient` singleton is shared process-wide by all services and `EventStore` *(F-013)*; connection strings resolve via `MongoConnectionResolver` (Aspire → environment → appsettings).
- **Cache-aside pattern**: `CacheAside` extension on `IDistributedCache` with semaphore-guarded double-checked locking
- **CI security gates** *(added F-017)*: every PR gets an unconditional dependency-vulnerability audit + gitleaks secret scan (`security-scan`); every service-touching PR also builds each service via .NET SDK container support and Trivy-scans the image (`docker-build-and-scan`). No hand-written Dockerfile is built or scanned in CI — they serve only the legacy Compose path.

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
- ~~**CONSTITUTION §7's security-scan gate is still not automated.**~~ — **RESOLVED by F-017** (`v0.6.0`). `security-scan` (dependency audit + gitleaks, `if: always()` — unconditional on every PR) and `docker-build-and-scan` (7-service image build + Trivy) now run automatically; a canary test proves the secret scanner would have caught the class of leak `ISSUE-002` already experienced.
- ~~**No `.editorconfig`.**~~ — **RESOLVED by F-018** (`v0.7.0`). `.editorconfig` now encodes the project's actual conventions; CI enforces `dotnet format --verify-no-changes`.
- ~~**No integration-test harness.**~~ — **RESOLVED by F-016 + F-018** (`v0.2.0`/`v0.7.0`). `AgendaBuddy.IntegrationTests` now has Tier 1/2/3 coverage (route-contract, persistence, audit) across all 7 services, 301 tests. ⚠️ Two things remain: `Integration — real services + MongoDB` is **not yet a required status check** on `main`; §7's Integration checkbox stays unchecked pending 10 consecutive green runs (tracked: `agenda-buddy-ym9`).

**Added by F-018 (2026-08-26):**

- **`gitleaks-action`'s default PR-scan mode skips content merged via a non-fast-forward merge's second parent.** Fixed with a second, independent full-range scan step in `security-scan` — confirmed live on a real PR. Tracked: `agenda-buddy-wow` (P1).
- **Two audit-trail bugs found, not fixed:** `UpdateCustomerCommandHandler` audits failures under the wrong event `Type` (`agenda-buddy-id4`); `UpdateServicesFromProviderCommandHandler` writes no audit event on its provider-not-found branch (`agenda-buddy-f49`).
- **Booking/Customer's `RequestCollection.cs` carry the same dormant `IKafkaClient` downcast Provider had**, fixed only for Provider (`agenda-buddy-5og`).
- **AC-11 (image-pull diagnostics) and AC-14 (AppHost-already-running warning) were never built**, despite an earlier task-store note crediting them as delivered (`agenda-buddy-10g`).
- **7 `MongoDbConfiguration` classes + 7 interfaces** are kept alive solely by 3 tests. Delete with the tests, or convert those tests to the new path.
- **7 near-identical `ServiceCollectionMongoResolutionTest.cs`** (~150 lines each) — collapse to a shared theory when one next needs editing.
- **`AppHostWiring` mutates Aspire-produced `EndpointAnnotation`s** — revisit on any Aspire major upgrade.
- **`scripts/seed/seed-mongo.sh` is stale** — hardcodes `mongo:27017` and targets `ProviderDb`/`CustomerDb`, which no service reads.
- **`docs/pdlc/context/` describes pre-Aspire wiring** in places — refreshed incrementally at ship.
- ~~**`agenda-buddy-prr`** — `MobileApp` does not compile under `/p:MobileWorkloads=false`~~ — **RESOLVED and verified 2026-08-18.** The bead is closed and `MobileApp.Tests` passes 67 (7 skipped) locally. CI already runs three dedicated mobile jobs (`build-android`, `build-ios` on `macos-latest`, `build-mobile-tests`). MobileApp stays out of `agenda-buddy-backend.slnf` **by design**, not because it is broken. A stale comment in `.github/workflows/dotnet.yml` still claims otherwise.
- **Two advisory test gaps from Echo**: the guarded legacy `MongoDbConfiguration` ctor throw, and `ProfessionSeedHostedService.StartAsync` (which swallows exceptions, so a seeding bug surfaces only as an empty catalogue).
- ~~**Six shipped-but-unreachable capabilities**~~ — **RESOLVED by F-014** (`v0.4.0`). All six now have a route, authenticated and ownership-guarded, verified live against a running AppHost. Server-owned appointment status (ADR-037) and the non-charging payment gateway (ADR-038) shipped alongside.
- ~~**The mobile client cannot reach the backend**~~ — **RESOLVED by F-015** (`v0.5.0`). A Gateway now gives it one address; every route/verb/payload is corrected; `SeedDataProvider` is deleted. Verified live against a running AppHost on the merged commit.
- ~~**PII exposure on public endpoints**~~ — **RESOLVED by F-016** (`v0.2.0`), and demonstrated live at the Ship/Verify gate rather than by inspection: all five routes 401 anonymous, non-owners receive `ProviderSummary` only, both Calendar routes ownership-guarded.

**Added or exposed by F-016 (2026-08-18, recorded at the ship gate 2026-08-22):**

- ⚠️ **No cache invalidation exists anywhere in the solution** (`agenda-buddy-xrw`, P2). Nine `CacheAside.GetOrCreateAsync` read sites, zero `RemoveAsync`, 5-minute absolute TTL — so a provider who completes onboarding is absent from the discovery list for up to five minutes. Pre-existing; **found by running the system** at the Verify gate, where review had only inferred it (finding I-1). `CacheAside.cs:38` also re-reads the cached value after taking the semaphore and discards it, leaving the double-checked lock incomplete.
- **`GET /api/v1/customers` returns the full `CustomerEntity`** — including `SubscribedProviderCollection`, `AppointmentCollection` and `KafkaTopic` — to any Provider-role caller. Accepted at review (I-2) per ADR-026's deferral of owner-scoping, now quantified against the real payload.
- **Authorization failures are entirely unlogged** — there is no log sink at all, so IDOR probing leaves no trace. Advisory A-1; belongs to F-021/F-024.
- **`SSH.NET 2024.2.0` (HIGH, `GHSA-q939-rpr3-3284`)** enters the dependency graph via Testcontainers in `AgendaBuddy.IntegrationTests`. Unreachable — Testcontainers only loads it for Docker-over-SSH, which this project does not use — and the unreachability is *tested* by `ContainerRuntimeGuardTest`. Disposition: ADR-030.
- ~~**The standards-readiness gate has now blocked five consecutive gates and has never executed once.**~~ — **RETIRED 2026-08-23 (ADR-042).** Ten consecutive skips revealed the real reason: Agenda Buddy is a personal `fererelabs` project the six Nordstrom enterprise standards bodies never applied to, independent of the access problem. The gate no longer runs on this repository; `CONSTITUTION.md` §9 records the exemption.

**Added or exposed by F-021 (2026-08-22):**

- **The per-IP rate limiter is per-process** (T-106, accepted) and **collapses to one bucket behind a proxy that does not forward the client address** (`agenda-buddy-end`) — F-017's topology work owns `UseForwardedHeaders`.
- **`credentials` has no unique index on `email`** — confirmed live on the database (`agenda-buddy-b0w`); the one `createIndex` script that would create it is documented as stale.
- One pre-existing reflection-guard test deleted (ADR-034), same class as F-016's ADR-025 deletion — needs maintainer acknowledgement.

**Added or exposed by F-014 (2026-08-23, recorded at the ship gate):**

- **`ObjectId` does not round-trip through JSON** for any entity-returning route (`agenda-buddy-do5`) — pre-existing since the entities were written, only now visible because a create response's id needed to be read back. Fixed in Booking, Customer, Provider (the three this feature touches); Calendar, Services and Profession still emit the broken shape.
- **No `JsonStringEnumConverter` is registered anywhere** — every enum on this API's wire is an integer, and a string value 400s with no validation detail.
- **Revenue cannot be computed** — `AppointmentEntity` records no service, no fee, no amount (ADR-039). Filed rather than approximated; touches F-015's contract and F-025's rules.
- **The payment amount is unvalidated** for the same reason (T-205(c), accepted) — harmless with the default recording gateway, a real underpayment the moment a Stripe key is configured.
- **`NotificationService` is storage-only** — nothing calls `SendAsync` yet, so F-022's dependency on it is not yet satisfied.
- **No formal Party Review ran for this feature**, and no episode draft existed before the Ship gate — both a deviation from F-016/F-021 precedent, worth restoring next feature.

**Added or exposed by F-015 (2026-08-24, recorded at the ship gate):**

- **`Mobile — iOS/Android Build` and `Integration — real services + MongoDB` only trigger on push/PR to `main`** — a 14-task, 5-wave Construction phase produced two real defects (a namespace collision, a missing build flag) that sat undetected through 863 green tests because neither CI job had run even once until the Ship-gate PR. Recommend opening PRs as drafts at Construction start, not at Ship — see episode 005's Reflect Notes.
- **The Gateway's route allowlist is the single point where a new backend route group becomes invisible to `MobileApp` silently** — found and fixed once already (messages/notifications), the mechanism that caused it (a plan built against a stale context-catalog snapshot instead of the live route table) is unfixed. Any PR adding a backend route should be required to show `Gateway/AspireServiceDiscoveryProxyConfigProvider.cs`'s diff.
- **No formal Party Review ran for this feature either** — second consecutive occurrence after F-014.
- **T-301 (Gateway single point of failure) accepted, not mitigated** — re-score if a real (non-Aspire) deployment materializes.
- **A minor, unreproduced observation**: a `GET api/v1/customers` response briefly showed a zeroed `ObjectId` during the AC5 stopped-service test — not root-caused, noted rather than dropped.
- ⚠️ **`AppHostWiring.cs`'s cloud shape gives ingress to the wrong processes, found reviewing the docs, not exercised** (cloud deploy is deferred, ADR-035). All seven domain services get `.WithExternalHttpEndpoints()`; the Gateway gets none — backwards since F-015 shipped, when the mobile client stopped calling services directly. **Correction, 2026-08-26: F-017 shipped without touching this** — its actual PRD scope was container images and the CI security/build gates, not AppHostWiring's networking shape (confirmed: `AppHostWiring.cs:215` is unchanged). Still open, needs re-filing to a feature that actually owns it.
- **No working provider-subscription capability exists**, found reviewing customer onboarding immediately after F-015 shipped. `CustomerEntity.SubscribedProviderCollection` is a `List<string>?` (always supported many, not one), but no command, handler, or route exists to set it, and `UpdateCustomerCommandHandler` actively discards any client-supplied value for the field. Same shape as the six capabilities F-014 fixed, missed because the original feature (F-003) predates PDLC tracking. Filed as **F-026**.

**Added or exposed by F-017 (2026-08-26, recorded at the ship gate):**

- **Two distinct flakes surfaced during F-017, both plausibly the same "full-solution concurrent test run" root cause, neither root-caused.** `AgendaBuddy.AppHost.Tests` flaked once during Construction's Test sub-phase (77/87, clean on every re-run). `AgendaBuddy.ServiceDefaults.Tests.TelemetryPiiTest` — the `InProcessServerCollection`/cross-test `TracerProvider` interference already known from F-015's Reflect notes — flaked once more on PR #59 (a Dependabot bump unrelated to that test project). Both always clear on isolated re-run. Worth a dedicated investigation before either becomes a source of false-red PRs.
- **Gateway has zero CI coverage of any kind** — not in any path filter in `dotnet.yml`, so a Gateway-only change triggers no job at all. Pre-existing since F-015 shipped the Gateway; surfaced but not introduced by F-017's Party Review (finding I1's fix covers `security-scan` only, not `build-and-test`/`docker-build-and-scan`).
- **`Customer/Dockerfile` does not exist at all**, discovered while building F-017's Dockerfile-hygiene guard. Harmless today (the image-build CI job uses SDK container support, not Dockerfiles) but inconsistent with the other 6 services, which all still have one for the legacy Compose path.
- **Duplicate `RepoRoot()` test helper** copy-pasted across `DockerAndComposeHygieneTest.cs`, `PinnedThirdPartyActionsTest.cs`, `PublishContainerTest.cs`, and `SecurityScanAndDockerJobShapeTest.cs` in `AgendaBuddy.AppHost.Tests` — accepted as low-priority YAGNI `shrink:` polish at Review (ADR-047).
- **`Aspire.Hosting.AppHost` 13.5.3** (picked up via the post-merge Dependabot batch, PR #67) introduces a new build warning, `ASPIRE010` (`AgendaBuddy.AppHost` not using the Aspire CLI bundle) — informational only, not yet acted on.
- **`CommunityToolkit.Maui` is stuck at 9.1.1** — Dependabot's proposed 15.0.1 bump (PR #61, still open) fails with `NU1605`: it requires `Microsoft.Maui.Controls >= 10.0.90`, and `MobileApp.csproj` pins `>= 10.0.20`. Needs a coordinated MAUI SDK bump, not a routine dependency merge.

**Added or exposed by F-019 (2026-08-27, recorded at the ship gate):**

- **2 of Booking's 10 routes (Update, Cancel) still validate via `MiniValidator`, not Validot** — Requirement 6 reached 3/10, not 10/10. Never assigned to any F-019 task. Tracked: `agenda-buddy-02e`.
- **`POST /appointments` with a null `EmailProvider` 500s instead of 400/404** — pre-existing, unchanged by this refactor; confirmed the wire response still leaks no exception detail regardless. Tracked: `agenda-buddy-cy2`.
- **Mapster (ADR-049-approved) has zero call sites in this feature** — Requirement 7 (response DTOs keeping `AppointmentEntity` out of route signatures) was never assigned to any task. Not a defect; F-020 should not assume a usage pattern exists to copy.
- **`gh` cannot create OR merge PRs on this repo** (a step further than F-017/F-018's precedent, where creation worked and only merge was blocked) — the authenticated identity has only `READ` access, distinct from the `git`-configured commit identity. No pre-merge PR-triggered CI run is currently possible; every ship from here merges straight to `main` and relies on the resulting push-triggered CI run instead. Worth fixing the `gh` auth setup before the next feature that wants pre-merge CI confidence.
- ~~**Episodes 006/007 written to the wrong location**~~ — disclosed, not retroactively fixed (both are shipped, permanent records). Episode 008 restores the project's own convention (`docs/pdlc/episodes/`). See `episodes/index.md`'s note.

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
10. **Auth hardened: partial updates, configuration-gated controls, warn-don't-fail** (ADR-032…034, F-021) — `IRepository<T>.FindOneAndUpdateAsync` is the one partial-update primitive and never upserts; HSTS and rate limiting are gated on configuration rather than `IsProduction()` because every service runs as Production under the local AppHost; each control warns loudly, naming the key, when off outside a local run. Cloud deployment itself is deferred until every pending feature ships and legacy tech debt is discharged (ADR-035).
11. **Six unreachable capabilities land on three existing services, by data ownership, not an eighth service** (ADR-036, F-014) — a service is a deployment unit, not a URL prefix. Appointment status becomes server-owned via the entity's own transition methods (ADR-037); payments are non-charging unless a Stripe key is configured, assigned once at construction (ADR-038); the provider report states a revenue figure is unavailable rather than publish one it cannot compute correctly (ADR-039).
12. **A Gateway *is* an eighth service, for the mobile client only** (F-015) — unlike ADR-036's decision for domain capabilities, `MobileApp` needs one address across seven dynamically-ported services, which no existing service can provide without becoming something else. The Gateway's single-instance posture is accepted as a local-dev-scoped risk (ADR-040, T-301). The Nordstrom standards-readiness gate is **retired outright** for this project (ADR-042) — ten consecutive unreachable-source skips resolved into an explicit exemption: this is a personal project, not a Nordstrom engagement.
13. **Booking's Clean Architecture pilot: 4 projects, in-repo `DataResponse<T>`, 4 packages not 5** (ADR-049, F-019) — `Booking.Api`/`Core`/`Domain`/`Infrastructure`, chosen over a 3- or 5-project split. A planned `SmallApiToolkit` dependency was dropped pre-Design (it doesn't ship a response-envelope type this project needs); `DataResponse<T>` is authored in-repo instead. FluentResults, Validot, GuardClauses, and Mapster are the 4 approved packages — Mapster shipped with zero call sites this feature, disclosed rather than assumed used.

See `docs/pdlc/memory/DECISIONS.md` for full ADR entries.
