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

## v0.3.0 — 2026-08-22

Hardens the auth system itself (F-021). F-016 established that no endpoint leaks PII; this release makes the
equivalent claim about signing in and staying signed in. Every defect was reproduced as a failing test before
being fixed, and each control was then observed against a live stack rather than inferred from a green suite.

### Fixed
- **A token refresh could permanently destroy an account.** `RefreshAsync` deleted the entire credential
  document and re-inserted it, so any fault between the two lost the email, password hash, role and reset
  flag irrecoverably — with no audit trail and no log line. Worse, the re-insert sat inside the
  `IsMongoDown` catch, so **the destructive path was the handled path**: a transient database blip returned
  a tidy 503 to a user whose account no longer existed. A mobile client refreshes hourly.
  Rotation is now a single atomic update whose filter carries the presented token hash, the expiry check and
  a not-locked condition. The document is never deleted, so there is no window in which it does not exist.
- **`AssertOwner`'s null-claim pass** — no change needed; already closed by F-016. Recorded because F-021's
  inherited scope listed four items and delivered three.

### Added
- **Rate limiting on `POST /api/v1/auth/login` and `POST /api/v1/auth/register`** — per source IP, sliding
  window, default 10 requests/minute, returning **429** with `Retry-After`. Both routes, not just login:
  BCrypt at work factor 12 was **measured at 262 ms** on the reference machine, and `register` hashes at the
  same cost. That measurement also reframed the threat — password guessing runs at ~3.8 attempts/sec/core,
  while the same cost lets an attacker spend *the server's* CPU, so roughly 4 unauthenticated requests per
  second pin a core of the service that issues every other service's tokens.
- **Account lockout** — consecutive failed logins are counted with an atomic increment; after 10 (default)
  the account locks for 15 minutes and then **unlocks itself**. No permanent lock and no administrative
  unlock: password reset does not exist yet, so a lock needing a human to clear it would strand a real
  provider — and let an attacker strand one deliberately. A refusal-because-locked is indistinguishable from
  a wrong password in status code and body.
- **HSTS**, off by default, conservative `max-age`, no `includeSubDomains` and no `preload` — those are the
  hard-to-reverse parts. Emitted only over TLS.
- **Credential-mutation logging.** Identity previously had no log sink at all. Create, rotate, lock, reset
  and session-end are now recorded with an operation, an outcome and a one-way `acct_<hash>` reference —
  never the address, because span redaction does not cover logs.
- `IRepository<T>.FindOneAndUpdateAsync(filter, update)` — one narrow partial-update primitive, post-image,
  and it **never upserts**, which is what stops a failed login for an unknown address creating an account
  (ADR-032).
- Two optional fields on the credential document: `failed_attempts` and `lock_until`. No migration: an
  absent counter reads as zero and an absent lock reads as unlocked.

### Changed
- **`UseHttpsRedirection` now runs before `UseAuthentication` in all seven services.** It previously ran
  last, so a bearer token was parsed and validated out of a plaintext request before the redirect was
  issued. Identity's `if (!IsDevelopment())` guard around its redirect is gone — under the AppHost that
  condition was always true, so it never meant what it looked like it meant.
- Login and logout no longer replace the whole credential document to update the refresh token.
- Both new controls are gated on **configuration**, not on `IsProduction()`, and default **off** — services
  run as Production under the local AppHost, so the environment name cannot tell a laptop from a deployment.
  The AppHost turns them on in its cloud graph and marks a local run as local; each service warns loudly at
  startup, naming the key, when a control is off outside a local run (ADR-033).

### Not shipped in this release
- **Cloud deployment is deferred by decision, not by blocker** (ADR-035): Azure is not reviewed until every
  pending feature is complete and the no-longer-needed tech debt is discharged. This is the third
  consecutive release without a remote deployment, and the first where that is a schedule rather than a gap.
- Rotating the committed Atlas credential remains outstanding and does **not** wait for the above.

## v0.2.0 — 2026-08-18

Closes unauthenticated PII exposure across the six domain services, and adds the integration harness that
makes endpoint authorization verifiable at all (F-016).

Every defect below was reproduced as a failing test before being fixed. Before this release no route table in
the solution was executed by any test — which is why the Calendar IDOR could exist unnoticed.

