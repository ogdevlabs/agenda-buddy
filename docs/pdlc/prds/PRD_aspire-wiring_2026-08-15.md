# PRD — Aspire Wiring

<!-- pdlc-template-version: 2.4.0 -->

**Feature ID:** F-013
**Feature:** `aspire-wiring`
**Author:** Atlas (Product Manager)
**Date:** 2026-08-15
**Status:** Approved
**Approved by:** oscargarcia@ogdevlabs.onmicrosoft.com
**Approved date:** 2026-08-15
**Brainstorm log:** [brainstorm_aspire-wiring_2026-08-15.md](../brainstorm/brainstorm_aspire-wiring_2026-08-15.md)
**Design docs:** [ARCHITECTURE.md](../design/aspire-wiring/ARCHITECTURE.md) · [threat-model.md](../design/aspire-wiring/threat-model.md)
**Plan:** [plans/PLAN_aspire-wiring_2026-08-15.md](plans/PLAN_aspire-wiring_2026-08-15.md)

> **Approval note.** Approved by the user's instruction *"no more questions, approval for all, continue, end with commit and Pr"* rather than by a reviewed read of this document. Five assumptions (A-1…A-5) and three open questions (OQ-1…OQ-3) below were decided by the agent team, not the user. **OQ-1 requires operational action outside this feature.**

---

## 1. Problem Statement

Agenda Buddy cannot be started.

- `docker compose up` starts **1 of 7** API services. `provider` and `services-api` are commented out (`docker-compose.yml:42-56`); Booking, Calendar, Customer, and Profession were never added. Three of the ten declared Compose services (`events`, `kafka-library`, `common-library`) are **class libraries with no `ENTRYPOINT`**, and their Dockerfiles publish `net10.0` output onto a `dotnet/runtime:8.0` base image.
- Running directly needs **seven terminals** plus `JWT_PUBLIC_KEY` exported for all seven services (`Library.ServerAuth/AuthenticationExtensions.cs:18-21` throws without it) and `JWT_PRIVATE_KEY` for Identity. Each service binds a hardcoded `localhost:603x`.
- All six domain services **throw at startup outside `Development`**. Each reads a root-level `MongoDB` configuration section (`Booking/Extensions/ServiceCollectionExtension.cs:10`, `Booking/Configuration/MongoDbConfiguration.cs:7`) that exists only in `appsettings.Development.json`. In any other environment the connection string resolves to `null` and `new MongoClient(null!)` throws.
- The solution has **no health checks, no OpenTelemetry, no metrics, no tracing, and no resilience policies** — verified absent across every `.cs` and `.csproj`.

Cost: there is no reliable local run, no deployable artifact, and no way to diagnose either.

## 2. Target User

The **developer/operator** — currently the sole maintainer (`ogdevlabs`) — in two contexts: starting the stack to work on a feature, and deploying it.

**This feature changes nothing for the end-user personas in `INTENT.md`.** Its value is second-order and must not be overstated:

1. It removes the blocker in front of four roadmap features marked **Shipped** whose functionality is unreachable — F-006 (notifications), F-007 (messaging), F-008 (notes), F-009 (reporting), and F-010 (payments) each have a domain service and unit tests but **no DI registration and no HTTP route**.
2. It makes injected connection strings the default path, which retires the live MongoDB Atlas credential committed in 14 tracked files.

> **Honesty note (R-7):** this feature *removes a blocker*; it does not deliver the features behind it. The roadmap must not be credited with progress it has not made.

## 3. Desired Behaviour Change

| Actor | Today | After |
|---|---|---|
| Developer starting the stack | 7 terminals + 2 exported keys, or Compose that starts 1 of 7 | `dotnet run --project AgendaBuddy.AppHost`, zero env vars |
| Developer diagnosing a failure | No health endpoint, no trace, no metric | `/health` per service; traces, metrics, and logs in the Aspire dashboard |
| Operator deploying outside Development | Startup throws | Starts under any `ASPNETCORE_ENVIRONMENT` given a connection string |
| Anyone reading the repo | A live database credential in 14 files | Zero committed secrets |

## 4. Scope

Tier B of three options evaluated in the brainstorm log (Round 2): **Aspire, plus the configuration and DI wiring Aspire structurally displaces — and nothing else.**

### 4.1 In scope

