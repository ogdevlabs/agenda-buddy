# Changelog

**Project:** Agenda Buddy

<!-- Format: Conventional — newest entries at top.
     Each entry added by Jarvis during the Ship sub-phase.
     Format per release:

     ## v[X.Y.Z] — [YYYY-MM-DD]
     ### Added
     - ...
     ### Changed
     - ...
     ### Fixed
     - ...
     ### Breaking Changes
     - ... (only if applicable)
-->

---

## v0.1.0 — 2026-08-18

First PDLC-tracked release. Wires the solution as a .NET Aspire application (F-013).

### Added
- `AgendaBuddy.AppHost` — Aspire orchestration for nine resources: MongoDB, Kafka, and all seven services (Booking, Calendar, Customer, Provider, Services, Profession, Identity). One `dotnet run` starts the whole graph.
- `AgendaBuddy.ServiceDefaults` — OpenTelemetry traces and metrics, health and liveness endpoints, service discovery, and HTTP resilience, applied uniformly across the seven services.
- `MongoConnectionResolver` and `MongoHealthCheck` in `Library` — resolve the Mongo connection string from Aspire, environment, or `appsettings.json`, in that order.
- `PiiRedactingProcessor` — strips personal data from exported spans before it leaves the process.
- Cloud deployment capability: `azure.yaml`, `.github/workflows/deploy.yml`, and a `DeploymentTarget.Cloud` shape of the AppHost. **Written and unit-tested, never executed** — no deployment has been performed.
- CI: path filters, an AppHost build step, two guard assertions, and an in-step JWT keypair so the guards need no repository secrets.

### Changed
- All seven services and `EventStore` now share one `IMongoClient` singleton. `EventStore` was `Scoped` and built a client, connection pool, and monitoring threads **per HTTP request** — every command and query handler writes an audit event, so this ran on every request. This is a runtime behaviour change, not a refactor.
- `KafkaClient.BootstrapServers` reads from configuration instead of a hardcoded broker address.
- Profession seeding moved from a `.Wait()` on a network call at DI-registration time to a hosted service. Its test suite dropped from 30 s to 168 ms.
- `IRequestCollection` is registered `Scoped`. As a singleton consuming a scoped `IEventStore`, it formed a captive dependency that DI validation rejected — and DI validation runs only in `Development`, the environment Aspire uses. Six of seven services could not start.
- README documents the AppHost workflow and how to provision the three AppHost secrets on a new machine.
- Deleted dead `IMongoDbConfiguration` registrations.

### Fixed
- **The AppHost never launched the seven services** (ISSUE-001). `AgendaBuddy.AppHost/Properties/launchSettings.json` was missing, so `DOTNET_ENVIRONMENT` was unset and the AppHost ran as `Production`. User secrets load only in `Development`, so every secret parameter resolved to `ValueMissing` and all seven services parked in `Waiting` — with nothing logged at any level below Debug. Deleting that file silently breaks the entire graph.
- `WithReference(database)` injects `ConnectionStrings__agenda-buddy`, not the `ConnectionStrings:mongodb` that `MongoConnectionResolver` reads. This crashed `profession` on startup.
- `MobileApp` did not compile under `/p:MobileWorkloads=false` (`CS0103 'Application'`), which failed the `build-mobile-tests` CI job outright — all 67 MobileApp tests had never run in CI.

### Breaking Changes
- **Traces no longer export `url.path`.** It was carrying customer email addresses out of the process. Consumers must read the `http.route` template instead of the raw path.
- **The committed `agenda_buddy` Atlas connection string was removed from 17 tracked files.** Local setups that relied on it must now supply `Parameters:mongodb-password` through user secrets. ⚠️ **Removal is not rotation** — the credential remains in git history and remains valid until the password is changed at Atlas. See `docs/issues/ISSUE-002-atlas-credential-rotation.md`.

---

## Pre-PDLC baseline — 2024-04-16 to 2026-07-30

### Existing functionality (pre-PDLC, documented at PDLC initialization)

#### Added
- Provider registration: add, update, and deactivate provider profiles (name, email, services, appointments)
- Provider service catalog: define services with name, description, fee, and fee type (Hourly/Fixed/Subscription)
- Customer management: register and update customer profiles linked to providers via subscriptions
- Appointment booking: create, update, and cancel appointments with status lifecycle (Requested → Booked → Completed)
- Calendar management: check provider availability and list existing calendar appointments
- Profession categories: 100+ seeded profession categories for provider classification
- Kafka topic management: auto-create a per-provider Kafka topic on provider registration
- Event sourcing: all commands persist a success/failure event to EventStore (MongoDB)
- Cache-aside distributed caching: IDistributedCache wrapper with semaphore-guarded double-checked locking
- GitHub Actions CI: dotnet restore → build → test → coverage on push/PR to main

*Note: This entry documents the state of the repository before PDLC was introduced.
       Future entries will be generated by Jarvis during each Ship sub-phase.*

---
