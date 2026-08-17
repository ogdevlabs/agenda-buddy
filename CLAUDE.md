# Agenda Buddy

Agenda Buddy is a scheduling and appointment management platform for independent service providers (fitness coaches, tutors, therapists, software instructors, etc.) who need to manage clients, services, and appointments in one place. It is built as event-driven microservices on .NET 8.

## Tech Stack

- **Language:** C# 12 / .NET 8
- **Framework:** ASP.NET Core 8 Minimal APIs (one service per domain)
- **Database:** MongoDB (MongoDB.Driver 2.25)
- **Messaging:** Kafka (Confluent) + MediatR 12 (CQRS)
- **Caching:** IDistributedCache (cache-aside pattern, 5-min TTL)
- **Testing:** xUnit
- **Infrastructure:** Docker + Docker Compose; GitHub Actions CI

## Project Structure

- `Library/` — shared domain entities, `IRepository<T>` / `MongoDbRepository<T>`, all domain services, tools (CacheAside, EnumHelper, SupportTools), profession seed data
- `EventAndCommands/` — CQRS kernel: all commands, queries, handlers, events, and EventStore persistence
- `Kafka/` — `KafkaClient` for topic creation (Confluent.Kafka)
- `Booking/`, `Calendar/`, `Customer/`, `Provider/`, `Services/`, `Profession/` — six independent ASP.NET Minimal API microservices, each with its own MongoDB config and Dockerfile
- `*.Tests/` projects mirror the service they test (e.g., `Library.Tests/`, `EventsAndCommands.Tests/`)
- `compose/` — Docker Compose data fixtures

## Development

- **Install:** `dotnet restore`
- **Dev server:** `docker compose -f docker-compose.yml -f docker-compose.override.yml up -d`
- **Build:** `dotnet build --no-restore`
- **Test:** `dotnet test --collect:"XPlat Code Coverage"`
- **Stop:** `docker compose down`

## Architecture

Six independent ASP.NET Minimal API microservices (Booking, Calendar, Customer, Provider, Services, Profession) each own their MongoDB collection and expose REST endpoints. All domain entities and services live in the shared `Library` project. Business logic flows through `EventAndCommands` (CQRS via MediatR): API handlers dispatch commands/queries to handlers, which call Library services and persist audit events to the MongoDB EventStore. Kafka provides async provider-to-customer messaging via per-provider topics.

See [.claude/docs/architecture.md](.claude/docs/architecture.md) for full architecture details.

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
- `EventAndCommands/Persitency/EventStore.cs` — audit event persistence (note: "Persitency" is a known typo)
- `Booking/Program.cs` — representative Minimal API entry point showing the full wiring pattern
- `docker-compose.yml` — Kafka + Zookeeper + Schema Registry + EventAndCommands + Library services
- `.github/workflows/dotnet.yml` — CI pipeline: restore → build → test → coverage upload

---

**PDLC memory:** `docs/pdlc/memory/` — CONSTITUTION.md, INTENT.md, OVERVIEW.md, DECISIONS.md, ROADMAP.md, STATE.md


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