1. **`AgendaBuddy.ServiceDefaults`** — shared library exposing `AddServiceDefaults()` (OpenTelemetry traces/metrics/logs with OTLP export, default health checks, `HttpClient` standard resilience, service discovery) and `MapDefaultEndpoints()` (`/health` readiness, `/alive` liveness). Referenced by all 7 API services.
2. **`AgendaBuddy.AppHost`** — orchestrator declaring a MongoDB resource (persistent volume), a Kafka resource (no volume — see §8 E-10), the 7 API service projects, and the two JWT keys as Aspire parameters.
3. **Connection-string resolution refactor** — the 7 `MongoDbConfiguration` classes, the 7 `ServiceCollectionExtension` classes, and `EventAndCommands/Persitency/EventStore.cs` move to `GetConnectionString("mongodb")` with the legacy sections retained as ordered fallbacks.
4. **Shared `IMongoClient`** — one client per process, replacing eager per-service construction and, critically, `EventStore`'s **per-request-scope** `new MongoClient(...)`.
5. **Health checks** — `/health` and `/alive` on all 7 services, with an explicit MongoDB readiness probe (a health check that does not probe Mongo is worse than none — R-6).
6. **`KafkaClient.BootstrapServers` from configuration** — closes the outstanding `CONSTITUTION.md` §9 item ("must be moved to configuration before any non-local deployment").
7. **Credential removal** — the Atlas connection string deleted from all 14 tracked files; the config *keys* remain as empty fallback slots so single-service runs still resolve (R-2, E-1).
8. **CI update** — extend the `dorny/paths-filter` to cover `AgendaBuddy.AppHost/**`, `AgendaBuddy.ServiceDefaults/**`, `global.json`, `Dockerfile*`, `docker-compose*.yml`, and `.github/**`; add an AppHost build step; assert `dotnet build /p:MobileWorkloads=false` still succeeds.
9. **Per-service connection-resolution tests** — the code being refactored currently has **zero test coverage** (R-3).
10. **ADR-013** in `DECISIONS.md`, satisfying `CONSTITUTION.md` §9's "new packages require discussion" constraint.

### 4.2 Explicit non-goals

- **No change** to CQRS, MediatR, `RequestCollection`, `EventsHelper`, or the EventStore *pattern*. Only *how its Mongo client is obtained* changes. (`CONSTITUTION.md` §3: "do not remove this pattern".)
- **No new HTTP routes**; **no registration** of the six unwired domain services → **F-014**.
- **No API gateway**, no `UsePathBase`, **no mobile-client change** → **F-015**.
- **No Kafka producer or consumer.** The topic-creation-only integration stays.
- **No auth or authorization change**; the six anonymous PII endpoints are **not** fixed here → **F-016**. Rejected in Progressive Thinking Round 5 as "while we're in here" scope creep.
- **No Dockerfile fixes** (including the three `runtime:8.0` images) and **no Compose deletion** → **F-017**. Compose is retained so the change is reversible (R-4, E-12).
- **No Aspire deployment/publish manifest** (`azd`, Kubernetes, container registry).
- **No `MongoDB.Driver` upgrade** unless R-1 forces it, in which case the escape hatch in §7 applies instead.

### 4.3 Decomposed follow-ups (filed, not built)

| ID | Feature | Why separate |
|---|---|---|
| F-014 | `wire-unreached-services` | Register + route the six unreachable shipped features |
| F-015 | `api-gateway-and-mobile-contract` | Gateway + fix the `api/v1/` prefix and verb mismatch so the mobile client stops using `SeedDataProvider` |
| F-016 | `secure-public-endpoints` | Authenticate, ownership-guard, and paginate the six anonymous PII endpoints; add the missing Calendar `OwnershipGuard` |
| F-017 | `container-and-cd-hardening` | Fix the three `runtime:8.0` Dockerfiles; add image build/scan/push and the mandatory §7 security scan |

## 5. User Stories and Acceptance Criteria

### US-1 — One command starts the whole stack

> As the maintainer, I want a single command to start every service and its dependencies, so that I can begin work without manual setup.

| ID | Criterion | Verification |
|---|---|---|
| AC-1.1 | `dotnet run --project AgendaBuddy.AppHost` in a **clean shell with no exported environment variables** starts successfully | Manual (E-7) |
| AC-1.2 | The Aspire dashboard lists **9 resources**: Booking, Calendar, Customer, Provider, Services, Profession, Identity, mongodb, kafka | Manual |
| AC-1.3 | All 7 services report `Healthy` | Manual |
| AC-1.4 | No service binds a hardcoded `localhost:603x` port when run under the AppHost | Code review + dashboard |
| AC-1.5 | `AgendaBuddy.AppHost` has **no project reference** to `MobileApp`, directly or transitively | Automated: `dotnet list AgendaBuddy.AppHost reference` |

