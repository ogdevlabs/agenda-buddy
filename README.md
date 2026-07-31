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
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

### Run locally

```bash
# Start all services (Kafka, MongoDB, microservices)
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d

# Stop
docker compose down
```

### Local service endpoints

| Service | Port |
|---------|------|
| Identity | `http://localhost:80` (HTTP) / `http://localhost:81` (gRPC) |
| EventAndCommands | internal |
| Kafka broker | `localhost:9092` |
| Schema Registry | `http://localhost:8081` |
| Kafka UI | `http://localhost:8080` |

### Build & test

```bash
dotnet restore
dotnet build --no-restore
dotnet test --collect:"XPlat Code Coverage"
```

---

## Environment Variables

Secrets are never stored in source. Set these before running:

| Variable | Service | Description |
|----------|---------|-------------|
| `JWT_PRIVATE_KEY` | Identity | RSA private key (PEM) for JWT signing |
| `JWT_PUBLIC_KEY` | All services | RSA public key (PEM) for JWT verification |
| `STRIPE_SECRET_KEY` | Booking / Library | Stripe secret key for payment intents |
| `ConnectionStrings` | All services | MongoDB connection string |

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
