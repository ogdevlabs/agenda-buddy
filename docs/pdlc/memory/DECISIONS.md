# Decision Registry

**Project:** Agenda Buddy
**Last updated:** 2026-07-30

<!-- PDLC Decision Registry (ADR format).
     Entries are appended by:
     - User: via /decide <text>
     - Agents: during Construction/Review (Step 14) and Reflect (Step 7)
     Each entry records: what was decided, who decided, why, what was considered,
     and what cross-cutting impacts were applied.
     This file is append-only. Mark superseded decisions as [SUPERSEDED by ADR-NNN]. -->

---

## ADR-001 — Event-driven microservices architecture *(pre-PDLC, inferred)*

**Date:** April 2024
**Status:** Accepted

**Decision:** Build Agenda Buddy as six independent domain microservices (Booking, Calendar, Customer, Provider, Services, Profession), each an ASP.NET Minimal API with its own MongoDB config and Dockerfile, communicating via Kafka.

**Context:** The domain naturally decomposes into independent bounded contexts. Each service can be deployed, scaled, and tested independently. The README's feature list (provider management, customer registration, calendar booking) maps cleanly onto separate services.

**Inferred from:** Directory structure, individual `Program.cs` files, separate `*.csproj` files, Docker Compose config.

---

## ADR-002 — MongoDB as primary datastore *(pre-PDLC, inferred)*

**Date:** April–May 2024
**Status:** Accepted

**Decision:** Use MongoDB with the official MongoDB.Driver for all persistence. Data modelled as documents with embedded sub-documents (e.g., services and appointments embedded in ProviderEntity).

**Context:** The provider/customer/appointment domain has variable-structure nested data that fits naturally into a document model. MongoDB was chosen over a relational DB.

**Inferred from:** `MongoDbRepository<T>`, all `[BsonElement]` attributes, `MongoDbConfiguration` in every service.

---

## ADR-003 — CQRS with MediatR for command handling *(pre-PDLC, inferred)*

**Date:** May 2024
**Status:** Accepted

**Decision:** Implement CQRS via MediatR 12. Commands (write operations) and queries (read operations) are separated in the shared `EventAndCommands` library. Handlers consume Library domain services.

**Context:** Clean separation of reads and writes makes each operation independently testable and auditable. The EventAndCommands project acts as a shared messaging kernel consumed by all microservices.

**Inferred from:** `EventAndCommands/Commands/`, `EventAndCommands/Queries/`, MediatR package references, `IRequestHandler<TRequest, TResponse>` implementations.

---

## ADR-004 — Event sourcing via EventStore *(pre-PDLC, inferred)*

**Date:** May 2024
**Status:** Accepted

**Decision:** Every command handler persists a success or failure event to the `EventStore` (MongoDB `Events` collection) for audit and replay purposes.

**Context:** Provides an audit trail of all mutations. Enables future replay or debugging without relying solely on the current state of domain collections.

**Inferred from:** `EventAndCommands/Persitency/EventStore.cs`, `EventAndCommands/Persitency/Event.cs`, usage in all command handlers.

---

## ADR-005 — Shared Library project for domain entities and services *(pre-PDLC, inferred)*

**Date:** April 2024
**Status:** Accepted

**Decision:** All domain entities (`AppointmentEntity`, `ProviderEntity`, `CustomerEntity`, `ServiceEntity`, `ProfessionEntity`), the generic `IRepository<T>` / `MongoDbRepository<T>`, and domain services (`BookingService`, `ProviderService`, etc.) live in the shared `Library` project consumed by all microservices and EventAndCommands.

**Context:** Avoids duplication of entity definitions across services. All services share the same domain model.

**Inferred from:** `Library/` project structure, `ProjectReference` to Library in all `*.csproj` files.

---

## ADR-006 — Cache-aside distributed caching *(pre-PDLC, inferred)*

**Date:** July 2024
**Status:** Accepted

**Decision:** Implement a cache-aside pattern via a `CacheAside` extension method on `IDistributedCache` with semaphore-guarded double-checked locking (5-minute default TTL).

**Context:** Added to improve read performance as service call volume grows. The semaphore prevents cache stampedes under concurrent load.

**Inferred from:** `Library/Tools/CacheAside.cs`, commit "Adding distributed cache to services" (2024-07-04).

---

## ADR-007 — Kafka per-provider topics *(pre-PDLC, inferred)*

**Date:** June 2024
**Status:** Accepted

**Decision:** Each provider gets a dedicated Kafka topic named `{email-prefix}-topic` (e.g., `john-topic` for `john@example.com`). Topics are created on-demand via `KafkaClient.CreateTopicIfNotExist`.