### US-2 — No secrets in the repository

> As the maintainer, I want no credential in source, so that cloning the repo does not disclose database access.

| ID | Criterion | Verification |
|---|---|---|
| AC-2.1 | `git grep 'zufa26pHneUCGol9'` returns **zero matches** in tracked files | Automated |
| AC-2.2 | No `mongodb+srv://` URI containing a password appears in any tracked file | Automated |
| AC-2.3 | Every service still resolves a connection string when run under the AppHost | AC-1.1 |
| AC-2.4 | Running a single service **outside** the AppHost with `ConnectionStrings__mongodb` set succeeds | Manual (E-1) |
| AC-2.5 | Running a single service with **no** connection string available fails with a message naming the missing configuration key — never a `null`-argument throw | Automated test (E-2) |

> AC-2.1–2.2 remove the secret from the **working tree**. They do **not** remediate git history — see OQ-1.

### US-3 — Every service is observable

> As the maintainer, I want health, traces, and metrics, so that I can tell whether a service is working and why it is not.

| ID | Criterion | Verification |
|---|---|---|
| AC-3.1 | Each of the 7 services exposes `/health` (readiness) and `/alive` (liveness) | Automated per-service test or manual curl |
| AC-3.2 | `/health` returns unhealthy when MongoDB is unreachable | Manual: stop the mongo resource, re-probe |
| AC-3.3 | `/alive` remains healthy when MongoDB is unreachable (liveness ≠ readiness) | Manual |
| AC-3.4 | Traces, metrics, and structured logs for all 7 services appear in the Aspire dashboard | Manual |
| AC-3.5 | `AddServiceDefaults()` is called in all 7 `Program.cs` files | Code review |

### US-4 — The stack starts outside Development

> As the operator, I want services to start in any environment, so that deployment is possible at all.

| ID | Criterion | Verification |
|---|---|---|
| AC-4.1 | `ASPNETCORE_ENVIRONMENT=Staging` with `ConnectionStrings__mongodb` set reaches "Now listening on…" for **each of the 7 services** | Manual, all seven |
| AC-4.2 | No code path calls `new MongoClient` with a possibly-null argument | Code review |
| AC-4.3 | `EventStore` obtains an injected `IMongoClient` rather than constructing one | Code review |

### US-5 — Nothing existing breaks

> As the maintainer, I want certainty that plumbing changes did not alter behaviour.

| ID | Criterion | Verification |
|---|---|---|
| AC-5.1 | All **256** existing tests pass | `dotnet test /p:MobileWorkloads=false` |
| AC-5.2 | **No existing test is modified or deleted.** A test that requires a change is a regression signal, escalated rather than edited | `git diff --stat` on `*.Tests/` |
| AC-5.3 | `git diff` touches **no file** under `EventAndCommands/Commands/`, `EventAndCommands/Queries/`, or `Library/Services/` | `git diff --name-only` |
| AC-5.4 | `dotnet build /p:MobileWorkloads=false` succeeds; MAUI workloads are not required to build the solution | CI |
| AC-5.5 | `KafkaClient` reads `BootstrapServers` from configuration, defaulting to `localhost:9092` when unset | Automated test |

## 6. Success Metrics

| Metric | Today | Target | Timeframe |
|---|---|---|---|
| Commands to start the full stack | ∞ (impossible) | **1** | At merge |
| API services startable via one command | 1 of 7 | **7 of 7** | At merge |
| Manual environment variables required for local run | 2 | **0** | At merge |
| Credentials in tracked files | 14 | **0** | At merge |
| Services with a health endpoint | 0 of 7 | **7 of 7** | At merge |
| Services that start outside `Development` | 1 of 7 (Identity) | **7 of 7** | At merge |
| `MongoClient` instances per HTTP request (EventStore path) | 1 per scope | **0** (shared singleton) | At merge |
| Existing tests passing | 256 | **256**, none modified | At merge |

"Developer experience improves" was explicitly rejected as an acceptance criterion — it is not measurable and it is gameable.

## 7. Risks and Mitigations

