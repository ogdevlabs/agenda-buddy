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

## ADR-011 — No rate limiting on /auth/login in v1 (F-001) *(accepted risk)* — **[SUPERSEDED by ADR-033]**

**Date:** 2026-07-30
**Status:** **Superseded 2026-08-22 by F-021.** `login` and `register` are now rate-limited per IP, and
consecutive failed logins lock an account for a self-clearing window. Two of this ADR's premises turned
out to be wrong, and the way they were wrong is worth keeping:
its bcrypt-cost estimate of "~100–300 ms" was accurate (**262 ms measured** at work factor 12 on this
hardware), but it read that cost only as a defence — the same 262 ms is what makes **unauthenticated CPU
exhaustion** trivial, because the attacker spends the server's CPU, not their own (threat T-101). And its
re-evaluation trigger, "if any auth anomaly is detected in logs", could never have fired: Identity had no
log sink at all until F-021 added one.
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

---

## ADR-022 — `UseExceptionHandler` moves outside the `IsDevelopment()` guard in the six domain services

**Date:** 2026-08-18 · **Status:** Accepted · **Feature:** F-016 · **Design ref:** `ARCHITECTURE.md` AD-1

**Context.** F-016 requirement 14 asks for `ForbiddenException` to map to 403 centrally, so that an endpoint which forgets the local `try/catch` cannot silently return 500 instead of 403. Design discovered this **cannot be satisfied as scoped**: in all seven services `UseExceptionHandler` is registered *inside* `if (app.Environment.IsDevelopment())`, alongside Swagger (`docs/pdlc/context/10-error-handling.md:9-34`). A mapping added to that lambda would yield 403 in Development and a **bare, empty-bodied 500 in Production** — preserving the exact failure the requirement exists to remove, in the only environment that matters.

**Decision.** Implement `AgendaBuddyExceptionHandler : IExceptionHandler` in `Library.ServerAuth`, register it with `AddExceptionHandler<T>()`, and call `app.UseExceptionHandler()` **unconditionally** in the six domain services. It maps `ForbiddenException` → 403 with ProblemDetails and returns `false` for everything else, so the existing Development-only lambda continues to handle what it handles today. The two coexist.

**Consequences.**
- **Production error behaviour changes for six services** — this is the reason an ADR exists rather than absorbing the change. Today Production emits an empty 500 for any unhandled exception; afterwards `ForbiddenException` emits a well-formed 403 and everything else still emits 500. A strict improvement, and a behavioural change beyond the feature's literal scope.
- `IExceptionHandler` is the .NET 8+ idiomatic form and is used **nowhere else in this codebase** — F-016 introduces the pattern. F-019/F-020 should generalise it, not reinvent it.
- **Deliberately not done:** the nine other exception types that incorrectly surface as 500 (`ArgumentException`→404, `KeyNotFoundException`→404, `UnauthorizedAccessException`→403, `InvalidOperationException`→409, `FormatException`→400, `MongoException`→503). Each changes the contract of an endpoint F-016 does not otherwise touch, with no acceptance criterion behind it. The handler is structured so each is a one-line addition later. `FormatException` from `new ObjectId(badId)` is the most likely live 500 and the best candidate to take next.
- **Identity is excluded.** It uses an incompatible ad-hoc `{ error, message }` envelope and is the only service without `ProblemDetailsServiceEndpointFilter`. Registering the handler there would put two error schemes in one service. F-021 touches Identity next; unification belongs with it.

---

## ADR-023 — Paginated list response contract

**Date:** 2026-08-18 · **Status:** Accepted · **Feature:** F-016 · **Required by:** PRD AC-16 · **Consumed by:** F-015

> **Implemented and verified 2026-08-18 by F-016-T15.** AC-16 requires this ADR to exist before the endpoint
> work closes; it did, and the contract was implemented as written. Three things the implementation
> established that the ADR did not say:
>
> 1. **Paging is at the database, not after the fact.** The query handlers call
>    `GetPagedAsync(skip, take)` (T10's primitive). Reading everything and slicing in the endpoint would
>    bound the *response* while leaving the *extraction* unbounded — the opposite of the point. Threading a
>    `PageRequest` down cost 12 files across two read paths: endpoint → `EventsHelper` →
>    `IRequestCollection`/`RequestCollection` → query handler → domain service → repository.
> 2. **The cache key must carry the page.** Both list routes cache, and the pre-existing keys were
>    `"providers"` / `"customers"`. Without `-p{page}-s{pageSize}` appended, page 2 serves page 1's entry.
>    Invisible in any single-page test.
> 3. **`skip` arithmetic is overflow-guarded.** `(page - 1) * pageSize` overflows to a *negative* skip for a
>    large page, and a negative skip is what the Mongo driver rejects — a 500 on an attacker-controlled
>    input. `PageRequest.Clamp` bounds the page so the product cannot overflow, with a test at
>    `int.MaxValue`.
>
> `PageRequest.Clamp` and `PagedResponse<T>` live in the new `Library/Dtos/` folder alongside
> `ProviderSummary` (T11). ⚠️ **`GET /api/v1/providers` returns `PagedResponse<ProviderSummary>`** — the
> projection and the envelope compose, and the list is homogeneous; see the `api-contracts.md` §5.1
> correction.

**Context.** `GET /api/v1/providers` and `GET /api/v1/customers` return unbounded bare JSON arrays; an uncapped list endpoint is the dump F-016 exists to remove. `IRepository<T>` (verified by reading `Library/Repositories/IRepository.cs`) exposes `GetAllAsync()` and `FindAllAsync(BsonDocument)` and **no skip, limit or count**. F-015 will write the mobile client against whatever shape is chosen, so this is a contract, not an implementation detail — AC-16 requires it recorded before the endpoint work closes.

**Decision.**

Request: `?page=<int, 1-based, default 1>&pageSize=<int, default 25>`.
Response envelope: `{ items: T[], totalCount: long, page: int, pageSize: int }`.
**`MaxPageSize` = 100.** Out-of-range values are **clamped server-side, never rejected**; the response echoes the **effective** `page`/`pageSize` after clamping.
One new repository primitive: `Task<(IEnumerable<TEntity> Items, long TotalCount)> GetPagedAsync(int skip, int take)`.

**Rationale for clamping over rejecting.** A 400 would tell an attacker the exact boundary and leave an honest client no way to discover the cap. Clamping plus echoing the effective value lets a correct client paginate and lets nobody probe. The cap is a **security control**, not ergonomics.

**Consequences.**
- **Breaking change**, taken deliberately now because these routes have **zero reachable consumers** — the mobile client's paths and base URL are both wrong (`01-api-surface.md:158`). Doing it after F-015 would mean writing the client twice.
- `204 No Content` for an empty collection is **retired**; empty pages return `200` with `items: []` and `totalCount: 0`, so a client always gets a parseable body.
- `IRepository<T>` gains a method, so **every implementer changes** — `MongoDbRepository<T>` and `Identity.Tests/Helpers/InMemoryRepository.cs`. The latter is a test helper and cheap, but if missed, `Identity.Tests` stops compiling.
- **Accepted debt with a named trigger:** `skip`/`limit` degrades linearly with offset. Immaterial on synthetic data; the fix at scale is keyset pagination, which **would change this contract**. Revisit *before* real user data lands, not after — by then F-015 depends on this shape.

---

## ADR-024 — Accepted risk: no audience scoping and no token revocation (threat T-008 deferred to F-023)

**Date:** 2026-08-18 · **Status:** Accepted (deferral) · **Feature:** F-016 · **Threat:** T-008 (MEDIUM) · **Owner:** F-023

**Context.** F-016 adds the solution's first authorization checks that read `sub` and `role` from the bearer token. Behind them there is no audience scoping: `ValidateAudience = false` and no `aud` claim is issued, so **all seven services accept any token this issuer minted** (`13-security.md:71`). And there is no revocation: `jti` is minted and never recorded, so an access token stays valid up to 60 minutes after logout (`:77`).

**Decision.** Accept for F-016; defer to **F-023 `token-revocation`**.

**Rationale.** Introducing `aud` and enabling `ValidateAudience` is a token-format change requiring coordinated updates to Identity's minting and all seven validators — inside a feature that deliberately excludes Identity (ADR-022's last bullet). Revocation needs a denylist store, and the current `AddDistributedMemoryCache()` is per-process and **cannot back a cross-service denylist** (`00-overview.md` finding 7). Neither is a one-task fix.

