# MOM — Progressive Thinking: api-refactor-foundations (F-018)

**Date:** 2026-08-18 · **Called by:** Atlas (Product Manager)
**Participants:** Neo, Echo, Phantom, Bolt, Friday, Muse, Pulse, Jarvis — 8 agents, Atlas facilitating
**Purpose:** Progressive-thinking refinement of F-018 discovery before Define
**Meeting mode:** run inline by the lead (subagents not spawned — not requested for this session)

---

## Round 1 — Concrete (what do we know for certain?)

Facts only. Catalog- or source-anchored claims count as facts; everything else was held for Round 2.

**Bolt (backend).**
- 7 services, each with its own `Program.cs` containing DI wiring, a ~40-line exception handler, and every endpoint inline. `Booking/Program.cs` is ~190 lines.
- Write paths go through per-service `RequestCollection` classes that **hand-construct** handlers: `new BookingAppointmentCommandHandler(...).Handle(...)` (`Booking/Requests/RequestCollection.cs`). Six such classes exist (Booking, Calendar, Customer, Profession, Provider, Services).
- `EventStore` takes an injected client: `public EventStore(IMongoClient client, IConfiguration configuration)` (`EventAndCommands/Persitency/EventStore.cs:19`).
- `new CancellationToken()` is passed to every handler — the request token is discarded.
- `kafkaClient as KafkaClient` — an unchecked downcast from interface to concrete type.

**Neo (architecture).**
- CONSTITUTION §3 mandates three invariants that must survive: CQRS via MediatR, an EventStore audit event per command ("do not remove this pattern"), and cache-aside for read-heavy queries ("do not bypass").
- MediatR is registered in every service and injected into every endpoint, but **`mediator.Send` is never called** (`docs/pdlc/context/15-cqrs-and-messaging.md`). There are also zero `INotificationHandler` implementations, so every `Publish` is a no-op.
- All 7 services call `builder.AddServiceDefaults()` exactly once, verified line-by-line.

**Echo (test).**
- **379 tests**: 305 across 12 backend projects + 74 in `MobileApp.Tests` (67 passing, 7 skipped). Verified by running both suites today.
- **Zero integration tests.** `AppHostWiringTest` asserts the Aspire app *model*, not a real run.
- **No test asserts that an EventStore audit event was written.** The §3 invariant is entirely unguarded.

**Phantom (security).**
- `AppointmentEntity` — the MongoDB persistence entity — is both request body and response body on Booking's three write endpoints.
- `AddAgendaBuddyAuthentication()` **throws `ApplicationException` when `JWT_PUBLIC_KEY` is unset** (`Library.ServerAuth/AuthenticationExtensions.cs:18-22`). Verified by reading the source.
- Token validation is strict: RS256 only, issuer `agenda-buddy-identity`, `ValidateLifetime = true`, `ClockSkew = TimeSpan.Zero`.
- The Atlas credential remains in git history and remains valid (9 commits).

**Pulse (devops).**
- CI has a path-filtered pipeline including three mobile jobs: `build-android` (installs `maui-android`), `build-ios` (**`macos-latest`**, installs `maui-ios`), `build-mobile-tests`.
- `ubuntu-latest` runners ship Docker, so Testcontainers needs no extra runner setup.
- Local dev is Rancher Desktop: **2 CPUs / 4.1 GB, already running a k8s cluster**, and `docker` is **not on PATH** (it lives at `~/.rd/bin`).

**Jarvis (docs).**
- **No committed OpenAPI spec.** Swagger is generated at runtime and only in `Development`.

**Friday (frontend) / Muse (UX).** No UI surface in F-018. MobileApp's inability to reach the backend is real but belongs to F-015. Muse abstains — Design-Laws Audit will correctly triage to Skip.

---

## Round 2 — Inferential

**Neo (inference).** `WebApplicationFactory<Program>` needs the entry-point type to be accessible. The services use top-level statements, so `Program` is internal, and **no service `.csproj` has `InternalsVisibleTo`** — only `MobileApp`, `AgendaBuddy.AppHost` and `Kafka` do. Inference: each service under integration test needs `<InternalsVisibleTo Include="AgendaBuddy.IntegrationTests" />`. **Verified against `Booking/Booking.csproj` — confirmed absent.**