| ID | Risk | Severity | Mitigation |
|---|---|---|---|
| R-1 | **Aspire's MongoDB integration may be incompatible with `MongoDB.Driver` 2.25.0**, which is pinned and is the reason `Directory.Build.props:18-28` carries three transitive CVE pins | **High** | **Resolved first, at task T-01, before any other code.** *Escape hatch:* if incompatible, skip the Aspire Mongo integration entirely and use plain `GetConnectionString("mongodb")` + a manual `AddSingleton<IMongoClient>`. Every acceptance criterion remains satisfiable. **Do not upgrade the driver in this feature** — that is a second migration. |
| R-3 | The refactored code has **zero test coverage** — no test exists for any `ServiceCollectionExtension`, and there is no integration test anywhere in the solution | **High** | Add per-service connection-resolution tests (T-04) covering the Aspire key, both legacy fallbacks, and the missing-value failure. Residual risk accepted: no end-to-end verification exists. |
| R-8 | **CI would not run at all.** The `paths-filter` ignores `global.json`, `Dockerfile`, `docker-compose*`, and `.github/**`; new top-level projects may also miss the `api` filter | **High** | Filter update is a first-class task (T-06), not cleanup. Verified by confirming jobs actually run on the feature PR. |
| R-2 | Credential removal is **not** remediation — the secret is in git history and stays valid until rotated | **High** | OQ-1. Outside this feature's control; flagged to the user explicitly. |
| R-5 | An AppHost reference could cascade MAUI TFMs and break `dotnet build` for anyone without MAUI workloads | Medium | AC-1.5 + AC-5.4. |
| R-6 | A health check that does not probe MongoDB reports healthy while the database is down | Medium | Explicit Mongo readiness probe; readiness and liveness kept separate (AC-3.2, AC-3.3). |
| R-4 | Aspire's Kafka resource does not provide `kafka-ui` or the Schema Registry that Compose does | Low | Compose retained, not deleted. Aspire is primary; Compose remains for Kafka tooling. |
| A-5/OQ-3 | The exact current Aspire version and its integration API surface were **not verified** — no network lookup was performed this session | Medium | T-01 resolves empirically before design assumptions are committed to code. |
| — | A shared `IMongoClient` changes connection-pool behaviour from N-per-request to one-per-process | Low | Strictly an improvement, but latency characteristics change. Record in the episode (Pulse, Progressive Thinking Round 3). |

## 8. Edge Cases

Carried from the brainstorm log; the behaviourally significant ones:

| ID | Case | Required behaviour |
|---|---|---|
| E-1 | Single service run outside the AppHost | Must work. Resolution order: `ConnectionStrings:mongodb` → `MongoDB:ConnectionString` → `LibrarySettings:MongoDB:ConnectionString` → fail with an actionable message |
| E-2 | No connection string anywhere | Fail fast naming the missing key. Never `new MongoClient(null!)` |
| E-3 | Docker not running | Aspire surfaces the container-runtime error; README documents it as the most likely first-run failure |
| E-4 | Ports 27017/9092 already bound | Use Aspire's dynamic host ports; do **not** pin. `scripts/seed/` assumes `mongo:27017` and will need the assigned port — documented, not fixed |
| E-5 | JWT keys absent | AppHost supplies both as parameters. ⚠️ `JWT_PRIVATE_KEY` remains **lazily** checked (`IdentityService.cs:189`), so Identity starts without it and fails on first login — pre-existing, out of scope |
| E-6 | MongoDB starts after a service | `WaitFor` on the resource; readiness health check covers the residual window |
| E-7 | AC-1.1 cannot be automated | No integration-test harness exists. Verification is manual and recorded in the episode. Accepted gap |
| E-8 | `scripts/seed/seed-mongo.sh` targets `ProviderDb`/`CustomerDb`, which **no service reads** | Already broken before this feature. Explicitly not fixed here; not to be attributed to this change |
| E-10 | Kafka moves from ZooKeeper to KRaft with a persisted volume | `CreateTopicIfNotExist` treats an existing topic as **failure** (`KafkaClient.cs:35-36`), returning HTTP 400 on re-registration. Pre-existing defect. **Kafka volume deliberately not persisted** to avoid amplifying it |
| E-12 | Rollback | `git revert` of one PR. No data migration; Compose and the legacy config path both retained |

## 9. Open Questions