**Residual risk.** A token obtained through any flow is a universal key across all seven services for up to 60 minutes, including after logout. Partially offset by strict validation that F-016 does not weaken: RS256 with `ValidAlgorithms = ["RS256"]` blocks algorithm confusion, `ClockSkew = Zero` removes the grace window, and asymmetric signing means only Identity can mint. **The validation is strict; the scoping is absent.**

**Re-evaluation trigger.** Before F-016 or F-021 ships to any environment holding real user data.

---

## ADR-025 — `POST /api/v1/professions` is deleted rather than role-gated (threat T-007)

**Date:** 2026-08-18 · **Status:** Accepted · **Feature:** F-016 · **Threat:** T-007 (MEDIUM) · **Supersedes:** F-016 PRD requirement 13

**Context.** PRD requirement 13 asked for `AssertRole` on `POST /api/v1/professions` so an arbitrary authenticated Customer could not write to the global reference catalogue. **Bolt found there was no role to check for**: Identity's allow-list is exactly `{Provider, Customer}` (`Identity/Program.cs:100-106`) — there is no administrative tier. The only implementable check, `AssertRole(user, "Provider")`, would still let any self-registered provider write shared reference data read by every user. With open, unverified, unthrottled registration, that raises the bar from "any account" to "any account that picked `Provider` at signup."

**Decision.** **Delete the route**, together with its handler wiring and its `RequestCollection` / `EventsHelper` write path. The two profession **read** routes stay anonymous and unchanged.

**Rationale.** Professions are seeded from `Library/Data/ProfessionSeedData.cs` and no shipped flow creates one. Removing surface is strictly stronger than guarding it, needs less code, and avoids inventing an `Admin` role inside a feature that excludes Identity.

**Rejected alternatives.** *Introduce an `Admin` role* — architecturally correct, but touches Identity's allow-list, token minting and seeding; real scope creep. *Accept `Provider`-only with an ADR* (Atlas's preference) — defensible pre-launch, but carries risk in writing for no benefit once deletion is available.

**Consequences.** Requirement 13 is **superseded, not dropped** — its intent (a Customer must not write the global catalogue) is fully satisfied by removal. `AddProfessionCommand` and `AddProfessionCommandHandler` become unreachable; they are left in place rather than deleted, since the refactor program (F-019/F-020) will audit dead handlers systematically. **If professions ever need to be user-creatable, that is a feature with a real authorization model — not a route quietly restored.**

---

## ADR-026 — `GET /api/v1/customers` requires the `Provider` role, not merely authentication (threat T-003)

**Date:** 2026-08-18 · **Status:** Accepted · **Feature:** F-016 · **Threat:** T-003 (HIGH) · **Scope:** addition beyond the approved PRD

**Context.** PRD requirement 9 makes `GET /api/v1/customers` authenticated. Threat modelling established that **authentication alone is nearly worthless there**: `POST /api/v1/auth/register` is anonymous, unverified and unrate-limited, so an attacker self-registers as a `Customer`, obtains a valid token, and pages through the entire customer table exactly as before — and `totalCount` tells them how many pages to fetch. **Pagination bounds each response; it does not bound extraction.**

Atlas reframed it as a product question rather than a control question: *who is this endpoint for?* `ROADMAP.md` F-003 `customer-onboarding-flow` (Shipped) defines discovery as customers finding **providers**, not each other. No flow lists every customer. The only defensible caller is a provider.

**Decision.** Require the `Provider` role on the **list** route. The maintainer approved this as an explicit scope addition at the Step 12 gate.

**Consequences.**
- Cost is one line — the same primitive as the `POST /api/v1/providers` role check — so the marginal cost over approved scope is near zero. No UX cost: no shipped screen consumes the route.
- **Only the list is gated.** `GET /api/v1/customers/{email}` stays authenticated-but-not-role-gated, because a customer legitimately reads their own record through it.
- **Deferred, not rejected:** scoping results to the calling provider's own `SubscribedCustomerCollection` is the stronger fix and was weighed at the gate. It is a genuine behaviour change and more work; the role check blocks the actual attack path now. Recorded so the stronger option remains a known follow-up rather than a forgotten one.
- The 200-vs-404 enumeration oracle on the single-record route is **narrowed, not closed** — any authenticated caller can still probe which emails are registered. Deliberate: 404 is kept for consistency with the eight existing call sites.

---

## ADR-027 — `Event` gains an `actor` field; F-016 stops being schema-change-free (threat T-005)

**Date:** 2026-08-18 · **Status:** Accepted · **Feature:** F-016 · **Threat:** T-005 (MEDIUM)

**Context.** F-016 requirement 16 reduces query-handler audit writes from full result payloads to metadata, closing a PII-amplification path where `GetProvidersQueryHandler.cs:23` serialised the entire provider list into the `events` collection on every call. But `Event` has **no actor field** (`15-cqrs-and-messaging.md:215`), so the reduced record reads *"a `GetProvidersQuery` succeeded at 14:03"* — with no indication of who. The change is a net gain in confidentiality and a net **regression in accountability**: the PII dump at least revealed *what* was accessed.

Until F-016 these endpoints had no authenticated caller to record. **This feature is the first point at which an actor exists.**

**Decision.** Add a nullable `Actor` property to `Event` (`[BsonElement("actor")]`), populated from the caller's `sub` claim.

**Amended 2026-08-18 during F-016-T18 — the mechanism, which Design got wrong.** `ARCHITECTURE.md` §5 costed this as *"one `[BsonElement]` and one assignment per handler."* That is not achievable: **no query handler has any access to the caller.** `ClaimsPrincipal` is dropped at the endpoint, the nine query objects carry no properties, and `RequestCollection` hand-constructs each handler from domain data. `IHttpContextAccessor` was registered nowhere in the solution.

Two viable implementations were put to the maintainer, who chose the second:

| | Where the actor is set | Files | Notes |
|---|---|---|---|
| **A** | each handler, via a new parameter | ~30 — 6 × `EventsHelper`, 6 × `IRequestCollection`, 6 × `RequestCollection`, 9 handlers, 9 query types | What §5 described. Widens six public interfaces to carry an audit field, and can be **half-done**: miss one handler and that path silently loses attribution. |
| **C — chosen** | `EventStore.SaveAsync`, from `IHttpContextAccessor` | ~8 | Attribution is a property of *writing an audit record*, not of each handler. One seam, cannot be half-done, and it attributes the **11 command handlers** for free — same field, no extra scope. `AddEventStore()` calls `AddHttpContextAccessor()` itself, so no service `Program.cs` changes at all. |

