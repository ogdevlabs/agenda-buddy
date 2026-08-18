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
- **Testing:** xUnit — 379 tests total: 305 across 12 backend projects + 74 in `MobileApp.Tests`
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
- `MobileApp/` — .NET MAUI client. **Deliberately excluded from `agenda-buddy-backend.slnf`** — it is covered by three dedicated CI jobs instead (`build-android`, `build-ios` on a macOS runner, and `build-mobile-tests`). Its 74 tests (67 passing, 7 skipped) run under `/p:MobileWorkloads=false`
- `*.Tests/` projects mirror the service they test (e.g., `Library.Tests/`, `EventsAndCommands.Tests/`)
- `compose/` — Docker Compose data fixtures

## Development

- **Install:** `dotnet restore`
- **Dev server (primary):** `dotnet run --project AgendaBuddy.AppHost` — starts MongoDB, Kafka, and all 7 services
- **Dev server (legacy):** `docker compose -f docker-compose.yml -f docker-compose.override.yml up -d`
- **Build:** `dotnet build --no-restore`
- **Test (backend, 305 tests):** `dotnet test agenda-buddy-backend.slnf --collect:"XPlat Code Coverage"` — use the solution filter, not the full solution
- **Test (mobile, 74 tests):** `dotnet test MobileApp.Tests/MobileApp.Tests.csproj /p:MobileWorkloads=false`
- **Format:** `dotnet format agenda-buddy-backend.slnf` — there is no `.editorconfig`, so this applies built-in defaults
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

Locally, `AgendaBuddy.AppHost` is the composition root — it declares the infrastructure and every service, assigning ports dynamically (no hardcoded host ports). Every service calls `builder.AddServiceDefaults()` exactly once, which supplies OpenTelemetry, `/health` (readiness, including a 5-second-cached MongoDB check) and `/alive` (liveness), service discovery, and HTTP resilience. **One `IMongoClient` singleton is shared process-wide** by all services and `EventStore`.

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
- `EventAndCommands/ConfigurationLoader.cs` — MongoDB config bootstrap for EventAndCommands
- `EventAndCommands/Persistence/EventStore.cs` — audit event persistence. Takes an injected `IMongoClient`; it no longer builds one per request scope. *(The long-standing `Persitency` misspelling was corrected in F-016; CONSTITUTION §9's prohibition against renaming it is retired.)*
- `Booking/Program.cs` — representative Minimal API entry point showing the full wiring pattern
- `AgendaBuddy.AppHost/Program.cs` + `AgendaBuddy.AppHost/AppHostWiring.cs` — the Aspire app model: every resource, reference, and the run/publish (`DeploymentTarget`) split
- `AgendaBuddy.ServiceDefaults/Extensions.cs` — `AddServiceDefaults()` / `MapDefaultEndpoints()`, called by all 7 services
- `AgendaBuddy.ServiceDefaults/PiiRedactingProcessor.cs` — strips email addresses from span attributes before export. **Do not remove:** `url.path` was leaking real customer emails (threat T-004)
- `Library/MongoConnectionResolver.cs` — resolves the Mongo connection string (Aspire → environment → appsettings) with an actionable failure message
- `agenda-buddy-backend.slnf` — the solution filter the backend CI job and local backend test runs target; excludes MobileApp by design
- `azure.yaml` + `.github/workflows/deploy.yml` — cloud deploy path. **Written, unit-tested, never executed**
- `docker-compose.yml` — legacy Kafka + Zookeeper + Schema Registry + service definitions
- `.github/workflows/dotnet.yml` — CI pipeline: restore → build → test → coverage upload, plus AppHost build and startup guards

---

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
