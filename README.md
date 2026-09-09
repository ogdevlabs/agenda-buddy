# AgendaMe

AgendaMe is a scheduling and appointment management platform for independent service providers — fitness coaches, tutors, therapists, software instructors, and anyone who offers personalized one-to-one sessions. It replaces the juggle of calendar apps, contact spreadsheets, and direct messaging with one place to manage a service catalog, client roster, bookings, and communications through a native mobile app.

The repository and .NET projects retain the `AgendaBuddy` name; `AgendaMe` is the user-facing product name.

---

## Features

| Feature | Description |
|---------|-------------|
| **Identity & Auth** | JWT RS256 authentication, per-IP rate limiting and self-clearing lockout on login/register, single-use refresh-token rotation |
| **Provider onboarding** | Sign up, define a profession, add services, and accept bookings |
| **Customer onboarding** | Sign up, discover providers, and subscribe to one |
| **Appointment lifecycle** | Request, book, reschedule, complete, and soft-cancel appointments; status transitions are **server-owned**, never client-asserted |
| **Calendar & availability** | Providers configure per-weekday hours and time off; authenticated customers can see free slots while appointment details remain owner-only |
| **Session notes** | Provider attaches private notes to each appointment — visible only to the provider |
| **Provider–customer messaging** | Subscription-gated, MongoDB-backed threaded messaging with per-message and per-thread mark-read flows |
| **Notifications** | Best-effort in-app, email, and push delivery; unread badge/filtering plus per-item and bulk mark-read flows |
| **Reporting dashboard** | Booking volume and completion counts; revenue is explicitly reported as unavailable rather than approximated (an appointment doesn't record which service it's for) |
| **Payments** | Non-charging by default (a recording gateway); real Stripe payment intents only when `Payments:Stripe:ApiKey` is configured |
| **Mobile client (iOS + Android)** | .NET MAUI app that reaches every capability above through a single Gateway address, with no fabricated fallback data |

Every read route that returns personal data requires authentication and enforces ownership; list endpoints are paginated and non-owners get a projected (not full) record.

---

## Architecture

**Eight** independent processes, orchestrated locally by **.NET Aspire**:

```
Identity     — JWT issuance and credential management
Booking      — appointment lifecycle, session notes, payments
Calendar     — availability schedule and slot queries
Customer     — customer profiles, messaging, notifications
Provider     — provider profile, service catalog, reporting
Services     — service definitions and fee management
Profession   — profession/category seed data (anonymous reference data)
Gateway      — YARP reverse proxy; the mobile client's only address
```

Shared entities, repositories, and domain services live in `AgendaBuddy.Library`; `AgendaBuddy.Library.ServerAuth` holds JWT validation and ownership guards. Six domain services use a four-project Clean Architecture split: thin `*.Api` endpoint modules dispatch MediatR commands and queries to `*.Core`, contracts live in `*.Domain`, and `*.Infrastructure` is intentionally empty until needed. Identity deliberately remains a direct-service exception. `AgendaBuddy.EventAndCommands` now contains only the shared audit EventStore kernel, not domain handlers. Messaging and notification fan-out are synchronous and in-process; there is no message broker.

The **Gateway** is the one thing that changed the shape of this diagram: `MobileApp` does not call the seven domain services directly. It calls the Gateway, which forwards `api/v1/{service}/**` to the matching destination via an explicit route allowlist — resolved live from Aspire service discovery, so a backend's dynamic port reassignment never needs the Gateway to restart. The Gateway has no business logic and does not validate the caller's JWT — it forwards it byte-for-byte, so the destination authenticates and authorizes exactly as it would a direct call.

```
┌─────────────────────────────────────────────────────┐
│  MobileApp (iOS / Android) — the only client         │
└────────────────┬────────────────────────────────────┘
                 │ HTTP, api/v1/{service}/** only
     ┌───────────▼───────────┐
     │        Gateway        │  ← YARP, explicit allowlist, JWT passthrough
     └───┬───┬───┬───┬───┬───┬┘
         │   │   │   │   │   │
     ┌───▼┐┌▼───┐┌▼──┐┌▼──┐┌▼──┐┌▼────┐┌▼────────┐
     │Book││Cal ││Cust││Prov││Svc││Prof ││Identity │  ← Minimal APIs
     └──┬─┘└──┬─┘└──┬─┘└──┬─┘└─┬─┘└─┬───┘└────┬────┘
        │     │     │     │    │    │         │
     ┌──▼─────▼─────▼─────▼────▼────▼─────────▼───┐
     │            Library (shared)                 │
     │  Entities · Services · Repository · Auth     │
     └──────────────────┬────────────────────────────┘
                        │
          ┌─────────────▼──────────────┐
          │  Service *.Core handlers  │
          │  MediatR · audit writes   │
          └─────────────┬──────────────┘
                        │
          ┌─────────────▼──────────────┐
          │          MongoDB           │
          └────────────────────────────┘
```

