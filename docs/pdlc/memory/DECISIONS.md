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

---

## ADR-014 — API refactor as a three-stage programme, MediatR retained as the single dispatcher (F-018) *(design decision)*

**Date:** 2026-08-18 · **Status:** Accepted

**Context.** The endpoint layer has ten evidenced defects (string-sniffed control flow, discarded `CancellationToken`s, persistence entities as the public API contract, MediatR registered but never dispatching, a ~40-line exception block duplicated across all seven `Program.cs` files). The maintainer asked to restructure it following [Gramli/AuthApi](https://github.com/Gramli/AuthApi): full Clean Architecture, five new packages, plus a Testcontainers harness and the `Persitency` rename. Scoped as one feature this reached ~46 projects and was too large for a single PRD.

**Decision.** Deliver the **full Clean Architecture target** in three staged features rather than reducing it: **F-018** foundations (harness, rename, governance — no endpoint changes), **F-019** pilot on `Booking` only, **F-020** rollout to the remaining six. Ordering is deliberate: the integration-test harness must exist *before* the endpoint rewrite, because episode 001 established that both of F-013's real defects were invisible to review and surfaced only by running the software.

**MediatR is retained as the single dispatcher.** The reference does CQRS *without* MediatR and uses `SmallApiToolkit`'s `IHttpRequestHandler` as its dispatch mechanism; adopting both would put two competing dispatchers in one codebase. Endpoints will call `mediator.Send(command)` — which finally honours CONSTITUTION §3 and removes the hand-constructed `new SomeCommandHandler(...)` calls. `IHttpRequestHandler` is explicitly **not** used.

**Consequences.** CONSTITUTION §3 is preserved rather than overridden. `SmallApiToolkit` is reduced to `DataResponse<T>`, the validation base class and `ExceptionMiddleware`. F-019 additionally depends on **F-016**, so the unauthenticated full-record endpoint is closed before the rewrite restructures it.

**Alternatives rejected.** One large feature (too large to plan or review). Dropping MediatR for full reference fidelity (would reverse a standing constraint for no gain, since the fix is to *start* using MediatR properly).

---

## ADR-015 — Adopt five packages from the reference implementation (F-018) *(design decision)*

**Date:** 2026-08-18 · **Status:** Accepted · **Amends:** CONSTITUTION §9

**Context.** CONSTITUTION §9 requires discussion before adding packages and asks for a minimal footprint. The reference uses `FluentResults`, `Validot`, `Mapster`, `GuardClauses` and `SmallApiToolkit`.

**Decision.** All five approved. §9 is amended to record the approval. **Used in F-019/F-020, not F-018** — F-018 grants the approval and records the reasoning; no production code consumes them yet.

**Recorded caveat.** `SmallApiToolkit` is the reference author's own library and its README scopes it to *"small-scale or example web APIs"*, with production-readiness applying "primarily to the core handler pattern". We take the narrow slice (`DataResponse<T>`, validation base, `ExceptionMiddleware`) and not the dispatch abstraction, which partially limits the exposure. If it proves unmaintained, the slice we use is small enough to vendor.

**Consequences.** F-019 must front-load a restore check against `net10.0` + `MongoDB.Driver` 2.25.0 before building on any of them — F-013 lost a task to exactly this class of assumption (`Aspire.MongoDB.Driver` required driver ≥ 3.9.0 and failed restore with `NU1605`). Partially de-risked already: `Testcontainers.MongoDb` 4.14.0 restores and runs cleanly on that combination.

---

## ADR-016 — Validot replaces MiniValidator (F-018) *(design decision)*

**Date:** 2026-08-18 · **Status:** Accepted · **Amends:** CONSTITUTION §4

**Context.** §4 mandates *"Input validation via `MiniValidator` at every API endpoint"*. Today that is literally true and is part of the problem — `MiniValidator.TryValidate` is repeated at the top of every endpoint, one of the four blocks duplicated across all seven services.

**Decision.** `Validot` replaces `MiniValidator`, with validation moved off the endpoint into a validation base class. §4 is amended.

**Consequences.** §4 no longer describes the code until F-019 lands, so the amendment records both the target and the transition. The duplication disappears only when endpoints are rewritten; F-018 changes no endpoint.

---

## ADR-017 — Testcontainers integration harness, one container per test class (F-018) *(design decision)*

**Date:** 2026-08-18 · **Status:** Accepted

**Context.** No integration tests exist, and CONSTITUTION §5's "all integration tests pass" has been unsatisfiable since initialization. Nothing asserts the §3 audit invariant, so F-019/F-020 could delete the audit trail with CI staying green.

**Decision.** A Testcontainers-backed harness in a single project, `AgendaBuddy.IntegrationTests`, kept **out of** `agenda-buddy-backend.slnf` so the unit job cannot accidentally start containers. Three assertion tiers: route contract, persistence round-trip, audit fired.

**One container per test *class*, not per test — reversed on measurement.** Discover chose container-per-test against an *assumed* 1–3 s startup. A pre-Design spike measured **4.45 s** (4436 / 4471 / 4475 ms, σ≈20 ms) — 2–3× the estimate. At F-019's expected 60–100 tests that is 4.5–7.4 minutes of pure container startup on a 2 CPU / 4.1 GB VM. Isolation is preserved instead by a **unique database name per test** inside the shared container, which delivers the same isolation the original choice was made for, at effectively zero cost.

**Also decided.** Kafka is **not** containerised — `IKafkaClient` is substituted with a recording fake, because Kafka here only creates topics and nothing is produced or consumed. Tier 3 reads the persisted document **directly with `MongoDB.Driver`**, not through `IEventStore`, so the assertion survives F-019/F-020 refactoring that abstraction.

**Consequences.** The 10-minute CI budget becomes comfortable rather than marginal; AC-21 remains as a tripwire. This converges on what Echo argued in Progressive Thinking Conflict A — settled by measurement rather than debate.

---

## ADR-018 — Container images pinned by tag, not digest (F-018) *(accepted risk)*

**Date:** 2026-08-18 · **Status:** Accepted · **Threat:** T-005 (MEDIUM, mitigate later)

**Context.** The harness pins images by tag (`mongo:7.0.14`). Tags are mutable, so a repointed or compromised upstream tag would execute attacker-controlled code on developer machines and CI runners. Digest pinning (`mongo@sha256:…`) removes that.

**Decision.** Pin by tag for now; defer digest pinning.

**Rationale.** Digest pinning adds a real update burden — every upgrade edits a hash — and the marginal risk over a pinned *patch* tag from a first-party image is modest. Friday's dissent (that the burden outweighs the risk here) is recorded and was accepted.

**Revisit when.** The harness pulls any non-first-party image, or a supply-chain incident touches a base image in use.

---

## ADR-019 — `InternalsVisibleTo` on seven production assemblies (F-018) *(accepted risk)*

**Date:** 2026-08-18 · **Status:** Accepted · **Threat:** T-006 (MEDIUM, accept)

**Context.** `WebApplicationFactory<Program>` requires the entry-point type to be visible. The services use top-level statements, so `Program` is internal. Each of the seven services therefore gains `<InternalsVisibleTo Include="AgendaBuddy.IntegrationTests" />` permanently. The assemblies are not strong-named, so the grant is to a *name*, not an identity — any assembly built with that name can reach their internals.

**Decision.** Accept.

**Rationale.** Exploitability is low: adding a project to the build requires repository write access, which is a larger problem than this grant. The alternatives are worse trades for a test-only need — making `Program` public widens the real API surface, and strong-naming seven assemblies is disproportionate.

**Revisit when.** The repository moves to a model where untrusted contributors can add projects, or the assemblies are strong-named for another reason.

---

## ADR-020 — OpenAPI specs generated in CI but not committed until F-016 (F-018) *(design decision)*

**Date:** 2026-08-18 · **Status:** Accepted · **Threat:** T-003 (MEDIUM, resolved at the Step 12 gate)

**Context.** The maintainer chose to adopt committed OpenAPI specs as a contract baseline, over Neo's objection that F-019/F-020 will churn them — the deciding argument being that F-015's mobile/backend route mismatch survived the project's entire life because no artifact ever made the contract diffable. Threat modelling then observed that the repository is **public**, and that a committed spec documents which endpoints are anonymous — including `GET /api/v1/providers`, which F-016 exists to fix because it returns full provider records unauthenticated and unpaginated.

**Decision.** Middle path: **generate and drift-check the specs in CI from day one; do not commit them** until F-016 closes that endpoint. Committing becomes an **F-016 exit criterion** so the deferral cannot be forgotten.

**Consequences.** The mechanical drift protection exists through exactly the period F-019/F-020 change contracts. AC-17 is reworded from "committed" to "generated and drift-checked"; AC-19's baseline becomes the previous run's artifact (or a checked-in hash manifest) rather than the spec body. The residual severity is lower than first assessed anyway — the endpoint returns synthetic data — but it remains an unauthenticated full-record dump.

**A separate, unenforced obligation (threat T-007, deferred).** The spec only delivers its stated value if diffs are *read*. CI can force regeneration; it cannot force review. Making that real — CODEOWNERS on the spec path, or a required PR label — is deferred to F-019/F-020, the features that will actually change the contract.

---

## ADR-021 — Threat-derived security ACs added to the F-018 PRD post-Define *(process record)*

**Date:** 2026-08-18 · **Status:** Accepted

Threat modelling runs at Design Step 10.5, after the Define gate closed. The three "mitigate now" threats it produced were therefore back-written into the F-018 PRD as acceptance criteria **28 (T-001)**, **29 (T-004)** and **30 (T-002)**, and materialized as structured `[security]` ACs on tasks `F-018-T08` and `F-018-T06` via `tasks.cjs ac add`.

This is a logged addendum, not a Define reopen. Recording it here because adding acceptance criteria after approval is a governance act that should be auditable — and because the *reason* matters: an AC (not a task) is what the build TDD gate enumerates and what `tasks.cjs done` mechanically refuses to close without a linked test. A threat recorded only as a task-body citation is invisible to both.