**Context:** Isolates message streams per provider, making it easy to consume only a specific provider's events without filtering.

**Inferred from:** `Kafka/KafkaClient.cs`, topic name derivation logic in `Booking/Program.cs`, `ProviderEntity.KafkaTopic`.

---

## ADR-008 — JWT asymmetric RSA signing (F-001) *(design decision)*

**Date:** 2026-07-30
**Status:** Accepted
**Feature:** F-001 auth-and-identity

**Decision:** Use RS256 (asymmetric RSA) for JWT signing. The private key lives in the Identity service only, injected via `JWT_PRIVATE_KEY` env var. The public key is distributed to all six consumer services via `JWT_PUBLIC_KEY` env var. Symmetric signing (HS256) was rejected.

**Context:** Asymmetric signing eliminates cross-service forgery risk — no consumer service ever holds the signing key. A compromised consumer cannot be used to forge tokens.

**Resolved via:** Progressive Thinking escalation (user decision B).

---

## ADR-009 — Passive JWT expiry after logout; no jti blocklist (F-001) *(accepted risk)*

**Date:** 2026-07-30
**Status:** Accepted
**Feature:** F-001 auth-and-identity
**Risk reference:** Threat model T-001 (adjacent)

**Decision:** After `POST /auth/logout`, the refresh token is deleted server-side but the access token remains valid until its natural 60-minute expiry. A jti blocklist (per-request DB lookup to validate token liveness) was considered and deferred.

**Context:** A blocklist adds a MongoDB read on every authenticated request across all six services — latency and operational complexity not justified at current scale. The 60-minute window is an accepted trade-off for v1.

**Resolved via:** Progressive Thinking escalation (user decision A). Residual risk documented.

---

## ADR-010 — Single role per account, v1 (F-001) *(design decision)*

**Date:** 2026-07-30
**Status:** Accepted
**Feature:** F-001 auth-and-identity

**Decision:** Each credential account holds exactly one role: `Provider` or `Customer`. A user cannot hold both roles simultaneously in v1. Multi-role accounts are deferred.

**Context:** Simplifies the claim model, ownership checks, and test matrix. Provider + Customer dual-role adds edge cases in every handler-level ownership check. Revisit when a concrete use case emerges.

**Resolved via:** Progressive Thinking escalation (user decision A).

---

## ADR-011 — No rate limiting on /auth/login in v1 (F-001) *(accepted risk)*

**Date:** 2026-07-30
**Status:** Accepted — deferred to security-hardening feature
**Feature:** F-001 auth-and-identity
**Risk reference:** Threat model T-001, T-007

**Decision:** `/auth/login` has no rate limiting, IP-based throttling, or account lockout in v1. bcrypt cost factor 12 provides per-attempt slowdown (~100–300ms). Rate limiting and brute-force protection are deferred to a dedicated security-hardening feature.

**Context:** Platform has no public users at time of F-001 delivery. Threat actor profile is opportunistic. bcrypt raises the floor. Full rate-limiting implementation warrants its own design (IP vs. email-based, distributed state, lockout UX).

**Re-evaluation trigger:** When the platform accepts public registrations or if any auth anomaly is detected in logs.

---

## ADR-012 — Email as JWT `sub` claim (F-001) *(accepted risk)*

**Date:** 2026-07-30
**Status:** Accepted
**Feature:** F-001 auth-and-identity
**Risk reference:** Threat model T-008

**Decision:** The JWT `sub` claim contains the user's email address (lowercase). An opaque UUID sub was considered but rejected.

**Context:** All six services use email as the join key for ownership checks (`EmailProvider`, `EmailCustomer` on `AppointmentEntity`; `Provider.Email`; `Customer.Email`). An opaque sub would require a sub→email lookup on every ownership check in every handler, adding per-request DB calls. The email in the JWT payload is visible to the authenticated user (they own it). PII-in-logs prohibition in CONSTITUTION.md §4 is the guard against accidental exposure.

**Known implication:** If a user changes their email in their profile (F-002/F-003), their existing access token carries the old `sub` until natural 60-minute expiry. Ownership checks fail for that window; user must re-login.

---

## ADR-013 — .NET Aspire for local orchestration (F-013) *(design decision)*

**Date:** 2026-08-17
**Status:** Accepted
**Feature:** F-013 aspire-wiring
**Risk reference:** R-1 (driver compatibility), threat T-001 (committed credential), T-002 (probe amplification), T-003 (dashboard exposure)