**Accepted cost of C:** `EventAndCommands` gains a `FrameworkReference` on `Microsoft.AspNetCore.App` and its kernel becomes ASP.NET-aware, which it was not before. Nothing is added to any deployed artifact (all seven consumers are ASP.NET Core apps), but it is a real coupling. **If F-019/F-020 ever needs the kernel HTTP-free, the seam is a small `IAuditActorProvider` interface owned by `EventAndCommands` and implemented in `Library.ServerAuth`** — recorded so that is a known move rather than a rediscovery. Side effect: three `Microsoft.Extensions.*` package references became redundant under the framework reference (NU1510) and were removed.

The "what counts as an actor" decision is a pure function, `AuditActor.From(ClaimsPrincipal?)`, so it is testable without a request, a container or a mocking framework. Null is a correct answer in three live cases: a hosted service, an anonymous read, and a token carrying no `sub` (the threat T-001 shape).

**Consequences.**
- **`data-model.md` is no longer a no-schema-change document.** F-016's revert leaves harmless unread residue rather than no trace. This was **Friday's recorded dissent** at the threat party — a clean revert is a genuinely valuable property for a feature changing authorization across five services — and it is the cost being accepted.
- **No backfill migration.** The field is nullable, MongoDB is schemaless, and nothing reads `actor` for control flow. A backfill is impossible anyway: the actor for a historical anonymous read is genuinely unknown, and inventing one would be worse than a null.
- Echo's counter-argument carried against accepting the regression: there is **no log sink** and `requestId` is not exported anywhere (`10-error-handling.md:138`), so nothing outside the `events` collection is durable. There was no fallback attribution to rely on.
- **What `actor` is not:** it records the `sub` claim from a validated token. It is not tamper-evident, not signed, and not joined to `jti` (minted, never recorded). It answers "which account did this" for incident response; it is **not a non-repudiation control.**

---

## ADR-028 — F-016 scope amendments discovered at Design *(process record)*

**Date:** 2026-08-18 · **Status:** Accepted · **Feature:** F-016

Design and threat modelling changed the approved F-016 PRD in four ways. Recorded together because amending an approved PRD is a governance act that should be auditable, and because three of the four came from **threat modelling finding things document review did not**.

| # | Amendment | Origin |
|---|---|---|
| 1 | **Requirement 18 reassigned from F-021 into F-016.** The response-shape projection reuses `OwnershipGuard.AssertOwner`, whose null-claim pass (`string.Equals(null, null)` is `true`) then lands on the **owner** branch and returns the unprojected entity. The hole exists today but is *unreachable* at these routes — F-016 is what makes it reachable, so F-016 must fix it. | Threat T-001, found by Neo→Phantom cross-talk |
| 2 | **Requirement 14's approach replaced** — see ADR-022. | Design, `10-error-handling.md` |
| 3 | **Requirement 13 superseded** by route deletion — see ADR-025. | Threat T-007, found by Bolt attempting the implementation |
| 4 | **Scope addition: `GET /api/v1/customers` role-gated** — see ADR-026. | Threat T-003, reframed by Atlas as a product question |

**Also broadened at Design, without an ADR because it is a straightforward scope correction:** PRD requirement 16 named `GetProvidersQueryHandler.cs:23` as "the specific offender", but **all ten query handlers** follow the identical publish→query→audit shape and `GetCustomersQuery` serialises every customer record. The design covers all ten; PRD AC-17 tests only the provider path and is flagged at the Plan gate to be broadened.

**Process observation worth keeping.** Amendment 3 was produced by *trying to implement* the requirement — Bolt went to write the role check and found the role did not exist. That is a class of finding no amount of document review produces, and it argues for keeping an implementation-feasibility lens in the threat party rather than treating it as purely analytical.

---

## ADR-029 — Threat-derived security ACs added to the F-016 PRD post-Define *(process record)*

**Date:** 2026-08-18 · **Status:** Accepted · **Feature:** F-016

Threat modelling runs at Design Step 10.5, after the Define gate closed. The **seven** "mitigate now" threats it produced were back-written into the F-016 PRD as acceptance criteria **20 (T-002)**, **21 (T-001)**, **22 (T-003)**, **23 (T-004)**, **24 (T-005)**, **25 (T-006)** and **26 (T-007)**, and materialized as structured `[security]` ACs on tasks `F-016-T06`, `T09`, `T16`, `T08`, `T18`, `T13` and `T17` via `tasks.cjs ac add`.

A logged addendum, not a Define reopen — same pattern as ADR-021 for F-018. Recorded because adding acceptance criteria after approval is a governance act that should be auditable, and because the *reason* matters: an AC (not a task) is what the build TDD gate enumerates and what `tasks.cjs done` mechanically refuses to close without a linked test. A threat recorded only as a task-body citation is invisible to both.

`tasks.cjs check` now reports **7 `security-ac-untested` findings for F-016** (plus 3 pre-existing for the paused F-018). Expected and correct until Build links the tests.

**Notable relative to F-018:** F-018's threat model produced three mitigate-now threats; F-016's produced seven, and **five of its eight threats were introduced or made newly reachable by the feature itself** rather than inherited. That is the signature of threat-modelling a security fix rather than a greenfield capability, and it is why the party was worth convening at Full depth.

---

## ADR-030 — Accepted risk: `SSH.NET` GHSA-q939-rpr3-3284 (HIGH) enters the graph via Testcontainers, unreachable and untreatable by pinning

**Date:** 2026-08-18 · **Status:** Accepted · **Feature:** F-016 (T02) · **Severity:** HIGH · **Reachability:** none, and *tested*

**Context.** Adding `Testcontainers.MongoDb` — approved in ADR-015's five-package set and validated by F-018's spike — pulls `SSH.NET` transitively, which carries advisory **GHSA-q939-rpr3-3284 (HIGH)**.

**Pinning cannot fix it.** Attempted and measured, not assumed:

| Attempt | Result |
|---|---|
| `Testcontainers.MongoDb` 4.0.0 | SSH.NET 2023.0.0 — flagged |
| 4.1.0 | SSH.NET 2024.1.0 — flagged |
| 4.3.0 / 4.6.0 | SSH.NET 2024.2.0 — flagged |
| explicit pin 2024.2.1 | flagged |
| explicit pin 2025.0.0 (latest) | **flagged** |

Every published version is covered by the advisory. There is no safe version to pin to, so the repo's existing `Directory.Build.props` transitive-pin mechanism — which fixed Snappier, SharpCompress, Newtonsoft.Json and Microsoft.OpenApi — does not apply here.

**Decision.** Accept, on the basis that the vulnerable code is **unreachable in this solution**, and make that basis a *test* rather than a claim.

`SSH.NET` is in the graph only to support Docker-over-SSH. This project talks to a local socket (Rancher Desktop). `AgendaBuddy.IntegrationTests/Harness/ContainerRuntimeGuardTest.cs` starts a real MongoDB container and asserts that **no SSH.NET assembly is loaded** while doing so. Verified passing 2026-08-18.

**Why a test and not a comment.** A comment saying "we don't use SSH" decays the moment someone sets `DOCKER_HOST=ssh://…` to use a remote builder. The test fails at that point, which converts a silent risk change into a build failure. This is the same reasoning that turned threat T-006's cache-ordering invariant from prose into a test.