### Breaking Changes
- `GET /api/v1/providers`, `/providers/{email}`, `/customers`, `/customers/{email}` and `/services/{email}`
  now require authentication. They previously returned full records to anonymous callers, including embedded
  appointments carrying customer email addresses and each provider's subscribed-customer list.
- `POST /api/v1/professions` is **removed**. Professions are seeded reference data, no shipped flow creates
  one, and Identity's role allow-list is exactly `{Provider, Customer}` — there is no administrative role to
  gate the route on (ADR-025).
- `GET /api/v1/providers` and `/customers` return `{items, totalCount, page, pageSize}` instead of a bare
  JSON array, and return `200` with `items: []` where they previously returned `204` (ADR-023).
- `GET /api/v1/providers` and `/providers/{email}` return `ProviderSummary` to any caller who is not the
  owning provider: no appointments, no subscribed customers, no Kafka topic.
- Nothing could reach these routes before this release, which is why the contracts changed now — the mobile
  client's paths and base URL are both wrong (F-015 fixes that against the new shapes).

### Fixed
- `GET /api/v1/calendar/availability/{email}` and `/calendar/appointments/{email}` now enforce ownership. Any
  authenticated user could previously read any provider's full appointment list, including every customer
  email in it. Every sibling service already guarded; Calendar was the one that did not.
- `OwnershipGuard.AssertOwner` no longer treats a missing `sub` claim as ownership. `string.Equals(null,
  null)` is `true`, so a token carrying no subject, checked against an entity with no email, passed the guard.
- `ForbiddenException` maps to **403 in every environment**. It previously reached the client as 403 only
  where an endpoint hand-wrote a `try/catch`; elsewhere it was a 500, and in `Production` a bare
  empty-bodied one (ADR-022).
- Query handlers no longer serialise their full result payload into the `events` collection. A single
  anonymous list call previously wrote every provider — with embedded appointments and customer emails —
  into a collection that is unbounded, unindexed and never pruned. The payload is now the result *size*.
- `AgendaBuddy.IntegrationTests` matched no CI path filter, so a change to the harness alone ran zero jobs.

### Added
- `GET /api/v1/customers` requires the `Provider` role, not merely a token. Registration is anonymous and
  unrate-limited, so authentication alone left the whole customer table pageable — pagination bounds the
  response, not the extraction (ADR-026).
- `POST /api/v1/providers` requires the `Provider` role **and** that the record is the caller's own. A role
  check alone still allows one provider to register a record under another provider's email. This is also the
  first time `OwnershipGuard.AssertRole` is called anywhere in the solution.
- Pagination on both list endpoints: `?page=&pageSize=`, capped at 100 and **clamped rather than rejected**,
  with the response echoing the size actually applied. Paged at the database, so the bound applies to the
  extraction and not just the response.
- `Event.actor` — audit records attribute reads to the calling `sub` claim. Nullable and additive, so no
  backfill; the actor of a historical anonymous read is genuinely unknown (ADR-027).
- `AgendaBuddy.IntegrationTests` — the first integration suite in the solution. Real services over HTTP
  against a MongoDB Testcontainer, one container per test class and a database per test, with a
  **fail-closed guard** that refuses to run unless the resolved endpoint is this session's own container.
  Identity is compared by host and port, not by hostname shape, because a tunnel to a remote cluster also
  presents as `127.0.0.1`. 99 tests.
- A separate, duration-enforced integration CI job, with the budget asserted in the step so growth has to be
  argued for in a reviewed change.

### Changed
- `EventAndCommands/Persitency` renamed to `Persistence`, pinned by a test so a revert fails rather than
  passing silently.
- Test suites: **531** tests across three separate commands — 358 backend, 99 integration, 74 mobile. The
  integration suite is deliberately outside `agenda-buddy-backend.slnf` so the unit gate stays Docker-free
  (ADR-031), which means the backend command does not run it.

### Known limitations
- `GET /api/v1/customers` still returns full customer records to any `Provider`-role caller, including the
  customer-to-provider relationship graph. Owner-scoping is the stronger fix and was deferred (ADR-026).
- Authorization failures are not logged anywhere — there is no log sink — so repeated probing leaves no trace.
- `SSH.NET` carries a HIGH advisory with no patched version. It reaches the build only through Testcontainers
  and only supports Docker-over-SSH, which this project does not use; a test asserts it is never loaded
  (ADR-030).

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
