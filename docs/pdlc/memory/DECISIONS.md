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