Every service (and the Gateway) calls `AddServiceDefaults()` exactly once, which supplies OpenTelemetry, `/health`/`/alive`, service discovery, HTTP resilience, and PII-redacted telemetry.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Language | C# / .NET 10 |
| Framework | ASP.NET Core 10 Minimal APIs |
| Orchestration | .NET Aspire 13 — hosting-only (`AgendaBuddy.AppHost` + `AgendaBuddy.ServiceDefaults`) |
| Gateway | YARP reverse proxy |
| Database | MongoDB (MongoDB.Driver **pinned at 2.25.0** — do not add `Aspire.MongoDB.Driver`, see `CLAUDE.md`) |
| Messaging | MongoDB-backed messaging + synchronous in-process notification dispatch |
| Caching | `IDistributedCache` — cache-aside pattern, 5-min TTL |
| Auth | JWT RS256 — `AddAgendaBuddyAuthentication()`, keys via Aspire secret parameters |
| Payments | Stripe.net — non-charging recording gateway by default |
| Mobile | .NET MAUI (iOS + Android), routed through the Gateway |
| Testing | xUnit across three separate suites; no single command runs all of them (see [Build & test](#build--test)) |
| Infrastructure | Aspire AppHost (primary) · Docker Compose (legacy fallback) · GitHub Actions CI |
| Observability | OpenTelemetry → Aspire dashboard, with a PII-redacting span processor |

---

## Project Structure

```
agenda-buddy/
├── AgendaBuddy.AppHost/        # Aspire composition root — declares every resource, local vs. cloud shape
├── AgendaBuddy.ServiceDefaults/# OpenTelemetry, health/liveness, service discovery, HTTP resilience
├── AgendaBuddy.Library/        # Shared entities, services, repositories, tools
├── AgendaBuddy.Library.ServerAuth/ # JWT validation and ownership guards
├── AgendaBuddy.EventAndCommands/   # EventStore and audit infrastructure
├── AgendaBuddy.{Domain}.Api/   # HTTP endpoint and DI wiring for six split services
├── AgendaBuddy.{Domain}.Core/  # MediatR handlers for those services
├── AgendaBuddy.{Domain}.Domain/# Per-service commands, queries, DTOs and envelopes
├── AgendaBuddy.{Domain}.Infrastructure/ # Intentionally empty until needed
├── AgendaBuddy.Identity/       # Direct IdentityService-based auth service
├── AgendaBuddy.Gateway/        # YARP reverse proxy; MobileApp's only backend address
├── AgendaBuddy.MobileApp/      # .NET MAUI client (iOS + Android)
├── *.Tests/                    # xUnit test projects mirroring each service
├── AgendaBuddy.IntegrationTests/ # real services over HTTP against a MongoDB Testcontainer
├── AgendaBuddy.MobileApp.Tests/ # Mobile tests under a net10.0 fallback TFM
├── bruno/agenda-buddy/          # Bruno API collection (hits the 7 services directly, bypassing the Gateway)
├── docs/api/openapi/            # generated OpenAPI specs — regenerate with scripts/generate-openapi.sh
├── compose/                     # Docker Compose data fixtures
├── docs/pdlc/                   # PDLC memory: CONSTITUTION, OVERVIEW, ROADMAP, STATE, episodes
└── docker-compose.yml           # incomplete legacy fallback; AppHost is authoritative
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- **A running container runtime — [Docker Desktop](https://www.docker.com/products/docker-desktop), Podman, or Rancher Desktop.** The AppHost provisions MongoDB as a container. *(No Aspire workload install is needed — Aspire ships as NuGet packages, so `dotnet restore` is the only other prerequisite. If you're on Rancher Desktop, `docker` lives at `~/.rd/bin`, not on `PATH` — `export PATH="$HOME/.rd/bin:$PATH"` first.)*
- To run the mobile client: the MAUI workload (`dotnet workload install maui`) and, for iOS, Xcode + a simulator.

### Run locally

One command starts MongoDB, all seven API services, and the Gateway:

```bash
dotnet run --project AgendaBuddy.AppHost
```

The Aspire dashboard opens with the resource graph, health, logs, traces, and metrics. `Ctrl+C` stops the AppHost (see the shutdown gotcha below).

To also launch the mobile app against this stack, in an iOS simulator, with the Gateway's address auto-discovered:

```bash
./scripts/run-ios.sh
```

### First run on a new machine — three secrets

The AppHost needs three values, held in [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) scoped to `AgendaBuddy.AppHost`:

| Parameter | What it is |
|---|---|
| `jwt-public-key` | RSA public key (PEM) every service uses to verify tokens |
| `jwt-private-key` | RSA private key (PEM) Identity uses to sign them |
| `mongodb-password` | Root password for the local MongoDB container |

User secrets are **per machine and per user**, so every new host — and every fresh OS account — starts with none of them. Until they are set, the dashboard shows those parameters as `ValueMissing` and **every service sits in `Waiting`, with nothing logged** (see troubleshooting below). Set all three in one go:

```bash
# Matched RSA pair — Identity signs with the private key, all seven verify with the public one
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out /tmp/jwt.key
openssl rsa -in /tmp/jwt.key -pubout -out /tmp/jwt.pub

dotnet user-secrets set "Parameters:jwt-private-key"  "$(cat /tmp/jwt.key)" --project AgendaBuddy.AppHost
dotnet user-secrets set "Parameters:jwt-public-key"   "$(cat /tmp/jwt.pub)" --project AgendaBuddy.AppHost
dotnet user-secrets set "Parameters:mongodb-password" "$(openssl rand -hex 16)" --project AgendaBuddy.AppHost

rm -f /tmp/jwt.key /tmp/jwt.pub
dotnet user-secrets list --project AgendaBuddy.AppHost   # expect the three Parameters:* keys
```

Three things to get right:

- **The JWT keys must be a matched pair.** Generating them independently leaves every request returning `401` with nothing else obviously wrong. The commands above derive the public key from the private one, so they always match.
- **Each host can have its own pair.** Nothing shares tokens across machines, so there is no need to copy secrets between them. If you *do* want identical tokens on two machines, copy both key values verbatim.
- **`mongodb-password` only takes effect on a first-ever run.** MongoDB ignores `MONGO_INITDB_ROOT_PASSWORD` when `/data/db` is non-empty, so if that host already has an `agendabuddy.apphost-*-mongodb-data` volume from an earlier attempt, remove it first — otherwise authentication fails permanently.

Nothing here is ever written to the repository; this replaces the undocumented gitignored `.env` file the services previously relied on. Because all three are declared as secret parameters, the dashboard masks them.

`mongodb-password` is declared explicitly rather than generated because MongoDB runs on a persistent data volume: a generated password changes on every run while the volume keeps the root user created by the first one, so nothing would ever authenticate again.

**User secrets only load in the `Development` environment**, which is why `AgendaBuddy.AppHost/Properties/launchSettings.json` sets `DOTNET_ENVIRONMENT=Development`. **Never delete that file.** Do not run the AppHost with `--no-launch-profile` unless you export `DOTNET_ENVIRONMENT=Development` yourself — either mistake reproduces [ISSUE-001](docs/issues/ISSUE-001-apphost-never-launches-services.md), where the whole graph hangs silently.

You do **not** need to set a MongoDB connection string: the AppHost injects it.

### Troubleshooting the first run

**"Docker is not running" / the resources never leave `Starting`.** Start Docker Desktop (or `podman machine start` / Rancher Desktop) and re-run. The AppHost cannot provision MongoDB without it.

**Every service sits in `Waiting` forever, with nothing logged.** A service is only scheduled once its parameters resolve and the resources it waits for are healthy — neither of which is reported as an error, which is what makes this silent. Two causes, both diagnosable from the dashboard's parameter resources:

- **A parameter shows `ValueMissing`.** Its value isn't in user secrets, or the AppHost isn't running in `Development` and so never loaded them. Check `DOTNET_ENVIRONMENT` and `dotnet user-secrets list --project AgendaBuddy.AppHost`.
- **`mongodb` never reaches `Healthy`.** Usually the data volume was initialised with a different root password, so the health check can't authenticate. `MONGO_INITDB_ROOT_PASSWORD` is ignored on a non-empty `/data/db`, so changing the parameter is not enough — reset the volume:

```bash
docker volume ls | grep mongodb-data          # find it: agendabuddy.apphost-<hash>-mongodb-data
docker volume rm <name>                       # destroys local dev data only
```

**A service fails immediately with `No MongoDB connection string found. Set one of: …`.** You are running that service directly (`dotnet run --project AgendaBuddy.Booking.Api`) rather than through the AppHost. Either start the AppHost instead, or export the connection string yourself:

```bash
export ConnectionStrings__mongodb='mongodb://localhost:27017'
```

The committed Atlas credential has been removed from every `appsettings*.json` (it is **still recoverable from git history and still valid — rotation is a human-only action**, see [ISSUE-002](docs/issues/ISSUE-002-atlas-credential-rotation.md)), and the keys were intentionally left in place as empty slots. So a standalone or Compose run now fails fast with that message instead of silently connecting to a shared cluster.

**Shutting down leaves orphaned processes.** `SIGTERM` on the AppHost has repeatedly left every service process running after the AppHost itself exits (a known, recurring gotcha — not fixed, worked around). If `dotnet run --project AgendaBuddy.Booking.Api` fails with "address already in use" after a `Ctrl-C`, find and kill the orphans:

```bash
pkill -f "agenda-buddy/.*bin/Debug"
```

### Talking to the app: only through the Gateway

`MobileApp` is the only client, and it reaches the backend through the Gateway, and **only** the Gateway. For `curl` or Bruno, prefix every route with `api/v1/{service}` and use `http://localhost:6080` during an AppHost run. The `bruno/agenda-buddy/` collection follows that path for application requests; only its health folder calls services directly because `/health` and `/alive` are deliberately outside the Gateway allowlist.

### Ports

The AppHost assigns all seven service host ports dynamically. The Gateway is the deliberate exception: its local host port is pinned to `6080` so the mobile client and Bruno have one stable address. Read direct service URLs from the dashboard only when diagnosing a service or running the Bruno health requests. Two consequences:

- Two people (or two branches) can run the stack simultaneously without colliding.
- `scripts/seed/seed-mongo.sh` hardcodes `mongo:27017` and needs the assigned port to work. It also targets `ProviderDb` and `CustomerDb`, which no service reads. **Neither is fixed here** — treat that script as stale.

### Docker Compose (legacy and incomplete)

The Compose files are retained as an incomplete legacy fallback. Only Identity is active; several old service entries are commented out, the broker containers are dead configuration, and there is no Gateway. Use the AppHost for a working full stack.

```bash
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d
docker compose down
```

Note the Compose path now needs `ConnectionStrings` supplied, for the reason above.

### Health endpoints

Every service (and the Gateway) exposes two anonymous endpoints, for orchestrator probes:

| Path | Purpose | Healthy | Unhealthy |
|---|---|---|---|
| `/health` | **Readiness** — runs every check, including MongoDB connectivity | `200` `Healthy` | `503` `Unhealthy` |
| `/alive` | **Liveness** — only checks tagged `live`; does **not** touch MongoDB | `200` `Healthy` | `503` `Unhealthy` |

The split is deliberate: point your restart probe at `/alive` and your traffic probe at `/health`. When MongoDB is unreachable, `/health` goes `503` so the service stops receiving traffic while `/alive` stays `200`, so nothing restarts a process that is running correctly and merely waiting on its database. Response bodies are a bare status word — no check names or exception detail. `/health` results are cached for 5 seconds, so probing it in a loop does not multiply database round-trips.

### ⚠️ The dashboard is a sensitive surface

The Aspire dashboard exposes environment variables, configuration, logs, and traces for every resource. Secret parameters are masked, but treat the dashboard as privileged: do not expose its port beyond localhost, and do not screenshot it into a public issue.

### Deploying to the cloud

The dev Azure environment is deployed through `azure.yaml`, `.github/workflows/deploy.yml`, and the
Terraform bootstrap in `infra/terraform/`. A successful `main` pipeline automatically redeploys relevant
backend changes; an hourly drift workflow compares the deployed SHA with `main` and repairs missed path-filter
deployments. Only the Gateway has public ingress. See [docs/deployment.md](docs/deployment.md) and
[DEPLOYMENTS.md](docs/pdlc/memory/DEPLOYMENTS.md) for the current runbook and evidence.

The original Atlas credential committed in git history remains a separate human-owned rotation risk; see
[ISSUE-002](docs/issues/ISSUE-002-atlas-credential-rotation.md).

→ **[docs/deployment.md](docs/deployment.md)** for the full procedure, what is verified, and the list of gaps between this and a production posture.

### Build & test

```bash
dotnet restore
dotnet build --no-restore
```

Three separate test commands — no single command runs all suites:

```bash
# Backend tests across the backend solution filter
dotnet test agenda-buddy-backend.slnf --collect:"XPlat Code Coverage"

# Integration tests: real services over HTTP against a MongoDB Testcontainer
dotnet test AgendaBuddy.IntegrationTests/AgendaBuddy.IntegrationTests.csproj /p:MobileWorkloads=false

# Mobile tests; live-Identity acceptance tests skip when the service is unavailable
dotnet test AgendaBuddy.MobileApp.Tests/AgendaBuddy.MobileApp.Tests.csproj /p:MobileWorkloads=false
```

`agenda-buddy-backend.slnf` is the solution minus MobileApp and `AgendaBuddy.IntegrationTests` (the latter is excluded so the unit gate stays Docker-free) — CI runs it as the fast loop. The Integration project has a `ProjectReference` to `MobileApp.csproj`, so always pass `/p:MobileWorkloads=false` when restoring or building it directly, or it pulls in MAUI's android/ios TargetFrameworks and fails on a machine with no MAUI workloads installed.

---

## Environment Variables

Secrets are never stored in source. **Under the AppHost you do not need to set the JWT keys or the connection string** — Aspire prompts for the keys once and injects the connection string. The table below applies when running a service standalone or via Compose:

| Variable | Service | Description | Under the AppHost |
|----------|---------|-------------|-------------------|
| `JWT_PRIVATE_KEY` | Identity | RSA private key (PEM) for JWT signing | supplied from the `jwt-private-key` secret parameter |
| `JWT_PUBLIC_KEY` | All services | RSA public key (PEM) for JWT verification | supplied from the `jwt-public-key` secret parameter |
| `Payments:Stripe:ApiKey` | Booking / Library | Selects the real Stripe gateway; **unset by default**, which selects a non-charging recording gateway instead | not set by the AppHost — configure explicitly if you want real Stripe |
| `ConnectionStrings__mongodb` | All services | MongoDB connection string | injected automatically |

The connection string is resolved in this order, first non-empty winning: `ConnectionStrings:mongodb`, `MongoDbSettings:ConnectionString`, `MongoDB:ConnectionString`, `LibrarySettings:MongoDB:ConnectionString`. If none resolves, the service fails at startup with a message naming all four.

**Two security controls default OFF** and are gated on configuration rather than environment name, because every service runs as `Production` under the local AppHost: `Security:RateLimiting:Enabled` and `Security:Hsts:Enabled`. The AppHost sets `Security__Local=true` locally (both stay off, no warning logged) and turns both on in the cloud graph. Each service warns loudly at startup, naming the key, if a control is off outside a local run.

---

## Key Patterns

- **Repository pattern** — all DB access via `IRepository<T>` / `MongoDbRepository<T>`; no raw MongoDB queries outside the repository. `FindOneAndUpdateAsync` is the only partial-update primitive and never upserts
- **Cache-aside** — `CacheAside` extension on `IDistributedCache` (semaphore-guarded) used for all read-heavy queries. ⚠️ No cache invalidation exists anywhere yet — a provider who finishes onboarding can be absent from discovery for up to 5 minutes
- **Ownership guard** — `OwnershipGuard.AssertOwner(user, email)` enforces that callers can only mutate their own resources; throws `ForbiddenException` (403) on violation, mapped centrally
- **CQRS** — six domain services dispatch to MediatR handlers in their own `*.Core` projects; command results are persisted to the shared EventStore audit trail
- **Server-owned state transitions** — appointment status changes only through `AppointmentEntity.TransitionTo`, applied via a dedicated route; `PUT` ignores a client-asserted status
- **Explicit route allowlist, never a catch-all** — the Gateway's `_routeSpecs` is the single source of truth for what the mobile client can reach; a backend route invisible here is invisible to the app, with nothing failing loudly
- **Best-effort notification fan-out** — producers call `INotificationDispatcher`; inbox, email, and push failures are isolated, with no broker, outbox, or retry

---

## Roadmap

The detailed feature history and current backlog live in [ROADMAP.md](docs/pdlc/memory/ROADMAP.md); operational
state lives in [STATE.md](docs/pdlc/memory/STATE.md). Beads is the durable source for open work (`bd ready`,
`bd list --status=open`). Keeping the full feature table in one place avoids this README becoming a second,
stale roadmap.

---

## Contributing

Branch naming: `feat/<F-NNN>-<kebab-case-name>` for feature work.
Commit format: `<type>(<scope>): <description>` (types: `feat` `fix` `chore` `docs` `test` `refactor` `perf` `ci`)
All PRs target `main` and require CI to pass — `build-and-test`, `Integration — real services + MongoDB`, `Mobile — Android Build`, `Mobile — iOS Build`, and `Mobile — Unit Tests`.