**Consequences.**
- `NU1903` is suppressed **in `AgendaBuddy.IntegrationTests` only**, never solution-wide, with the full rationale inline in the csproj.
- **The suppression does not hide it from an audit.** Verified: `dotnet list package` with the vulnerability report still lists SSH.NET as High after the `NoWarn`. So **F-017's dependency-audit gate is unaffected** — it will report this, and it should.
- **This will be the first finding F-017's scanner reports**, and the expected disposition is "accepted, see ADR-030" rather than "fix". Recorded here so F-017 does not treat it as new.
- **Re-evaluation triggers:** a patched SSH.NET is published (drop the pin and the NoWarn); or anyone configures a remote Docker host over SSH (the guard test fails first); or Testcontainers drops the dependency.

**Honest note on posture.** CONSTITUTION §7 marks a dependency audit "always required, cannot be unchecked", and it remains unimplemented (F-017 owns it). Introducing a HIGH-severity advisory while that gate is down is exactly the situation the gate exists to prevent. It is accepted here because the alternative is abandoning the harness — which contradicts ADR-015, ADR-017, F-018's passed spike, and the entire verification premise of F-016 — and because unreachability is demonstrated rather than argued. **A different maintainer could reasonably decide otherwise, and would be entitled to.**

---

## ADR-031 — `AgendaBuddy.IntegrationTests` is excluded from `agenda-buddy-backend.slnf`

**Date:** 2026-08-18 · **Status:** Accepted · **Feature:** F-016 (T02)

**Context.** `agenda-buddy-backend.slnf` is what CI's `api` job and the documented local command (`dotnet test agenda-buddy-backend.slnf`) target. The new integration project requires a running container runtime.

**Decision.** Add the project to `agenda-buddy.sln` (so the solution is complete for IDE and tooling) but **not** to `agenda-buddy-backend.slnf`. The integration suite is invoked by targeting its `.csproj` directly, and F-016-T20 gives it a dedicated CI job.

**Rationale — this follows an established precedent in this repo, it is not a new pattern.** `MobileApp` and `MobileApp.Tests` are excluded from the slnf by design and covered by three dedicated CI jobs (`build-android`, `build-ios`, `build-mobile-tests`) that target their csproj. The integration project has the same shape: a real external prerequisite the unit gate should not inherit.

**Consequences.**
- **The unit gate stays Docker-free.** Folding container tests into the slnf would make `dotnet test agenda-buddy-backend.slnf` — documented in `CLAUDE.md` and run by CI's `api` job — fail on any machine without a container runtime. That is a significant regression in the fast feedback loop for no benefit.
- **The 305/309 headline count stays meaningful.** AC-19 counts the backend slnf; mixing in container tests would make the number depend on whether Docker was running.
- **Duration stays honest.** Measured on the maintainer's machine: **3 s warm, 62 s cold** per container (the cold figure is the 1.13 GB `mongo:7.0` pull). T20 must enforce a duration budget, which is far easier for a job that contains only integration tests.
- **Cost:** two commands instead of one, and a project that a naive `dotnet test agenda-buddy-backend.slnf` will not run. Mitigated by T20's blocking CI job — without it this would be the wrong trade, which is precisely why T20 was absorbed at the Plan gate.

---

## ADR-032 — One partial-update primitive on `IRepository<T>`, shared rather than Identity-only

**Date:** 2026-08-22 · **Status:** Accepted · **Feature:** F-021 (T01)

**Context.** `RefreshAsync` rotated a refresh token by deleting the whole `CredentialEntity` and re-inserting it (`IdentityService.cs:135`→`:155`). Any fault between those lines destroyed the account irrecoverably — and because the re-insert sat inside `catch … when (IsMongoDown(ex))`, **the destructive path was the handled path**: a transient database blip returned a tidy 503 to a user whose account no longer existed. The atomic delete was a correct single-use-token guard; its **granularity** was the defect. `IRepository<T>` offered no primitive that could express "change this one field": `UpdateAsync` replaces the entire document.

**Decision.** Add exactly one member to the shared interface:

```csharp
Task<TEntity?> FindOneAndUpdateAsync(BsonDocument filter, BsonDocument update);
```

Post-image (`ReturnDocument.After`), never upserting, `BsonDocument` in and out.

**Rationale.**
- **Smallest thing that fixes the defect.** Rotation needs "match these conditions, apply this change, atomically, and tell me what you matched" — and the conditions have to be in the *filter*, because that is what makes single use atomic without a delete.
- **`$inc`, `$set` and `$unset` all fit it**, so the failed-attempt counter, the lock, the counter reset and the logout unset are the same primitive. No second method.
- **Post-image earns its keep**: the incremented counter comes back with the write, so the lockout decision costs no extra query.
- **No upsert is a property of the primitive, not of its callers.** AC-9 ("a failed login for an unknown email creates nothing") is therefore not something each call site has to remember.
- **Shared, because two more features need it.** F-014 wires six capabilities that currently read-modify-write; F-019/F-020 rewrite this layer. Adding it once here is the cheapest point.

**Alternatives rejected.** A transaction around delete-and-insert — heavier, needs a replica set, and still deletes. An Identity-only method — the next caller re-solves it. A general query-builder — explicitly forbidden by PRD requirement 3, and the thing this convention exists to avoid.

**Blast radius, measured.** Exactly **two** implementers, both updated by this feature: `MongoDbRepository<TEntity>` and `Identity.Tests/Helpers/InMemoryRepository.cs`. Verified by grep over `: IRepository<`, the same sweep F-016 ran across its 19 changed symbols. Mocked usages (`Mock<IRepository<T>>` in six `Library.Tests` service tests) are unaffected by an added member.

**Consequences.**
- Coverage splits three ways, as it did for `GetPagedAsync` (F-016-T10): contract in `Library.Tests`, semantics against the in-memory implementer in `Identity.Tests`, and **MongoDB's own behaviour** in `AgendaBuddy.IntegrationTests/Harness/CredentialUpdatePrimitiveTest.cs`. The third one is new here — F-016 recorded "no test of `GetPagedAsync`'s Mongo semantics" as debt, and this feature does not repeat that.
- `PRD requirement 3` is enforced by a test that counts the interface's update members, so the next overload has to be argued for.
- Two whole-document replacements were removed from Identity while it was open: the login refresh-token write and the logout unset. Neither was the reported defect, but both replaced a credential document from a stale read.

---

## ADR-033 — Security controls gated on configuration, not on `IsProduction()`, with the AppHost declaring "local"

**Date:** 2026-08-22 · **Status:** Accepted · **Feature:** F-021 (T05, T06) · **Threat:** T-103

**Context.** Rate limiting and HSTS must be on when deployed and off on a developer's machine. The intuitive switch is `IsProduction()`, and here it is **wrong**: every service runs as **Production under the local AppHost**, because `AppHostWiring.cs` adds each project with `launchProfileName: null` while `launchSettings.json` sets `DOTNET_ENVIRONMENT=Development` for the AppHost process alone. Verified independently at Design: `/swagger/v1/swagger.json` returns 404 on all seven running services. An environment-gated HSTS would emit `Strict-Transport-Security` for `localhost` — which browsers cache stickily, for the whole `max-age`, across projects — and an environment-gated limiter would throttle every local run.

**Decision.** Gate both on explicit configuration (`Security:Hsts:Enabled`, `Security:RateLimiting:Enabled`), default **off**, and have the **composition root state which kind of run this is**: the AppHost injects `Security__Local=true` for a local run and `Security__Hsts__Enabled=true` (plus the limiter for Identity) for a cloud publish.