| ID | Question | Owner | Blocking? |
|---|---|---|---|
| **OQ-1** | **The Atlas credential (`agenda_buddy` user) must be rotated at MongoDB Atlas and the cluster access log reviewed.** Deleting it from the working tree does not invalidate it — it remains in git history and remains valid. | **User — operational action** | Not blocking this feature; **blocking any public exposure** |
| OQ-2 | Are all four success criteria must-have, or is only "one command starts everything" required? (A-2) | User | No — PRD assumes all four |
| OQ-3 | Which Aspire version and integration packages apply to .NET 10, and is the Mongo integration compatible with driver 2.25.0? (A-5, R-1) | Build T-01 | Yes — resolved first |
| OQ-4 | Confirm A-1: is the motive unblocking the unreachable backlog, or deployment readiness? Scope would tilt toward publish/manifest work if the latter | User | No |
| OQ-5 | Confirm A-3 (Kafka reference only on Booking/Customer/Provider) and A-4 (Mongo container locally, Atlas remotely) | User | No |

## 10. Dependencies

- **.NET 10 SDK** — already pinned (`global.json:3`). No upgrade needed.
- **A container runtime** (Docker Desktop or Podman) — a new hard requirement for the local AppHost path. Previously optional for `dotnet run`.
- **Aspire workload/SDK** — version TBD at T-01.
- No dependency on any other roadmap feature. F-014 through F-017 depend on **this**, not the reverse.

## 11. Constitution Compliance

| `CONSTITUTION.md` clause | Status |
|---|---|
| §2 Business logic in the Library service layer only | ✅ No service-layer logic added |
| §2 Repository pattern only | ⚠️ **Improved, not fully satisfied.** `EventStore` still bypasses `IRepository<T>`; this feature only stops it constructing its own `MongoClient`. Full remediation is out of scope |
| §2 Async all the way | ✅ Unchanged. Note `Profession/Extensions/ServiceCollectionExtensions.cs:24`'s `.Wait()` is pre-existing and not fixed here |
| §3 Service isolation, shared Library, CQRS, EventStore, cache-aside, Kafka per-provider topics | ✅ All preserved — AC-5.3 enforces it |
| §4 Secrets never in source | ✅ **This feature brings the repo into compliance** for the working tree (OQ-1 for history) |
| §4 HTTPS enforced | ⚠️ Unchanged. No HTTPS endpoint is configured and `UseHttpsRedirection` sits after `UseAuthentication` — pre-existing, out of scope |
| §5 Definition of Done | ⚠️ "All integration tests pass" is unsatisfiable — the solution has none. "XML doc comments on all public service methods" applies to new ServiceDefaults members |
| §7 Test gates: unit tests required | ✅ AC-5.1 |
| §7 Security scan "always required, cannot be unchecked" | ❌ **Still unmet.** CI implements no secret or dependency scan. Deferred to F-017 — a known, deliberate constitution gap |
| §9 New packages require discussion | ✅ ADR-013 |
| §9 Kafka `BootstrapServers` to configuration before non-local deployment | ✅ AC-5.5 closes this |
| §9 Do not rename `Persitency` | ✅ Untouched |

## 12. Readiness Assessment

*Produced by the Step 18.6 PRD + Plan Readiness Party (Atlas co-chairing with Neo), run in Lite mode — non-interactive, advisory only.*

| Dimension | Rating | Evidence |
|---|---|---|
| **Completeness** | **Fair** | 5 user stories, 22 acceptance criteria, 11 edge cases, all traceable to catalog `file:line` findings. Downgraded from Strong because AC-1.1/1.2/1.3, AC-3.2/3.4, and AC-4.1 are **manually verified** — the solution has no integration-test harness (`11-testing.md`), so the headline criterion cannot be automated (E-7). |
| **Traceability** | **Strong** | Every criterion maps to a numbered problem statement item and a concrete anchor. Every non-goal maps to a filed follow-up (F-014…F-017). Every assumption is numbered (A-1…A-5) and surfaced as an open question. |
| **Durability** | **Fair** | Reversible by a single `git revert` (E-12); Compose and legacy config paths retained. Downgraded because R-1 could invalidate the chosen Mongo-integration approach mid-build — mitigated by resolving it at T-01 with a documented escape hatch — and because R-3 means the refactored code has no pre-existing safety net. |

**Party conclusion:** proceed. The plan front-loads the two highest risks (R-1 at T-01, R-8 at T-06) and the scope boundary held against one scope-creep attempt (Phantom's, Progressive Thinking Round 5). The dominant residual weakness is verification, not design: this feature's success criteria are largely proven by hand because the codebase has no integration-test capability. That is a pre-existing condition this feature does not fix and should not be blamed for — but it does mean **AC-1.1 through AC-4.1 rest on the implementer's manual attestation recorded in the episode.**
