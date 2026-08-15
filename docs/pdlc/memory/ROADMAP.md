# Roadmap

**Project:** Agenda Buddy
**Last updated:** 2026-08-15T16:45:00Z

---

## Build Strategy

**Approach:** Layered
**Rationale:** Brownfield project with an established microservices architecture — build each feature fully within existing patterns before moving to the next; foundational features first.

---

## Feature Backlog

<!-- Claimed by: git user email of the dev holding the roadmap-level Beads claim.
     This column is a cache of Beads assignees — if it disagrees with `bd list
     --label roadmap`, Beads wins (rendered on next /ship or /diagnose). -->

| ID | Feature | Description | Priority | Status | Claimed by | Shipped | Episode |
|----|---------|-------------|----------|--------|------------|---------|---------|
| F-001 | auth-and-identity | Authentication and authorization layer — protect all API endpoints with JWT-based auth so providers and customers log in and only see their own data | 1 | Shipped | oscargarcia@ogdevlabs.onmicrosoft.com | 2026-07-31 | PR #19 |
| F-002 | provider-onboarding-flow | End-to-end provider registration flow — a provider signs up, defines their profession, adds their first service, and is ready to accept bookings | 2 | Shipped | oscargarcia@ogdevlabs.onmicrosoft.com | 2026-07-31 | PR #20 |
| F-003 | customer-onboarding-flow | End-to-end customer registration flow — a customer signs up, discovers providers, and subscribes to one | 3 | Shipped | oscargarcia@ogdevlabs.onmicrosoft.com | 2026-07-31 | PR #21 |
| F-004 | appointment-lifecycle | Complete appointment lifecycle — book, confirm, update, cancel, and complete; with status transitions and validation rules enforced end-to-end | 4 | Shipped | oscargarcia@ogdevlabs.onmicrosoft.com | 2026-07-31 | PR #22 |
| F-005 | provider-availability-schedule | Provider sets their available hours/days so customers can only book slots that are genuinely open | 5 | Shipped | oscargarcia@ogdevlabs.onmicrosoft.com | 2026-07-31 | PR #23 |
| F-006 | booking-notifications | Email or in-app notifications for appointment created, confirmed, updated, and cancelled — sent to both provider and customer | 6 | Shipped | oscargarcia@ogdevlabs.onmicrosoft.com | 2026-07-31 | PR #24 |
| F-007 | provider-customer-messaging | In-app messaging between provider and customer using the existing Kafka infrastructure | 7 | Shipped | oscargarcia@ogdevlabs.onmicrosoft.com | 2026-07-31 | PR #25 |
| F-008 | journal-and-notes | Provider can attach private session notes to each appointment; visible only to the provider | 8 | Shipped | oscargarcia@ogdevlabs.onmicrosoft.com | 2026-07-31 | PR #26 |
| F-009 | reporting-dashboard | Provider sees their booking volume, revenue summary (from service fees), and customer retention metrics | 9 | Shipped | oscargarcia@ogdevlabs.onmicrosoft.com | 2026-07-31 | PR #27 |
| F-010 | payment-integration | Connect a payment gateway (Stripe) so providers can collect fees at booking time | 10 | Shipped | oscargarcia@ogdevlabs.onmicrosoft.com | 2026-07-31 | PR #28 |
| F-011 | upgrade-to-net10 | Upgrade all projects from .NET 8 to .NET 10 LTS — TFMs, NuGet packages, Docker base images, CI workflow | 11 | Shipped | oscargarcia@ogdevlabs.onmicrosoft.com | 2026-07-31 | PR #29 |
| F-012 | mobile-app | Cross-platform iOS and Android mobile client — providers and customers manage appointments, services, notifications, and messaging from a native mobile experience | 12 | Shipped | oscargarcia@ogdevlabs.onmicrosoft.com | 2026-07-31 | PR #31 |
| F-013 | aspire-wiring | Wire the solution as a .NET Aspire solution — AppHost orchestration for the six microservices plus Identity, ServiceDefaults for telemetry/health/resilience, and Aspire-managed MongoDB + Kafka resources for local development | 13 | In Progress | oscargarcia@ogdevlabs.onmicrosoft.com | — | — |

---

## Status Key

- **Planned** — Not yet started
- **In Progress** — Currently in brainstorm, build, or ship
- **Shipped** — Completed and deployed (date + episode link filled in)
- **Deferred** — Deprioritized or postponed
- **Dropped** — Removed from roadmap (reason noted)