**Rationale.** The marker is the part that makes "default off" safe. Without it a service cannot tell "off because this is a laptop" from "off because a deployment forgot a key" — which is threat T-103, and the same failure shape as F-016's original defect, where `AssertRole` was present in the codebase and never called by anything. With it, each service warns loudly at startup when a control is off outside a local run, naming the exact key to set.

**Warn, do not fail fast** (the R4 question carried from Define). A config slip should be visible and fixable, not an outage: refusing to start would turn a missing key into downtime for a service six others depend on for token validation.

**Consequences.**
- Turning both flags off returns the services to exactly pre-F-021 behaviour, so the feature is revertible by configuration in an incident. Neither the limiter middleware nor its policy is even registered when the flag is off, so this is not merely a runtime branch.
- The cloud graph turns the controls on **in code**, which means shipping without them takes an edit to `AppHostWiring.cs` rather than an omission somewhere else. Asserted by `AppHostWiringTest` for all seven services.
- The integration harness can switch both on (`ServiceHostFixture.StartService(settings:)`), so neither control can ship unexercised (AC-15).
- A standalone `dotnet run` and `scripts/generate-openapi.sh` count as local via `IsDevelopment()`, so they emit no warning.
- **`UseHttpsRedirection` is deliberately NOT flag-gated.** Six services already called it unconditionally, so a flag defaulting to off would silently remove an existing control and one defaulting to on would be decorative. It is a no-op wherever no HTTPS port is configured, which is why every local run and the whole integration suite are unaffected. Identity's `if (!IsDevelopment())` guard around its redirect is removed — under the AppHost that condition was always true anyway.

---

## ADR-034 — F-021 replaces the reflection guard that forbade a logger in `IdentityService`

**Date:** 2026-08-22 · **Status:** Accepted · **Feature:** F-021 (T04) · **Threats:** T-001 (F-001), T-105

**Context.** `Identity.Tests/Security/LoginLogSanitizationTest.cs` carried `IdentityService_ConstructorParameters_ContainNoILogger`, which asserted **by reflection** that `IdentityService` had no logger parameter at all — a structural proxy for "no credential material in logs", written when nothing in Identity logged anything. F-021 PRD requirement 17 requires credential mutations to be logged: the account-destroying refresh was silent *as well as* destructive, so an account lost that way left no trace of ever having existed.

**Decision.** Delete the reflection guard and replace it with the assertion it was standing in for: the logger exists, and no log line contains the email address, the password, the access token or the refresh token. Account identity is logged as `acct_` plus a 12-hex-character SHA-256 prefix.

**Rationale.** The proxy and the requirement are in direct conflict, and the proxy is the weaker statement. It also turns out the three sanitization tests beside it were **vacuous**: they iterated `GetMessages(Information)` on a logger factory wired to nothing, so they asserted over an empty list and could not have failed. They now run against real log output, with a `NotEmpty` guard so they cannot silently become vacuous again.

**Why a hash prefix rather than truncation.** `PiiRedactingProcessor` protects **spans, not logs** — nothing downstream would catch an address written here, and F-013's telemetry rollout is this project's own precedent for that (threat T-004: real customer emails exported in `url.path` the moment telemetry was switched on). Truncation is not redaction: `aud…@example.com` still identifies a person in a user base this size. The honest claim for the hash is "not an address", not "anonymous" — with a known address list any digest is reversible.

**Consequences.**
- One pre-existing test deleted. This is F-021's only such deviation, the same class of decision as F-016's ADR-025, and it needs the same maintainer acknowledgement.
- `IdentityService` takes `IOptions<LockoutOptions>` and `ILogger<IdentityService>`, both **optional with defaults**, so the twenty-odd unit tests that predate F-021 compile unchanged and the shipped defaults apply to any caller that configures nothing.
- Identity still writes no audit events. Adopting the EventStore here would put credential-shaped documents into the collection every other service writes to; logs are the record, which is stated in `data-model.md` §6 rather than left implicit.


---

## ADR-035 — Azure is not reviewed until every pending feature ships and the no-longer-needed tech debt is discharged

**Date:** 2026-08-22 · **Status:** Accepted · **Decided by:** maintainer (ogdevlabs) · **Feature:** F-021 (Ship gate)

**Context.** The cloud capability has been written, unit-tested (47 AppHost model tests, now 62) and **never executed** since F-013. Each of the three tagged releases has recorded a "deploy skipped" with the same three blockers, and each recorded it as a gap widening by repetition. Meanwhile the roadmap says plainly that six features' worth of the product does not work yet.

**Decision.** Azure is **not reviewed, provisioned or deployed** until **both**:

1. **Every pending feature is completed** — F-014, F-015, F-017, F-018, F-019, F-020, and F-022–F-024 if they are still on the roadmap at that point.
2. **The tech debt of "things no longer needed" is discharged** — the code, containers, Compose services, scripts and configuration that exist only because of earlier shapes of this project and that a deployment would otherwise carry into a cloud environment.

Until both hold, "deploy skipped" is the **expected** outcome of a ship and is not reported as a gap.

**Rationale.** Deploying now would provision infrastructure, cost and attack surface for a system that cannot serve its own use cases:

- **F-014** exists because `NotificationService`, `MessageService`, `NoteService`, `PaymentService`, `ReportingService` and `DeactivateProviderCommand` have no DI registration, no configured collection and no HTTP route — so F-006 through F-010 are marked `Shipped` on code nothing can call.
- **F-015** exists because the mobile client cannot reach the backend at all.
- **F-017** owns the container story, and three Dockerfiles currently publish `net10.0` output onto a `dotnet/runtime:8.0` base and **cannot run**. There is no deployable artifact to deploy.

A cloud environment would make all of that a running cost without making any of it work. The second condition exists because deployment is the point at which dead weight stops being untidy and starts being deployed: a Compose file with services nothing uses, a seed script that writes to databases no service reads, and three unrunnable Dockerfiles are cheap in a repository and expensive in an environment.

**What this explicitly does NOT defer.**

- **Rotating the `agenda_buddy` Atlas credential** (`agenda-buddy-41s`, P0). It is a blocker *for* deployment, but its justification does not depend on deployment: the credential is valid, publicly recoverable from git history, and grants write access to a live cluster with **no backups**. Deferring the deploy does not defer this.
- **F-017's dependency-audit and secret-scan gate.** CONSTITUTION §7 mandates it, it has been satisfied by hand for three consecutive features, and it is about the repository rather than about any environment.
- **Keeping the cloud path buildable.** `azure.yaml`, `.github/workflows/deploy.yml` and the `DeploymentTarget.Cloud` graph stay covered by tests, so the capability does not rot while it waits. F-021 added assertions there for exactly this reason: the cloud branch is where its two security controls are switched on.

**Consequences.**

- `/ship` stops treating a skipped deploy as a finding. The Operation phase's deploy step records "deferred per ADR-035" and moves on, which is a materially different record from "skipped, blockers unchanged".
- `agenda-buddy-dwe` (first cloud deployment) is **deferred** rather than open-and-blocked, so `bd ready` stops offering work nobody intends to start.
- **A re-evaluation trigger, not a date.** The condition is a state of the roadmap, so it is checked at each ship rather than scheduled. The first ship after the last pending feature closes should re-open this decision explicitly rather than inheriting it.
- **The risk accepted:** the first deployment will exercise a capability that has by then been unexecuted for even longer, against an Azure surface that may have moved. That is the cost of the trade, and it is smaller than the alternative — carrying a live cloud environment for a product whose own roadmap says six features do not work.

