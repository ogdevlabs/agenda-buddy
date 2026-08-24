# Agenda Buddy

Agenda Buddy is a scheduling and appointment management platform for independent service providers (fitness coaches, tutors, therapists, software instructors, etc.) who need to manage clients, services, and appointments in one place. It is built as event-driven microservices on .NET 10, orchestrated locally with .NET Aspire.

## Tech Stack

- **Language:** C# / .NET 10 (`net10.0`)
- **Framework:** ASP.NET Core Minimal APIs (one service per domain)
- **Orchestration:** .NET Aspire 13.4.6, **hosting-only** — `AgendaBuddy.AppHost` + `AgendaBuddy.ServiceDefaults`
- **Database:** MongoDB (MongoDB.Driver **pinned at 2.25.0** — see the Aspire caveat below)
- **Messaging:** Kafka (Confluent) + MediatR (CQRS)
- **Caching:** IDistributedCache (cache-aside pattern, 5-min TTL)
- **Observability:** OpenTelemetry traces/metrics/logs via ServiceDefaults, exported to the Aspire dashboard
- **Testing:** xUnit — **867 tests total**, in **three separate suites** that no single command runs: **468** across 12 backend test projects (`agenda-buddy-backend.slnf`; the slnf also carries `Gateway` itself, a 13th, non-test project), **234** in `AgendaBuddy.IntegrationTests` (real services — now including the Gateway — over HTTP against a MongoDB Testcontainer — needs a container runtime), and **165** in `MobileApp.Tests` (158 passing, 7 skipped)
- **Infrastructure:** Aspire AppHost (primary local) · Docker + Docker Compose (legacy fallback) · GitHub Actions CI

> **Aspire caveat:** do **not** add `Aspire.MongoDB.Driver`. It requires MongoDB.Driver ≥ 3.9.0 against the pinned 2.25.0 and fails restore with `NU1605`. The project registers `AddSingleton<IMongoClient>` with a custom `MongoHealthCheck` instead (ADR-013). There is no Aspire workload to install.

## Project Structure

- `AgendaBuddy.AppHost/` — Aspire composition root: declares MongoDB + Kafka containers and all 7 service projects
- `AgendaBuddy.ServiceDefaults/` — shared cross-cutting setup referenced by every service (OpenTelemetry, health/liveness, service discovery, HTTP resilience, `PiiRedactingProcessor`)
- `Library/` — shared domain entities, `IRepository<T>` / `MongoDbRepository<T>`, all domain services, tools (CacheAside, EnumHelper, SupportTools), `MongoConnectionResolver`, `MongoHealthCheck`, profession seed data
- `Library.ServerAuth/` — server-side auth primitives (JWT validation, ownership guards)
- `EventAndCommands/` — CQRS kernel: all commands, queries, handlers, events, and EventStore persistence
- `Kafka/` — `KafkaClient` for topic creation (Confluent.Kafka); broker address is configuration-driven
- `Booking/`, `Calendar/`, `Customer/`, `Provider/`, `Services/`, `Profession/`, `Identity/` — seven independent ASP.NET Minimal API microservices
- `Gateway/` — the eighth process (F-015). A thin YARP reverse proxy in front of the seven services — `MobileApp`'s **only** configured base address. No business logic, no auth validation (JWT passthrough only), no path rewriting. Builds its route/cluster table programmatically from the same Aspire service-discovery config keys (`services__<name>__http__0`) every service already reads (`Gateway/AspireServiceDiscoveryProxyConfigProvider.cs`) — an explicit `api/v1/{service}/**` allowlist, never a catch-all forward (ADR/threat T-302). Attaches the failed destination's cluster name (`failedService`) to a `ProblemDetails` body on a 5xx/timeout/unreachable destination (`Gateway/Program.cs`)
- `MobileApp/` — .NET MAUI client, and (as of F-015) a client that actually reaches the backend, through the Gateway. **Deliberately excluded from `agenda-buddy-backend.slnf`** — it is covered by three dedicated CI jobs instead (`build-android`, `build-ios` on a macOS runner, and `build-mobile-tests`). Its 165 tests (158 passing, 7 skipped) run under `/p:MobileWorkloads=false`
- `*.Tests/` projects mirror the service they test (e.g., `Library.Tests/`, `EventsAndCommands.Tests/`)
- `compose/` — Docker Compose data fixtures

