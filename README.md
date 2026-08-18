# Agenda Buddy

Scheduling and appointment management platform for independent service providers — fitness coaches, tutors, therapists, software instructors, and anyone who offers personalized one-to-one sessions. Agenda Buddy replaces the juggle of calendar apps, contact spreadsheets, and direct messaging with a single place to manage your service catalog, client roster, bookings, and communications.

---

## Features

| Feature | Description |
|---------|-------------|
| **Identity & Auth** | JWT RS256 authentication — providers and customers log in and access only their own data |
| **Provider onboarding** | Sign up, define a profession, add services, and accept bookings |
| **Customer onboarding** | Sign up, discover providers, and subscribe to one |
| **Appointment lifecycle** | Book, confirm, update, cancel, and complete — status transitions enforced end-to-end |
| **Calendar & availability** | Provider sets available hours; customers can only book genuinely open slots |
| **Booking notifications** | In-app notifications for appointment created, confirmed, updated, and cancelled |
| **Provider–customer messaging** | Threaded in-app messaging with stable thread IDs |
| **Session notes** | Provider attaches private notes to each appointment — visible only to the provider |
| **Reporting dashboard** | Booking volume, estimated revenue, unique customers, and retention rate |
| **Payment integration** | Stripe payment intents collected at booking time |

---

## Architecture

Six independent **ASP.NET Core 10 Minimal API** microservices, each with its own MongoDB collection and Dockerfile:

```
Booking      — appointment CRUD and lifecycle
Calendar     — availability schedule and slot queries
Customer     — customer profile management
Provider     — provider profile and service catalog
Services     — service definitions and fee management
Profession   — profession/category seed data
Identity     — JWT issuance and credential management
```

All domain entities and services live in a shared **`Library`** project consumed by every microservice. Business logic flows through **`EventAndCommands`** (CQRS via MediatR): API handlers dispatch commands/queries to handlers, which call Library services and persist audit events to a MongoDB EventStore. **Kafka** provides async provider-to-customer messaging via per-provider topics.

```
┌─────────────────────────────────────────────────────┐
│  Mobile / API clients                               │
└────────────────┬────────────────────────────────────┘
                 │ HTTPS / JWT RS256
     ┌───────────▼───────────┐
     │   Identity service    │  ← JWT issuance
     └───────────────────────┘
     ┌──────┬──────┬──────┬──────┬──────┬──────┐
     │Book  │Cal   │Cust  │Prov  │Svc   │Prof  │  ← Minimal APIs
     └──┬───┴──┬───┴──┬───┴──┬───┴──┬───┴──┬───┘
        │      │      │      │      │      │
     ┌──▼──────▼──────▼──────▼──────▼──────▼───┐
     │            Library (shared)              │
     │  Entities · Services · Repository · Auth │
     └──────────────────┬───────────────────────┘
                        │
          ┌─────────────▼──────────────┐
          │   EventAndCommands (CQRS)  │
          │   MediatR · EventStore     │
          └─────────────┬──────────────┘
                        │
          ┌─────────────▼──────────────┐
          │   MongoDB  +  Kafka        │
          └────────────────────────────┘
```

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Language | C# 12 / .NET 10 |
| Framework | ASP.NET Core 10 Minimal APIs |
| Database | MongoDB (MongoDB.Driver 2.25) |
| Messaging | Kafka (Confluent) + MediatR 12 (CQRS) |
| Caching | `IDistributedCache` — cache-aside pattern, 5-min TTL |
| Auth | JWT RS256 — `AddAgendaBuddyAuthentication()`, key via env var |
| Payments | Stripe.net v45 |
| Testing | xUnit + Moq |
| Infrastructure | Docker + Docker Compose |
| CI/CD | GitHub Actions — restore → build → test → coverage |

---

## Project Structure