---

## ADR-036 — Six capabilities land on three existing services, placed by data ownership

**Date:** 2026-08-23 · **Status:** Accepted · **Feature:** F-014

**Context.** Five `Library` services and one command handler had implementations, unit tests, and **zero** non-test references outside their own definitions. Making them reachable needs a host for each.

**Decision.** No new service. Notes and payments go to **Booking** (both keyed by `appointment_identifier`, and appointments live there); messages and notifications to **Customer**, as two **new top-level route groups** (`/api/v1/messages`, `/api/v1/notifications`) rather than children of `/api/v1/customers`; reporting and deactivation to **Provider** (both computed from or mutating the provider document).

**Rationale.** A message is addressed to a **person** — a provider has an inbox for exactly the same reason a customer does — so a URL saying `customers` about a provider's inbox asserts something false that every client then has to work around. **A service is a deployment unit, not a URL prefix**, and Identity already hosts two unrelated groups (`/api/v1/auth` and a top-level `/device-token`), so this is precedent rather than novelty. The alternative was an eighth service — a process, a Dockerfile, a health check, an AppHost resource and a `WaitFor` edge — to serve `InsertAsync` and `FindAllAsync` over two small collections.

**Consequences.** Booking takes three of the six, which is why messaging went to Customer rather than piling a fourth family there. No cross-service reads were introduced. Four new repositories and four collection names; MongoDB creates each collection on first write, so there is no migration.

---

## ADR-037 — Appointment status becomes server-owned, and the transition rules become reachable

**Date:** 2026-08-23 · **Status:** Accepted · **Feature:** F-014 · **Threat:** T-203

**Context.** `AppointmentEntity.Book()` and `.Complete()` encode the transition rules and were **never called anywhere in production** — only in tests. What ran was `appointment.AppointmentStatus = appointmentEntity.AppointmentStatus` (`UpdateAppointmentCommandHandler.cs:51`), copying a public settable enum straight from the `PUT` body. **A caller could mark a brand-new appointment `Completed`** — a claim that work was delivered — or move a completed one back and erase it from the provider's count. `MobileApp` already drives status this way (`AppointmentDetailPage.xaml.cs:93`), so the design was live; only F-015's absence kept it harmless.

This became F-014's business rather than a separate feature because **`ReportingService` is meaningless without it**: its two headline numbers derive from `AppointmentStatus`, so wiring reporting while status stayed unenforced would have shipped a dashboard structurally guaranteed to report zero completed appointments.

**Decision.** The `PUT` **ignores** the status field and preserves the stored value. Status changes go through a dedicated route that applies the transition via **the entity's own methods**. Illegal transitions answer **409**. Completing is **provider-only**; either participant may book.

**Rationale.** Routing through `Book()`/`Complete()` rather than a transition table in the handler keeps the invariant with the data, and makes a state added to the enum without a method **unreachable by construction** — the opposite of today. Leaving the field writable *and* adding the route would have added a door rather than closed one. Ignoring rather than rejecting the field is the compatible half: a `400` on a field the only existing client always sends would break a caller that has no other route yet.

**Consequences.**
- **Breaking for any client that sets status.** Free now, expensive after F-015 — the same argument that made F-016's breaking changes cheap.
- **`Confirmed` and `Cancelled` stay unreachable.** `Confirmed` is only produced on a Calendar projection; `Cancelled` is never persisted because cancellation deletes. Adding them is a product question about what they mean.
- **It activated a latent bug, which is fixed in the same feature.** `CancelAppointmentCommandHandler` refused to cancel a **`Booked`** appointment — the state a customer actually needs to cancel. Invisible while nothing set `Booked`; shipping the two changes separately would have looked like the status fix broke cancellation.
- **Both stored copies are written** — the `appointments` document and the provider's embedded one — because `ReportingService` counts from the embedded list. They are not atomic together (separate documents, no replica set, no transaction); re-issuing the transition repairs a partial write, and that is recorded in the handler.

---

## ADR-038 — Payments are non-charging by default; Stripe only when a key is configured

**Date:** 2026-08-23 · **Status:** Accepted · **Feature:** F-014 · **Threat:** T-206

**Context.** `StripePaymentGateway(string apiKey)` took a raw string, no Stripe configuration section existed anywhere, and it assigned `StripeConfiguration.ApiKey` — a **process-global static** — inside request handling. There is no Stripe account, no key, and no deployment (ADR-035 defers cloud until every pending feature ships).

**Decision.** `RecordingPaymentGateway` is registered by default: it mints an intent id prefixed **`local_`**, reports success, and contacts nothing. `StripePaymentGateway` is registered only when `Payments:Stripe:ApiKey` is present, which must be an **Aspire secret parameter** and never `appsettings.json`. The API key is assigned **once at construction**.

**Rationale.** The two alternatives both fail. A gateway that throws leaves `PaymentService` unreachable — the exact condition F-014 exists to end — and makes AC-6 unwritable. A gateway that charges by default is unthinkable without an account. Recording locally is the only option that leaves the capability exercisable and the money untouched. Assigning the static once narrows a live credential's exposure from "written on every request" to "written at startup", which is as narrow as the Stripe SDK allows.

**Consequences.**
- **A `Succeeded` status is not proof of settlement**, and the signal is in the **stored data** rather than only a log: Stripe ids begin `pi_`, so `local_` is permanently identifiable. `api-contracts.md` §2 states it so a client cannot infer otherwise.
- **Residual risk, accepted (PRD R4):** payments could stay permanently fake — a deployment forgets the key and records payments that never happened. Mitigated as ADR-033 mitigated the same shape: a loud startup warning naming the key, outside a local run.
- **The amount stays unvalidated** (threat T-205(c)) and cannot be validated, because an appointment does not record which service it was booked for. Harmless while nothing charges; a real underpayment the moment a key is configured.

---

## ADR-039 — The provider report publishes no revenue figure

**Date:** 2026-08-23 · **Status:** Accepted · **Feature:** F-014

**Context.** `ReportingService` computed `EstimatedRevenue` as `completed.Count × sum(all active service fees)` — completed appointments multiplied by the **entire catalogue total**. A provider offering services at 50, 80 and 100 with two completed appointments was reported as having earned **460**.

**It cannot be corrected by changing the formula.** `AppointmentEntity` records no service, no fee and no amount, so the input needed to compute revenue **does not exist in the stored data**.

**Decision.** Remove `EstimatedRevenue`. Return `revenueAvailable: false` and `revenueUnavailableReason` instead.

**Rationale.** Fixing appointment status (ADR-037) makes this number non-zero and **still wrong**, which is worse than zero: 0 reads as "no data yet", 460 reads as a fact. Publishing a number this system knows to be wrong is precisely the defect class F-014 exists to end — something marked delivered that does not do what its name says. A `bool` rather than a nullable number, so a client cannot render `null` as `0`. A stated absence rather than a silent omission, so a missing field reads as a decision rather than a serialisation bug.

**Blast radius, swept before deciding.** `ProviderReport` and `EstimatedRevenue` appear **nowhere** outside `Library` and `Library.Tests`. Zero production consumers: free today, a client rewrite after F-015.