**Phantom (inference).** Because auth throws without `JWT_PUBLIC_KEY`, every integration test host must supply a key *before* the factory builds. And because tokens must be RS256/`agenda-buddy-identity`/unexpired, tests need the **private** key too in order to mint valid tokens. Inference: the harness needs an RSA keypair generated per test session.

**Pulse (inference).** Testcontainers discovers the Docker endpoint via `DOCKER_HOST` or the default socket path. Rancher's socket is `~/.rd/docker.sock`, not the Docker Desktop default. Inference: **Testcontainers will probably not work locally out of the box** and needs `DOCKER_HOST` (or a `.testcontainers.properties`) configured — CI will work while local dev fails, which is the worse failure order.

**Echo (inference, later withdrawn).** Assumed OTLP export would need suppressing in tests. **Withdrawn in Round 3 — see below.**

**Bolt (inference).** `MongoConnectionResolver` reads `ConnectionStrings:mongodb` first, so injecting the Testcontainer connection string is a single configuration override per test host. No production code change needed for that.

---

## Round 3 — Consequential

**Correction to Round 2 (Echo's inference was wrong).** OTLP export is already conditional: `AgendaBuddy.ServiceDefaults/Extensions.cs:115-117` only calls `UseOtlpExporter()` when `OTEL_EXPORTER_OTLP_ENDPOINT` is non-empty. In a test host it is unset, so **telemetry export is naturally inert and needs no suppression.** One fewer prerequisite than assumed.

**Consequences that follow:**

1. **The harness has exactly three hard prerequisites**, not four: (a) `InternalsVisibleTo` per service, (b) an RSA keypair + `JWT_PUBLIC_KEY` set before host build, (c) the Mongo connection string injected via configuration. Until all three are solved, `WebApplicationFactory` throws and **no integration test runs at all**. This is the critical path.
2. **A shared fixture is unavoidable** even though the user chose container-per-test. Keypair generation and configuration wiring are per-session concerns; only the *container* is per-test. Design must separate those two lifetimes cleanly.
3. **Kafka is the wall-clock threat, not Mongo.** Mongo starts in ~1–3 s; Kafka typically 5–10 s. Only the provider-registration path touches Kafka. Consequence: keep Kafka to the smallest possible number of tests, or the 10-minute budget is at risk from one path.
4. **Adding the §7 gate changes the Definition of Done** (§5 already lists "all integration tests pass" — currently unsatisfiable). §5 becomes satisfiable for the first time.
5. **The `Persistence` rename is a 7-line change plus a directory move**, with no config coupling. It can safely be its own commit.

---

## Round 4 — Speculative (what might we be missing?)

**Phantom.** ⚠️ *"Integration tests that mint JWTs need a private key. If anyone commits a fixed test keypair, we have created a new secret-shaped artifact in the repo three weeks after a credential incident whose remediation is still open."* Even a test-only key trains the wrong habit and will trip secret scanners once F-017 adds one. The keypair must be generated in memory per test session and never written to disk — the same approach F-013 already adopted for the CI startup guard.

**Echo.** *"Container-per-test with xUnit parallelism on a 2-CPU VM will produce timeout flakes that look identical to real failures. The failure mode isn't slowness — it's that people learn to re-run red builds."* Recommends measuring before scaling the test count.

**Neo.** *"F-019 introduces `DataResponse<T>`, which changes every response envelope. Integration tests written against today's raw-entity response bodies will all break in F-019 — and we won't be able to tell 'F-019 broke behaviour' from 'F-019 changed the envelope as designed'."*

**Pulse.** *"Testcontainers pulls images on first run. The NuGet cache doesn't cover Docker images, so a cold CI runner pays the pull each time unless we pin and cache deliberately."*

**Jarvis.** *"There is no committed OpenAPI spec, so 'route contract' has no canonical baseline. We'd be asserting routes against our own beliefs about them."*

**Bolt.** *"`Calendar` and `Identity` have no write endpoints, so tiers 2 and 3 don't apply — the per-service matrix isn't uniform."*

---

## Round 5 — Conflicting

**Conflict A — container lifetime. Echo vs the user's Q6 decision.**
Echo argues per-class container reuse; the user explicitly chose container-per-test after being shown the 2-CPU/4.1 GB constraint.
**Resolved:** the user's decision stands. It is not overridden, and the 10-minute CI budget from Q11 is the objective trigger to revisit. Echo's concern is recorded as a risk rather than silently dropped.

**Conflict B — Neo vs Echo: what should integration tests assert, given F-019 will change response envelopes?**
Neo: tests asserting exact response bodies are write-once and will be rewritten in F-019.
Echo: tests that assert nothing about the response are worthless.
**Resolved in Neo's favour, with Echo's floor:** assert **behaviour that must survive the refactor** — HTTP status code, the persisted database state, and whether the audit event fired — rather than the exact JSON envelope. Envelope assertions are added *in F-019*, alongside the change that introduces the envelope. This makes the F-018 tests a genuine regression net for F-019 instead of a snapshot of the old shape.

**Conflict C — Phantom vs Pulse: JWT keypair generation cost.**
Phantom: generate per run, never persist. Pulse: RSA generation on every test adds up.
**Resolved:** generate **once per test session** in memory (not per test, not per container), injected into each host. Satisfies Phantom's requirement at negligible cost.

**Escalated to the user (team could not decide — it is a scope/process call, not a technical one):** whether F-018 should commit an OpenAPI spec as the canonical route-contract baseline. See the Escalation section.

---

## Round 6 — Strategic (ranked design priorities)

1. **Solve the three prerequisites first — they are the critical path.** `InternalsVisibleTo` per service, per-session RSA keypair with `JWT_PUBLIC_KEY` set before host build, and Mongo connection-string injection. Nothing runs until these work. A spike proving one service boots under `WebApplicationFactory` should precede all other work.
2. **Verify Testcontainers against Rancher Desktop early.** Local-dev breakage while CI passes is the worse failure order, and `docker` not being on PATH is a known trap in this project.
3. **Tier 3 (audit fired) with the mutation check is the single highest-value deliverable.** It guards the one §3 invariant that F-019/F-020 could silently break, and the mutation check is the discipline episode 001 explicitly recommended.
4. **Assert behaviour, not envelopes** (Conflict B), so the net survives F-019.
5. **Generate JWT keys per session, in memory, never on disk** (Phantom's Round 4 point).
6. **Measure the CI job against the 10-minute budget before scaling test count**, and confine Kafka to as few tests as possible.

**Safely deferred / simplified:**
- Tiers 2 and 3 for `Calendar` and `Identity` — they have no write endpoints (Bolt). Route contract only.
- Docker image pinning/caching — do it only if the job approaches the 10-minute budget.
- Extracting shared abstractions into a common project — that is an F-020 question, not F-018.

---

## Escalation

**One question the team could not resolve internally.**

**Question:** Should F-018 generate and commit an OpenAPI spec as the canonical route-contract baseline?

- **Jarvis's view:** without one, "route contract" tests assert routes against our own assumptions. A committed spec makes the contract reviewable in a diff, and would have made F-015's mobile/backend mismatch obvious years earlier — every mobile route omits the `api/v1/` prefix and no artifact ever made that visible.
- **Neo's view:** F-019 and F-020 will change response shapes deliberately. A spec committed now would be regenerated twice, and a spec that churns is one people stop reading. Better to commit it at the end of F-020 when the shape is final.

**User's decision (2026-08-18):** **"Commit it now, accept the churn"** — Jarvis's position.

F-018 generates and commits an OpenAPI spec per service. The two regenerations in F-019 and F-020 are accepted as a known cost. Consequences to carry into Define:
- The spec becomes an **F-018 deliverable and an acceptance criterion**, not a side effect.
- Swagger currently only runs in `Development`, so spec generation needs a deterministic, environment-independent path — a build/CI step or a test that writes the spec, not "start the app and download it".
- Because the spec will change in F-019 and F-020, those features must treat **an unreviewed spec diff as a defect**. A regenerated spec that nobody reads defeats the purpose Jarvis argued for, which was making contract drift visible in review.
- Immediate secondary value: diffing the committed spec against `MobileApp`'s expected routes gives **F-015 a concrete, reviewable artifact** for the mismatch it exists to fix.

---

## Attendance note

Muse abstained (no UI surface). Friday contributed Round 1 facts only, then abstained — the mobile contract is F-015's scope, not F-018's.
