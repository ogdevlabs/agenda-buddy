# Overview
<!-- pdlc-template-version: 2.1.0 -->
<!-- This file is the living, aggregated record of everything this product does and has shipped.
     It is updated automatically by PDLC after every successful merge to main (during Reflect sub-phase).
     Use it to orient yourself after time away, onboard a new teammate, or brief Claude in a fresh session.
     Do not edit manually — let PDLC maintain it. If you need to correct something, update and note the reason. -->

**Project:** Agenda Buddy
**Last updated:** 2026-07-30T00:00:00Z

---

## Project Summary

Agenda Buddy is a scheduling and appointment management platform for independent service providers — fitness coaches, tutors, therapists, software instructors, and other one-to-one session specialists. It lets providers manage their client roster, service catalog, and appointment calendar in one place, replacing the patchwork of spreadsheets and general-purpose calendar apps most solo practitioners currently rely on.

---

## Active Functionality

*Pre-PDLC functionality — documented at initialization. Future entries tracked per episode.*

- Providers can register with a profile (name, email) and be assigned a Kafka topic for async messaging
- Providers can define a service catalog with name, description, fee, and fee type (Hourly/Fixed/Subscription)
- Providers can be updated, deactivated, and looked up by email
- Customers can register with a profile (name, email) and subscribe to providers
- Appointments can be created (Requested), updated, and cancelled; status transitions: Requested → Booked → Completed
- Calendar availability can be checked; existing appointments can be listed for a provider
- 100+ profession categories are seeded and queryable for provider classification
- All command mutations are persisted as events (success/fail) to the EventStore for audit
- Read-heavy queries use a cache-aside distributed cache with 5-minute TTL and stampede protection
- GitHub Actions CI runs build + test + coverage on every push/PR to main

---

## Shipped Features

*Pre-PDLC functionality documented above. PDLC-tracked features will appear here after first ship.*

| # | Feature | Date Shipped | Episode | PR |
|---|---------|-------------|---------|-----|
| — | Pre-PDLC baseline | 2024-04-16 → 2026-07-30 | — | — |

---

## Architecture Summary

- **Six independent ASP.NET Minimal API microservices**: Booking, Calendar, Customer, Provider, Services, Profession — each with its own MongoDB config, Dockerfile, and test project
- **Shared Library project**: all domain entities (`AppointmentEntity`, `ProviderEntity`, `CustomerEntity`, `ServiceEntity`, `ProfessionEntity`), the generic `IRepository<T>` / `MongoDbRepository<T>`, domain services, and tools (CacheAside, EnumHelper) live here and are consumed by all services
- **CQRS via MediatR**: the shared `EventAndCommands` project holds all commands, queries, and their handlers; each handler calls Library services and persists an audit event to EventStore
- **Kafka**: Confluent stack (Kafka + Zookeeper + Schema Registry + Kafka UI) run via Docker Compose; per-provider topics created on-demand
- **MongoDB**: document store for all domain data; embedded sub-documents for provider services and appointments
- **Cache-aside pattern**: `CacheAside` extension on `IDistributedCache` with semaphore-guarded double-checked locking

---

## Known Tech Debt

- [Added 2026-07-30] `EventAndCommands/Persitency/` is a typo (should be `Persistence`) — renaming deferred to avoid breaking references across all consumers
- [Added 2026-07-30] `KafkaClient` hardcodes `BootstrapServers = "localhost:9092"` — must be made configurable via appsettings before any non-local deployment
- [Added 2026-07-30] `topicName` computed but never used in `Booking/Program.cs` and other services — dead code cleanup needed
- [Added 2026-07-30] `provider` and `services-api` containers are commented out in `docker-compose.yml` — wire them in or remove the commented blocks
- [Added 2026-07-30] No authentication or authorization layer — all API endpoints are publicly accessible; this is a critical gap before any exposure beyond localhost
- [Added 2026-07-30] Customer and Profession command handlers have no test coverage in `EventsAndCommands.Tests` — coverage gap

---

## Decision Log Summary

1. **Microservices over monolith** (ADR-001) — six independent services, each deployable separately; adds operational complexity but enables independent scaling
2. **MongoDB + document model** (ADR-002) — nested provider/customer/appointment data fits documents well; no migration to relational planned
3. **CQRS + MediatR** (ADR-003) — clean separation of reads/writes in the shared EventAndCommands kernel; enables independent testing of each operation
4. **Event sourcing via EventStore** (ADR-004) — all command results persisted for audit; do not remove or bypass
5. **Cache-aside pattern** (ADR-006) — added for read performance with stampede protection; use `CacheAside.GetOrCreateAsync` for all cache interactions

See `docs/pdlc/memory/DECISIONS.md` for full ADR entries.