```
agenda-buddy/
├── Library/                   # Shared entities, services, repository, tools
├── EventAndCommands/          # CQRS: commands, queries, handlers, EventStore
├── Kafka/                     # KafkaClient — topic creation (Confluent)
├── Booking/                   # Booking microservice
├── Calendar/                  # Calendar microservice
├── Customer/                  # Customer microservice
├── Provider/                  # Provider microservice
├── Services/                  # Services microservice
├── Profession/                # Profession microservice
├── Identity/                  # Identity / auth microservice
├── *.Tests/                   # xUnit test projects mirroring each service
├── compose/                   # Docker Compose data fixtures
├── docs/pdlc/                 # PDLC memory: CONSTITUTION, INTENT, ROADMAP, STATE
└── docker-compose.yml         # Kafka + Zookeeper + Schema Registry + services
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- **A running container runtime — [Docker Desktop](https://www.docker.com/products/docker-desktop) or Podman.** This is a hard requirement for the local path, not a convenience: the AppHost starts MongoDB and Kafka as containers. Nothing runs locally without it. *(No Aspire workload install is needed — Aspire ships as NuGet packages, so `dotnet restore` is the only other prerequisite.)*

### Run locally

One command starts everything — MongoDB, Kafka, and all seven API services:

```bash
dotnet run --project AgendaBuddy.AppHost
```

The Aspire dashboard opens with all nine resources, their health, logs, traces, and metrics. `Ctrl+C` stops everything.

### First run on a new machine — three secrets

The AppHost needs three values, held in [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) scoped to `AgendaBuddy.AppHost`:

| Parameter | What it is |
|---|---|
| `jwt-public-key` | RSA public key (PEM) every service uses to verify tokens |
| `jwt-private-key` | RSA private key (PEM) Identity uses to sign them |
| `mongodb-password` | Root password for the local MongoDB container |

User secrets are **per machine and per user**, so every new host — and every fresh OS account — starts with none of them. Until they are set, the dashboard shows those parameters as `ValueMissing` and **all seven services sit in `Waiting`** (see troubleshooting below). Set all three in one go:

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

**User secrets only load in the `Development` environment**, which is why `AgendaBuddy.AppHost/Properties/launchSettings.json` sets `DOTNET_ENVIRONMENT=Development`. Do not delete that file, and do not run the AppHost with `--no-launch-profile` unless you export `DOTNET_ENVIRONMENT=Development` yourself — either mistake reproduces ISSUE-001, where the whole graph hangs silently.

You do **not** need to set a MongoDB connection string: the AppHost injects it.

### Troubleshooting the first run

**"Docker is not running" / the resources never leave `Starting`.** The most common first-run failure by a wide margin. Start Docker Desktop (or `podman machine start`) and re-run. The AppHost cannot provision MongoDB or Kafka without it.

**The containers come up but all seven services sit in `Waiting` forever, with nothing logged.** A service is only scheduled once its parameters resolve and the resources it waits for are healthy — neither of which is reported as an error, which is what makes this silent. Two causes, both diagnosable from the dashboard's parameter resources:

- **A parameter shows `ValueMissing`.** Its value isn't in user secrets, or the AppHost isn't running in `Development` and so never loaded them. Check `DOTNET_ENVIRONMENT` and `dotnet user-secrets list --project AgendaBuddy.AppHost`.
- **`mongodb` never reaches `Healthy`.** Usually the data volume was initialised with a different root password, so the health check can't authenticate. `MONGO_INITDB_ROOT_PASSWORD` is ignored on a non-empty `/data/db`, so changing the parameter is not enough — reset the volume:

```bash
docker volume ls | grep mongodb-data          # find it: agendabuddy.apphost-<hash>-mongodb-data
docker volume rm <name>                       # destroys local dev data only
```

**A service fails immediately with `No MongoDB connection string found. Set one of: …`.** You are running that service directly (`dotnet run --project Booking`) rather than through the AppHost. Either start the AppHost instead, or export the connection string yourself:

```bash
export ConnectionStrings__mongodb='mongodb://localhost:27017'
```

The committed Atlas credential has been removed from every `appsettings*.json`, and the keys were intentionally left in place as empty slots. So a standalone or Compose run now fails fast with that message instead of silently connecting to a shared cluster.

### Ports

The AppHost assigns host ports **dynamically** — services no longer bind the old hardcoded `localhost:603x`. Read the actual URL for a service from the dashboard. Two consequences:

- Two people (or two branches) can run the stack simultaneously without colliding.
- `scripts/seed/seed-mongo.sh` hardcodes `mongo:27017` and needs the assigned port to work. It also targets `ProviderDb` and `CustomerDb`, which no service reads. **Neither is fixed here** — treat that script as stale.

### Docker Compose (retained, superseded)

`docker-compose.yml` and `docker-compose.override.yml` still work and are kept deliberately, so reverting this feature is a single `git revert` with no loss of capability. They are no longer the recommended path — they provide no health model, no telemetry, and no connection-string injection:

```bash
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d
docker compose down
```

Note the Compose path now needs `ConnectionStrings` supplied, for the reason above.

### Health endpoints

Every service exposes two anonymous endpoints, for orchestrator probes:

| Path | Purpose | Healthy | Unhealthy |
|---|---|---|---|
| `/health` | **Readiness** — runs every check, including MongoDB connectivity | `200` `Healthy` | `503` `Unhealthy` |
| `/alive` | **Liveness** — only checks tagged `live`; does **not** touch MongoDB | `200` `Healthy` | `503` `Unhealthy` |

The split is deliberate: point your restart probe at `/alive` and your traffic probe at `/health`. When MongoDB is unreachable, `/health` goes `503` so the service stops receiving traffic while `/alive` stays `200`, so nothing restarts a process that is running correctly and merely waiting on its database. Response bodies are a bare status word — no check names or exception detail. `/health` results are cached for 5 seconds, so probing it in a loop does not multiply database round-trips.

### ⚠️ The dashboard is a sensitive surface

The Aspire dashboard exposes environment variables, configuration, logs, and traces for every resource. Secret parameters are masked, but treat the dashboard as privileged: do not expose its port beyond localhost, and do not screenshot it into a public issue.

### Build & test

```bash
dotnet restore
dotnet build --no-restore
dotnet test --collect:"XPlat Code Coverage"
```

`agenda-buddy-backend.slnf` is the solution minus the MAUI projects. CI builds and tests through it, and it is the faster loop locally when you are not touching `MobileApp`:

```bash
dotnet test agenda-buddy-backend.slnf
```

---

## Environment Variables

Secrets are never stored in source. **Under the AppHost you do not need to set the JWT keys or the connection string** — Aspire prompts for the keys once and injects the connection string. The table below applies when running a service standalone or via Compose:

| Variable | Service | Description | Under the AppHost |
|----------|---------|-------------|-------------------|
| `JWT_PRIVATE_KEY` | Identity | RSA private key (PEM) for JWT signing | supplied from the `jwt-private-key` secret parameter |
| `JWT_PUBLIC_KEY` | All services | RSA public key (PEM) for JWT verification | supplied from the `jwt-public-key` secret parameter |
| `STRIPE_SECRET_KEY` | Booking / Library | Stripe secret key for payment intents | **still required** — set it yourself |
| `ConnectionStrings__mongodb` | All services | MongoDB connection string | injected automatically |

The connection string is resolved in this order, first non-empty winning: `ConnectionStrings:mongodb`, `MongoDbSettings:ConnectionString`, `MongoDB:ConnectionString`, `LibrarySettings:MongoDB:ConnectionString`. If none resolves, the service fails at startup with a message naming all four.

---

## Key Patterns

- **Repository pattern** — all DB access via `IRepository<T>` / `MongoDbRepository<T>`; no raw MongoDB queries outside the repository
- **Cache-aside** — `CacheAside` extension on `IDistributedCache` (semaphore-guarded) used for all read-heavy queries
- **Ownership guard** — `OwnershipGuard.AssertOwner(user, email)` enforces that callers can only mutate their own resources; throws `ForbiddenException` (403) on violation
- **CQRS** — all mutations go through MediatR command handlers in `EventAndCommands`; every result is persisted to the EventStore (audit trail)
- **Per-provider Kafka topics** — each provider gets a dedicated topic derived from their email prefix

---

## Roadmap

| ID | Feature | Status | PR |
|----|---------|--------|----|
| F-001 | Auth & identity | ✅ Shipped | #19 |
| F-002 | Provider onboarding | ✅ Shipped | #20 |
| F-003 | Customer onboarding | ✅ Shipped | #21 |
| F-004 | Appointment lifecycle | ✅ Shipped | #22 |
| F-005 | Provider availability schedule | ✅ Shipped | #23 |
| F-006 | Booking notifications | ✅ Shipped | #24 |
| F-007 | Provider–customer messaging | ✅ Shipped | #25 |
| F-008 | Journal & notes | ✅ Shipped | #26 |
| F-009 | Reporting dashboard | ✅ Shipped | #27 |
| F-010 | Payment integration | ✅ Shipped | #28 |
| F-011 | Upgrade to .NET 10 | ✅ Shipped | #29 |
| F-012 | Mobile app (iOS + Android) | 🔵 In Progress | — |

---

## Contributing

Branch naming: `feature/<kebab-case-name>`  
Commit format: `<type>(<scope>): <description>` (types: `feat` `fix` `chore` `docs` `test` `refactor` `perf` `ci`)  
All PRs target `main` and require CI to pass.