## Development

- **Install:** `dotnet restore`
- **Dev server (primary):** `dotnet run --project AgendaBuddy.AppHost` — starts MongoDB, Kafka, all 7 services, and (as of F-015) the Gateway — 8 processes total
- **Dev server (legacy):** `docker compose -f docker-compose.yml -f docker-compose.override.yml up -d`
- **Build:** `dotnet build --no-restore`
- **Test (backend, 468 tests):** `dotnet test agenda-buddy-backend.slnf --collect:"XPlat Code Coverage"` — use the solution filter, not the full solution
- **Test (integration, 234 tests):** `dotnet test AgendaBuddy.IntegrationTests/AgendaBuddy.IntegrationTests.csproj` — ⚠️ a **separate command**. `AgendaBuddy.IntegrationTests` is deliberately excluded from the slnf (ADR-031) so the unit gate stays Docker-free, which means the backend command above **does not run it**. Needs a container runtime; `export PATH="$HOME/.rd/bin:$PATH"` first under Rancher Desktop. It has a `ProjectReference` to `MobileApp.csproj` (F-015, for `MobileClientRouteResolutionTest`) — always restore/build with `/p:MobileWorkloads=false`, or it pulls in MobileApp's default android/ios TargetFrameworks and fails with `NETSDK1147` on a machine with no MAUI workloads
- **Test (mobile, 165 tests):** `dotnet test MobileApp.Tests/MobileApp.Tests.csproj /p:MobileWorkloads=false` (158 passing, 7 skipped)
- **Format:** `dotnet format agenda-buddy-backend.slnf` — there is no `.editorconfig`, so this applies built-in defaults
- **Regenerate the OpenAPI specs:** `./scripts/generate-openapi.sh [Service…]` → `docs/api/openapi/`. Runs each service **standalone as Development** against a throwaway Mongo container, because Swashbuckle is registered only in Development and the AppHost's services do not run as Development
- **Run the app + iOS simulator:** `./scripts/run-ios.sh` — starts the AppHost, discovers the dynamic ports (including the Gateway's, injected as `MAUI_API_BASE_URL`), boots a simulator, launches `MobileApp`. As of F-015, the app calls the real backend through the Gateway — `SeedDataProvider` is deleted, and there is no fabricated fallback left in any ViewModel (see `bruno/agenda-buddy/` for a collection that also hits the real services directly, bypassing the Gateway)
- **Stop:** `Ctrl-C` on the AppHost (legacy: `docker compose down`)

### Local-run gotchas

- **`docker` is not on PATH** under Rancher Desktop — it lives at `~/.rd/bin`. Aspire shells out to docker, so `export PATH="$HOME/.rd/bin:$PATH"` first.
- **Never delete `AgendaBuddy.AppHost/Properties/launchSettings.json`.** It sets `DOTNET_ENVIRONMENT=Development`; without it the AppHost runs as `Production`, user secrets never load, every secret parameter goes `ValueMissing`, and all 7 services park in `Waiting` **with nothing logged**.
- **Three AppHost secrets** must exist in user secrets: `Parameters:mongodb-password`, `Parameters:jwt-public-key`, `Parameters:jwt-private-key`. See the README for provisioning on a new machine.
- **Debug the app model** with `Logging__LogLevel__Aspire=Debug` — resource state transitions and parameter states are Debug-level only.
- **MongoDB uses a persistent volume**, so its password must stay stable. If auth breaks: `docker volume rm agendabuddy.apphost-<hash>-mongodb-data`.
- **Running a service standalone** needs `--no-launch-profile`, else launchSettings overrides `ASPNETCORE_ENVIRONMENT`.
- macOS has no `timeout` — use background + sleep + kill.

## Architecture

Seven independent ASP.NET Minimal API microservices (Booking, Calendar, Customer, Provider, Services, Profession, Identity) each own their MongoDB collection and expose REST endpoints. All domain entities and services live in the shared `Library` project. Business logic flows through `EventAndCommands` (CQRS via MediatR): API handlers dispatch commands/queries to handlers, which call Library services and persist audit events to the MongoDB EventStore. Kafka provides async provider-to-customer messaging via per-provider topics.

Locally, `AgendaBuddy.AppHost` is the composition root — it declares the infrastructure, every service, and (as of F-015) the `Gateway`, assigning ports dynamically (no hardcoded host ports). Every service — and the Gateway itself — calls `builder.AddServiceDefaults()` exactly once, which supplies OpenTelemetry, `/health` (readiness, including a 5-second-cached MongoDB check) and `/alive` (liveness), service discovery, and HTTP resilience. **One `IMongoClient` singleton is shared process-wide** by all services and `EventStore`.

**`MobileApp` is the only client, and it reaches the backend through the Gateway, and only the Gateway** (F-015). The Gateway forwards `api/v1/{service}/**` to its matching destination by an explicit allowlist — never a catch-all — resolved live from the same Aspire service-discovery config every service already reads, so it survives a backend restart's dynamic-port reassignment without itself restarting. It does not validate, strip, or terminate the caller's JWT — auth passthrough only, forwarded byte-for-byte to the destination, which validates it exactly as it would a direct call. On a destination failure it attaches the failed cluster's name (`failedService`) to a `ProblemDetails` body, so the client can say "Booking is unavailable" rather than a generic error. **A gap found live, not by any automated test, and fixed in the same gate that found it (F-015-T14):** the Gateway's route allowlist initially had no entry for `api/v1/messages/**` or `api/v1/notifications/**` — both real routes on the Customer service (`Customer/Program.cs:255,333`) — so any request to them through the Gateway got a `gateway-no-route` 404. Fixed with a two-line `_routeSpecs` addition and four regression tests; see `verification.md` §3.1 for the reproduction and fix. **The allowlist remains the one place a new backend route group can go silently unreachable from the mobile client** — see the Key Files entry below.

See [docs/pdlc/archive/design/aspire-wiring/ARCHITECTURE.md](docs/pdlc/archive/design/aspire-wiring/ARCHITECTURE.md) for the Aspire design and [docs/pdlc/context/](docs/pdlc/context/) for a `file:line`-anchored map of the codebase.

## Coding Conventions

- Business logic in Library service layer only — not in API handlers
- Repository pattern only — `MongoDbRepository<T>` for all DB access
- Async all the way: every I/O method returns `Task` or `Task<T>`
- MongoDB field names via `[BsonElement("snake_case")]` attributes
- PascalCase for classes, methods, properties; `_camelCase` for private fields
- `[Required]`, `[EmailAddress]` data annotations on entity properties at the API boundary

## Key Files

- `Library/Entities/` — all domain entity definitions (AppointmentEntity, ProviderEntity, CustomerEntity, ServiceEntity, ProfessionEntity)
- `Library/Repositories/MongoDbRepository.cs` — generic MongoDB CRUD implementation
- `Library/Tools/CacheAside.cs` — distributed cache-aside extension (use this for all cached reads)
- `Library/Repositories/IRepository.cs` — `FindOneAndUpdateAsync(filter, update)` is the **only** partial-update primitive (ADR-032). Every other write here replaces a whole document. It **never upserts**, which is what stops a failed login for an unknown address creating an account
- `AgendaBuddy.ServiceDefaults/TransportSecurity.cs` — HSTS policy plus `UseAgendaBuddyTransportSecurity()`. **All seven services must call it immediately before `UseAuthentication()`** — `AddServiceDefaults()` runs on the builder, so it cannot position middleware itself. A test in `Library.Tests` fails if any service gets the order wrong or calls `UseHttpsRedirection` directly
- `Identity/Extensions/RateLimitingExtensions.cs` — per-IP limiter on `login` **and** `register`, the two routes that spend BCrypt (262 ms each, measured). `refresh` is deliberately unlimited
- `Library/Tools/ObjectIdJsonConverter.cs` — **register this in any service that returns an entity.** Without it `System.Text.Json` emits `"id": {"timestamp":…,"machine":…}`, which cannot be read back into an `ObjectId` at all. Registered in Booking, Customer and Provider by F-014; Calendar, Services and Profession still emit the broken shape (filed)
- `Library/Services/PaymentGatewayFactory.cs` — payments are **non-charging** unless `Payments:Stripe:ApiKey` is configured. A `Succeeded` payment with a `local_` intent id moved no money (ADR-038)
- `Library/Entities/AppointmentEntity.cs` — `TransitionTo` is the **only** way to change an appointment's status (ADR-037). The `PUT` route ignores the status field; restoring that assignment reopens threat T-203
- `EventAndCommands/ConfigurationLoader.cs` — MongoDB config bootstrap for EventAndCommands
- `EventAndCommands/Persistence/EventStore.cs` — audit event persistence. Takes an injected `IMongoClient`; it no longer builds one per request scope. *(The long-standing `Persitency` misspelling was corrected in F-016; CONSTITUTION §9's prohibition against renaming it is retired.)*
- `Booking/Program.cs` — representative Minimal API entry point showing the full wiring pattern
- `Gateway/Program.cs` — the reverse-proxy pipeline: `AddServiceDefaults()`, transport security before auth (no auth middleware here — passthrough), YARP registration, the `MapFallback` handler that shapes an unmatched path into `gateway-no-route`, and the response transform that shapes a destination failure into `gateway-destination-unreachable` + `failedService`
- `Gateway/AspireServiceDiscoveryProxyConfigProvider.cs` — the explicit `api/v1/{service}/**` route allowlist (never a catch-all — T-302) built from live Aspire service-discovery config, polled every 2 seconds. **A reviewer should check this file first for any new client-facing route** — a route a backend service adds is invisible to `MobileApp` through the Gateway until a line is added here, and nothing fails loudly when it's missing. F-015's own `messages`/`notifications` gap (found live, fixed same-gate — `verification.md` §3.1) is the worked example
- `MobileApp/Routing/` — Maui-free, DI-free static route-builder classes (one per `*ApiService`) extracted so route/verb/payload logic is testable under `MobileApp.Tests`'s `net10.0` fallback TFM, not only the `#if MOBILE`-gated Maui bootstrap (F-015)
- `MobileApp/Infrastructure/GatewayErrorMapper.cs` — maps the Gateway's `failedService` cluster id to a human-readable display name in the error banner ("booking" → "Booking is unavailable right now. Try again.")
- `MobileApp/Infrastructure/ApiBaseUrlResolver.cs` — `MAUI_API_BASE_URL` env var → `ApiBaseUrl` config → hardcoded `http://localhost:6036/` fallback. `scripts/run-ios.sh` sets the env var to the Gateway's discovered address; without it, the app still falls back to the old wrong default
- `MobileApp/Services/SeedDataProvider.cs` — **deleted by F-015.** No ViewModel has a fabricated-data fallback left; a real error or a real empty result reaches the UI instead
- `AgendaBuddy.AppHost/Program.cs` + `AgendaBuddy.AppHost/AppHostWiring.cs` — the Aspire app model: every resource, reference, and the run/publish (`DeploymentTarget`) split. The `Gateway` resource `WithReference`/`WaitFor`s all seven services
- `AgendaBuddy.ServiceDefaults/Extensions.cs` — `AddServiceDefaults()` / `MapDefaultEndpoints()`, called by all 7 services
- `AgendaBuddy.ServiceDefaults/PiiRedactingProcessor.cs` — strips email addresses from span attributes before export. **Do not remove:** `url.path` was leaking real customer emails (threat T-004)
- `Library/MongoConnectionResolver.cs` — resolves the Mongo connection string (Aspire → environment → appsettings) with an actionable failure message
- `AgendaBuddy.IntegrationTests/` — the only integration suite. `Harness/ServiceHostFixture.cs` hosts a real service over HTTP against a MongoDB Testcontainer (container per test class, database per test); `Harness/MongoEndpointGuard.cs` **fails the suite closed** if the resolved endpoint is not this session's own container. Add a per-service anchor alias to `GlobalUsings.cs` to host a new service — never `WebApplicationFactory<Program>`, which is ambiguous across all seven assemblies (see `Harness/EntryPoints.cs`)
- `agenda-buddy-backend.slnf` — the solution filter the backend CI job and local backend test runs target; excludes MobileApp **and `AgendaBuddy.IntegrationTests`** by design (ADR-031)
- `docs/api/openapi/` + `scripts/generate-openapi.sh` — generated OpenAPI specs for all 7 services, plus a route index. A **build artifact**, regenerable on demand; do not hand-edit
- `bruno/agenda-buddy/` — Bruno collection covering all 7 services, with the F-016 authorization expectations encoded in the request names (e.g. *"Create profession — MUST be 404 or 405"*). Two environments: `Local (Aspire AppHost)` and `Local (standalone)`
- `scripts/run-ios.sh` — one-command local run: AppHost + port discovery + iOS simulator + `MobileApp`
- `azure.yaml` + `.github/workflows/deploy.yml` — cloud deploy path. **Written, unit-tested, never executed**
- `docker-compose.yml` — legacy Kafka + Zookeeper + Schema Registry + service definitions
- `.github/workflows/dotnet.yml` — CI pipeline: restore → build → test → coverage upload, plus AppHost build and startup guards

---

### Security controls that default OFF

`Security:RateLimiting:Enabled` and `Security:Hsts:Enabled` are **off unless configured**, and gated on
configuration rather than `IsProduction()` — every service runs as **Production** under the local AppHost,
so the environment name cannot distinguish a laptop from a deployment (ADR-033). The AppHost injects
`Security__Local=true` locally and turns both **on** in the cloud graph; each service warns at startup,
naming the key, when a control is off outside a local run. Full surface in
`docs/pdlc/context/06-configuration.md`.

**PDLC memory:** `docs/pdlc/memory/` — CONSTITUTION.md, INTENT.md, OVERVIEW.md, DECISIONS.md, ROADMAP.md, STATE.md

## ⚠️ Open risk you should know about

The `agenda_buddy` MongoDB Atlas credential was committed and **is still in git history and still valid** — it was removed from the working tree in F-013, which is not the same as rotating it. The cluster holds client names, emails, phone numbers and appointment records, and has no backups. Rotation is a human-only action and is the hard prerequisite for any cloud deployment. See `docs/issues/ISSUE-002-atlas-credential-rotation.md`.


<!-- BEGIN BEADS INTEGRATION v:1 profile:minimal hash:46cd31e7 -->
## Beads Issue Tracker

This project uses **bd (beads)** for issue tracking. Run `bd prime` to see full workflow context and commands.

### Quick Reference

```bash
bd ready              # Find available work
bd show <id>          # View issue details
bd update <id> --claim  # Claim work
bd close <id>         # Complete work
```

### Rules

- Use `bd` for ALL task tracking — do NOT use TodoWrite, TaskCreate, or markdown TODO lists
- Run `bd prime` for detailed command reference and session close protocol
- Use `bd remember` for persistent knowledge — do NOT use MEMORY.md files

**Architecture in one line:** issues live in a local Dolt DB; sync uses `refs/dolt/data` on your git remote; `.beads/issues.jsonl` is a passive export. See https://github.com/gastownhall/beads/blob/main/docs/core-concepts/sync-concepts.md for details and anti-patterns.

## Agent Context Profiles

The managed Beads block is task-tracking guidance, not permission to override repository, user, or orchestrator instructions.

- **Conservative (default)**: Use `bd` for task tracking. Do not run git commits, git pushes, or Dolt remote sync unless explicitly asked. At handoff, report changed files, validation, and suggested next commands.
- **Minimal**: Keep tool instruction files as pointers to `bd prime`; use the same conservative git policy unless active instructions say otherwise.
- **Team-maintainer**: Only when the repository explicitly opts in, agents may close beads, run quality gates, commit, and push as part of session close. A current "do not commit" or "do not push" instruction still wins.

## Session Completion

This protocol applies when ending a Beads implementation workflow. It is subordinate to explicit user, repository, and orchestrator instructions.

1. **File issues for remaining work** - Create beads for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **Handle git/sync by active profile**:
   ```bash
   # Conservative/minimal/default: report status and proposed commands; wait for approval.
   git status

   # Team-maintainer opt-in only, unless current instructions forbid it:
   git pull --rebase
   bd dolt push
   git push
   git status
   ```
5. **Hand off** - Summarize changes, validation, issue status, and any blocked sync/commit/push step

**Critical rules:**
- Explicit user or orchestrator instructions override this Beads block.
- Do not commit or push without clear authority from the active profile or the current user request.
- If a required sync or push is blocked, stop and report the exact command and error.
<!-- END BEADS INTEGRATION -->