**Decision:** Adopt .NET Aspire **13.4.6** for local orchestration: an `AgendaBuddy.AppHost` project that provisions MongoDB and Kafka as containers and launches all seven API services, plus an `AgendaBuddy.ServiceDefaults` library giving every service OpenTelemetry, health checks, service discovery, and HTTP resilience. Aspire's **hosting** packages only — the `Aspire.MongoDB.Driver` **client** integration is excluded.

**Context:** The solution could not be started. Every service read `MongoDB:ConnectionString`, which existed only in `appsettings.Development.json`; there was no health model, no telemetry, and `EventStore` opened a new `MongoClient` per request scope. Starting the stack meant running seven projects by hand with an undocumented gitignored `.env`.

**Per CONSTITUTION §9, the packages this adds:** `Aspire.AppHost.Sdk`, `Aspire.Hosting.AppHost`, `Aspire.Hosting.MongoDB`, `Aspire.Hosting.Kafka` (13.4.6, AppHost only); `Microsoft.Extensions.Http.Resilience` and `Microsoft.Extensions.ServiceDiscovery` (10.9.0) plus five `OpenTelemetry.*` (1.17.0) in ServiceDefaults; and `Microsoft.Extensions.Configuration.Abstractions` + `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` (10.0.0) in `Library`, which had neither. All are first-party; no new vendor and no lock-in beyond Aspire itself, which is confined to the AppHost and ServiceDefaults.

**R-1 outcome — the escape hatch was taken.** Established empirically in F-013-T01, not assumed:

- `Aspire.MongoDB.Driver` 13.4.6 requires `MongoDB.Driver >= 3.9.0` on every target framework. Referencing it beside the pinned 2.25.0 fails restore with `NU1605` (warning-as-error).
- The 2.x-era alternative does not help: Aspire 9.5.2's client integration requires `[2.30.0, 3.0.0)` — still a conflict — and would pin the orchestration stack two majors back on a .NET 10 SDK. `Aspire.MongoDB.Driver.v3` is retired.
- Upgrading the driver was explicitly out of scope: it is a second migration hiding inside this one, and 2.25.0 is the reason for three CVE pins in `Directory.Build.props:18-28`.

So services register `AddSingleton<IMongoClient>` over `MongoConnectionResolver.Resolve` and use a custom `MongoHealthCheck`. The hosting side is unaffected — `Aspire.Hosting.MongoDB` resolves driver 3.9.0 inside the AppHost's own graph while each service keeps 2.25.0, verified as a 0-warning build against the real `Directory.Build.props`. **The outcome is simpler than the conditional design it replaced:** one registration path, not two.

**Alternatives rejected:**

| Option | Why rejected |
|---|---|
| Fix Docker Compose instead | Cheapest route to "one command", but delivers no health model, no telemetry, no resilience, and no connection-string injection. The Development-only configuration defect would survive, so AC-4.1 fails. |
| Project Tye | Archived. |
| Shell script wrapping seven `dotnet run`s | No dependency provisioning, no health model, no telemetry; keeps every hardcoded port. |
| Adopt `IOptions<T>` throughout | The right long-term fix for stringly-typed configuration, but touches far more than the three seams this feature owns. Deferred. |
| Upgrade `MongoDB.Driver` to 3.x | Would retire three CVE pins, genuinely attractive — but it is a second migration. Excluded; revisit as its own feature. |

**Consequences:**

- **A container runtime is now a hard requirement** for the local path. Previously optional, now the stack does not start without it.
- **Docker Compose is retained but superseded** (R-4, E-12), along with every legacy configuration key, so rollback is a single `git revert` with no loss of capability.
- **Host ports are dynamic.** Aspire pinned them by two independent routes — the launch profile and `Kestrel:Endpoints` in `appsettings.json` — and both are neutralised in the AppHost. `scripts/seed/seed-mongo.sh` is consequently stale (it also targets databases no service reads); recorded, not fixed (E-8).
- **Connection-pool behaviour changed**: from one client per request scope to one per process. This is the intended fix (AC-4.3), but it is a real runtime behaviour change, not a refactor.
- **The committed Atlas credential was removed from tracked files, which does not remediate the disclosure.** It remains in git history and stays valid until rotated at Atlas. Rotation and a cluster access-log review are outstanding operational actions (threat T-001, PRD OQ-1); merging F-013 does not close them.
- **The CONSTITUTION §7 security scan is still not implemented.** F-013's CI adds a single-pattern credential assertion, which is not a scanner. Deferred to F-017.
- **No integration-test harness exists**, so AC-1.1, AC-1.2, AC-1.3, AC-3.2 and AC-4.1 are verified manually (E-7). F-013-T10 records that attestation.
- F-014 … F-017 filed as follow-ups.