**Consequences.** One pre-existing test replaced (`GetProviderReportAsync_CalculatesEstimatedRevenue`) — F-014's only deleted test, needing the same acknowledgement F-016's ADR-025 and F-021's ADR-034 needed. The data-model change that would make revenue computable — an appointment referencing its service — is filed, not built: it touches F-015's contract and F-025's rules and needs a product answer about historical appointments that have no service to reference.

---

## ADR-040 — The gateway's single-instance posture is accepted as a local-dev-scoped risk (T-301)

**Date:** 2026-08-23 · **Status:** Accepted · **Feature:** F-015 · **Threat:** T-301

**Context.** F-015 introduces a gateway in front of all seven backend services so `MobileApp` has one
address to call. Before this feature, no client could reach any service, so no single component's failure
could take down "all of them" for the client — there was nothing reachable to lose. After this feature, if
the one gateway process is down, every backend service becomes unreachable to the mobile client even if all
seven are individually healthy: a new aggregated single point of failure.

**Decision.** Accept the risk for this feature's scope. A single Aspire-run gateway instance matches how
every other resource in this AppHost already runs — no service, MongoDB, or Kafka resource runs with more
than one instance today. This feature does not regress local development, and no real (multi-replica,
load-balanced) deployment exists yet to make this a production concern.

**Rationale.** Building redundancy (multiple gateway replicas, a load balancer in front of them) into a
local-development-only orchestration model would be speculative infrastructure for a deployment target that
doesn't exist — the same reasoning ADR-035 already applied to deferring cloud review generally. The
mitigation belongs with whichever feature first stands up a real deployment target, since that is also where
"single instance of anything" stops being an acceptable default across the whole graph, not just the
gateway.

**Consequences.** Re-evaluation trigger: the first real (non-Aspire) deployment. That work is F-017's scope
and remains gated by ADR-035's deferral (every pending feature complete, plus the no-longer-needed tech debt
discharged). Until then, "the gateway is down" and "the AppHost isn't running" are the same failure mode
from the mobile client's perspective, which is an acceptable local-development trade — the alternative is
building HA infrastructure nobody can deploy yet.

---

**Note (issue #55 bookkeeping):** Threat-derived security ACs added to the F-015 PRD post-Define, at the
Design threat-modeling gate (Step 10.5/14.5): T-302 (gateway route allowlist) and T-303 (forwarded-`Host`
header / transport-security interaction). Both are `[security]`-tagged, test-first, and materialized on
tasks F-015-T03 and F-015-T04 respectively.

---

## ADR-041 — Skipped the Nordstrom standards `--design` gate at Plan (Step 17.5) for F-015

**Date:** 2026-08-23 · **Status:** Accepted · **Feature:** F-015

**Context.** Plan Step 17.5 runs the Nordstrom Standards Readiness check in **enforcing** mode against the
approved PRD — a MUST-level finding would block Plan approval, the same tier as a Phantom Critical finding.
Skipping an enforcing gate is therefore treated like `/override`: it requires a one-line reason and is
recorded here as an ADR.

**Decision.** Skip, with reason: **the plugin's six source standards repos have failed to resolve under this
machine's `gh` auth for the tenth consecutive gate across this project** (an SSO/VPN condition, confirmed
unchanged from F-013 through F-015; see the project's reference memory
`nordstrom-standards-repos-unreachable`). This is not a judgment that the standards don't apply — it is that
the check has never once been able to run.

**Rationale.** Re-attempting an already-confirmed-broken network condition ten times running has stopped
being due diligence and started being ritual. The condition needs a structural fix (SSO/VPN access, or a
vendored `.nordstrom-standards/` cache) or an explicit retirement decision — both recommended repeatedly
since F-013, both still owned by **F-017**.

**Consequences.** F-015 proceeds to Plan approval without a design-level standards analysis. If F-017
resolves the access condition, a `--delta` run against F-015's shipped shape can retroactively confirm or
flag findings after the fact. This is now the **tenth** consecutive gate blocked by this condition — the
oldest unaddressed process finding in the project, unchanged since F-013.

---

## ADR-042 — Nordstrom Standards Readiness is retired for this project — not applicable, not merely unreachable

**Date:** 2026-08-23 · **Status:** Accepted

**Context.** Ten consecutive gates (F-013 ship through F-015 Plan), across six distinct gate call sites
(Define `--ideate` ×3, Plan/Review `--design`/enforcing ×7), have all skipped for the identical reason: the
`nordstrom-standards-readiness` plugin's six source standards repos do not resolve under this machine's `gh`
auth. Every prior entry treated this as a **transient access problem** — "give it a reachable source" —
and recommended a fix owned by F-017. That recommendation has never been actioned, and the underlying reason
it never will be is more fundamental than access: **Agenda Buddy is a personal `fererelabs` project, not a
Nordstrom enterprise engagement.** The six standards bodies this plugin assesses against (Engineering,
Security, Privacy, Data Science & Analytics, Data Governance, AI Tooling) are Nordstrom's internal
enterprise standards. They were never going to apply here regardless of whether the repos resolved.

**Decision.** Retire the Nordstrom Standards Readiness gate for this project, explicitly and durably — not
as another skip-with-notice, but as a standing decision that the gate does not apply. Recorded in
`CONSTITUTION.md` §9 so every future `/brainstorm`, `/build`, `/ship`, and `/hotfix` gate call site can see
the exemption without re-deriving it, and does not need to re-attempt a network probe that was never going
to be relevant even if it succeeded.

**Rationale.** The distinction matters: "unreachable" implies the gate is real and should eventually run;
"not applicable" says the gate is answering a question this project doesn't have. Continuing to log a tenth,
eleventh, twelfth skip-with-notice for an inapplicable enterprise-standards check is process theater — it
manufactures the appearance of a governance gap where none exists. `docs/issues/ISSUE-002` and this
project's actual security posture (ADR-030, ADR-033, F-016's PII closures, F-021's auth hardening) are
carried by this project's own CONSTITUTION §7 test gates and threat-modeling practice, which are real and
do run.

**Consequences.** No future gate call site should prompt for or attempt the Nordstrom standards check on
this repository. `F-017`'s backlog item "give the standards gate a reachable source or retire it explicitly"
is **closed by this ADR** — retirement was the answer, not a VPN fix. If this project is ever formally
brought under Nordstrom's enterprise standards program (a change of organizational context, not a technical
one), this ADR should be revisited and superseded.

---

## ADR-043 — Malicious transitive NuGet package risk during the new per-service publish step is accepted (T-003)

**Date:** 2026-08-25 · **Status:** Accepted · **Feature:** F-017 · **Threat:** T-003

**Context.** F-017 adds a CI job that runs `dotnet publish -t:PublishContainer` for each of the seven
remaining services. NuGet packages can execute arbitrary code via MSBuild targets/install scripts at
restore or build time; a compromised transitive dependency could exfiltrate the CI job's ambient token
during any of these seven new publish invocations.

**Decision.** Accept the risk. This is not new exposure — `build-and-test` already runs `dotnet
restore`/`dotnet build` on every pull request today, the same risk class this feature's seven new
invocations extend, not introduce.

**Rationale.** Fixing "arbitrary NuGet packages can execute code at build time" is a generic .NET
supply-chain concern that would require a solution-wide control (a package allowlist, `dotnet nuget
verify`) applicable to every project in the solution, not just the seven this feature touches — out of
scope for a CI/container-hardening feature. F-017's own dependency-audit job (Requirement 4) is itself a
partial mitigation for the broader concern.

**Consequences.** Re-evaluation trigger: if this repository begins accepting external contributions, or if
a solution-wide NuGet-supply-chain control is scoped as its own feature. No task or acceptance criterion
follows from this decision.

---

## ADR-044 — Resource exhaustion from the 7-way parallel image-build matrix is accepted (T-004)

**Date:** 2026-08-25 · **Status:** Accepted · **Feature:** F-017 · **Threat:** T-004

**Context.** F-017's new `docker-build-and-scan` job runs a 7-way parallel matrix (`dotnet publish
-t:PublishContainer` + Trivy scan per service) on every pull request touching a service directory. Combined
with Trivy's uncached CVE-database download, this could slow CI turnaround on a busy day.

**Decision.** Accept the risk. The job already carries `timeout-minutes: 10` per matrix entry (Requirement
10), bounding the worst case.

**Rationale.** This is a reliability/cost concern, not a security threat with attacker benefit — this
repository has no external contributors triggering CI at volume today. The `timeout-minutes` bound is
sufficient for the actual usage pattern.

**Consequences.** Re-evaluation trigger: if CI cost or turnaround time becomes a real problem in practice,
revisit Trivy CVE-database caching (already recorded as a Known Risk in the F-017 PRD, not actioned here).

---

## ADR-045 — Dependabot bypassing PR-review discipline is accepted, because it cannot (T-005)

**Date:** 2026-08-25 · **Status:** Accepted · **Feature:** F-017 · **Threat:** T-005

**Context.** F-017 adds `.github/dependabot.yml`. If Dependabot PRs were auto-merged, a compromised upstream
package version could land on `main` without human review.

**Decision.** Accept the risk. This feature adds no auto-merge capability — Dependabot PRs go through the
identical PR-review process as any other PR.

**Rationale.** `CONSTITUTION.md` §6 already requires PR + human approval on `main`, unchanged by this
feature. There is nothing new to mitigate: the existing control already covers the new PR source.

**Consequences.** Re-evaluation trigger: if a future feature proposes Dependabot auto-merge, that feature's
own threat model must re-examine this decision — it does not carry forward automatically.

---

## ADR-046 — A workflow-file change weakening F-017's own new gates is accepted, because branch protection already covers it (T-006)

**Date:** 2026-08-25 · **Status:** Accepted · **Feature:** F-017 · **Threat:** T-006

**Context.** Anyone with PR access could propose a change to `dotnet.yml` that silently weakens or removes
the `security-scan` or `docker-build-and-scan` jobs this feature adds (e.g., changing a `fail` condition to
a `warn`).

**Decision.** Accept the risk. This is a pre-existing, generic CI-governance risk that applies equally to
every workflow change ever made in this repository — not specific to what F-017 adds.

**Rationale.** The same branch-protection + human-review requirement that governs every other change to
this repository already governs changes to `dotnet.yml` itself. Requiring a *different* control for this
one file would be inconsistent with how every other file in the repository is protected.

**Consequences.** None specific to this feature. Re-evaluation trigger: none identified — this is the same
residual risk every CI pipeline with human-reviewed changes carries.

---

**Note (issue #55 bookkeeping):** Threat-derived security ACs added to the F-017 PRD post-Define, at the
Design threat-modeling gate (Step 10.5/14.5): T-001 (unpinned third-party Actions) and T-002 (secret-value
CI-log leakage). Both are `[security]`-tagged, test-first, added as PRD ACs 14-15, and materialized on tasks
F-017-T09 and F-017-T05 respectively via `tasks.cjs ac add`.

---

## ADR-047 — F-017 Party Review: 2 Important findings and 11 Advisory/YAGNI items accepted as-is

**Date:** 2026-08-26 · **Status:** Accepted · **Feature:** F-017 · **Source:** Review gate (Step 13), batched acceptance

**Context.** The F-017 Party Review (`docs/pdlc/reviews/REVIEW_container-and-cd-hardening_2026-08-26.md`)
raised 1 Critical, 4 Important, 10 Advisory findings, and 1 YAGNI over-engineering note. The user chose to
fix the Critical (C1, missing regression tests — resolved, commit `7cefae1`) and two of the four Important
findings (I1, security-scan's path-filter coverage gap — resolved, commit `521a7ce`; I3, `CLAUDE.md` drift —
resolved, commit `ebabba7`), and to accept the remainder as-is rather than fix them now. This entry batches
that acceptance per Build Step 14's instruction to batch multiple deferred/accepted findings into one
Decision Review rather than one per finding. Given all four reviewing agents (Neo, Echo, Phantom, Jarvis) had
just independently assessed this exact diff moments earlier in the Party Review itself, this was recorded
directly rather than re-convening a full 9-agent Decision Review Party for findings already fully diagnosed
by the agents who own the affected artifacts.

**Decision.** Accept, as-is, without further Construction-phase action:

- **I2** — AC10 (the `docker` path filter's live-PR verification) has not happened; `feat/F-017-...` has no
  open PR yet. Accepted because it genuinely cannot be verified from a local branch — it becomes actionable
  the moment a PR opens, which is `/ship`'s job, not Construction's.
- **I4** — one flaky run (77/87) out of 5 full-suite runs in `AgendaBuddy.AppHost.Tests`, all other runs
  clean at the claimed count. Accepted as a suspected resource-contention flake, not a logic bug; filed as a
  follow-up to investigate rather than blocking this feature.
- **A2–A10** (Advisory) — stale comment already fixed alongside C1; tech-debt bead recommendation for the
  Gateway path-filter gap; duplicate `RepoRoot()` test helper (YAGNI `shrink:`); AC3's non-digest-pinned-image
  edge case; `PublishContainerTest`'s structural-proxy nature; AC10/AC12's PRD-anticipated deferral; merge-commit
  subject format; and the confirmations (T-001/T-002/T-003–006 accept-rationales, `api-contracts.md`/
  `data-model.md` "no changes", `ARCHITECTURE.md`'s correction quality). None change behavior or carry a
  security/architecture risk beyond what's already logged; all are documentation-quality or nice-to-have
  polish.

**Rationale.** The Critical and the two most consequential Important findings (a real security-coverage gap
and stale onboarding documentation) are fixed. The remainder are either not fixable pre-merge by their
nature (I2), a suspected flake needing observation rather than a code change (I4), or genuinely low-stakes
polish (Advisories, YAGNI). Fixing everything now would trade a fast, correctly-scoped Construction phase for
diminishing-returns cleanup on a feature that has already found and fixed four real pre-existing defects
(Profession/Dockerfile, the broken trivy-action tag, the NU1903 NoWarn misconception, the gitleaks
secretGroup redaction bug) plus two real gaps caught by this very review (C1, I1).

**Consequences.** Follow-up beads recommended, not filed as blocking: (1) investigate the `AgendaBuddy.AppHost.Tests`
flake (I4) before it becomes a source of false-red PRs; (2) track the Gateway path-filter omission (A2) —
Gateway currently has zero CI coverage of any kind, a pre-existing F-015 gap this review surfaced but did not
introduce; (3) verify AC10 live the moment a real PR opens against `feat/F-017-container-and-cd-hardening`
(I2) — this is `/ship`'s responsibility, not deferred indefinitely. Re-evaluation trigger: none of these
block a future feature by default: only I4 recurring at a materially higher rate, or a real secret leak
recurring in a path this feature's `security-scan` job now DOES cover (post-I1-fix), would warrant revisiting.
