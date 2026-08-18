# Roadmap

**Project:** Agenda Buddy
**Last updated:** 2026-08-18T13:20:00Z

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
| F-013 | aspire-wiring | Wire the solution as a .NET Aspire solution — AppHost orchestration for the six microservices plus Identity, ServiceDefaults for telemetry/health/resilience, and Aspire-managed MongoDB + Kafka resources for local development | 13 | Shipped | — | 2026-08-18 | [EPISODE_aspire-wiring_2026-08-17.md](../episodes/EPISODE_aspire-wiring_2026-08-17.md) (`v0.1.0`, PR #35) |
| F-014 | wire-unreached-services | Register and route the six shipped-but-unreachable capabilities: NotificationService, MessageService, NoteService, PaymentService, ReportingService, DeactivateProviderCommand. All six have domain implementations and unit tests but no DI registration, no configured collection, and no HTTP route — so F-006 through F-010 are marked Shipped while being unreachable | 14 | Planned | — | — | — |
| F-015 | api-gateway-and-mobile-contract | Make the mobile client actually reach the backend: add the missing `api/v1/` prefixes, introduce a gateway so one base URL can address seven services, fix the three wrong base URLs, wire the unused refresh-token flow, and make LogoutAsync call the server | 15 | Planned | — | — | — |
| F-016 | secure-public-endpoints | Close the PII exposure: six anonymous endpoints leak data (worst is `GET /api/v1/providers`, which returns every provider's full record including customer emails, unauthenticated and unpaginated); ownership-guard both Calendar routes (IDOR); actually call `OwnershipGuard.AssertRole`; paginate list endpoints; map ForbiddenException to 403 centrally | 16 | Planned | — | — | — |
| F-017 | container-and-cd-hardening | Fix the container and CI story: three Dockerfiles publish net10.0 output onto a dotnet/runtime:8.0 base and cannot run; delete the three class-library Dockerfiles and their Compose services; implement the CONSTITUTION §7 dependency-audit + secret-scan gate; add image build/scan/push to CI. *(The integration-test capability moved to F-018 on 2026-08-18.)* | 17 | Planned | — | — | — |
| F-018 | api-refactor-foundations | **Stage 1/3 of the API refactor program.** Testcontainers integration-test harness wired into CI; `MobileApp` into CI (resolves `agenda-buddy-prr`); `Persitency` → `Persistence` rename; constitution amendments + ADRs. Builds the safety net *before* any endpoint is rewritten | 18 | In Progress | oscargarcia@ogdevlabs.onmicrosoft.com | — | — |
| F-019 | api-refactor-pilot-booking | **Stage 2/3.** Full Clean Architecture (Api/Core/Domain/Infrastructure) + all five packages applied to `Booking` only, proving the target shape end-to-end. MediatR becomes the single dispatcher — finally honouring §3. Depends on F-018 | 19 | Planned | — | — | — |
| F-020 | api-refactor-rollout | **Stage 3/3.** Roll the proven shape across the remaining six services; delete the six `RequestCollection` classes and six duplicated exception blocks. Scope deliberately deferred until F-019 ships. Depends on F-019 | 20 | Planned | — | — | — |

> **API refactor decomposed 2026-08-18.** `/brainstorm refactor-minimal-apis` established that the requested scope — full Clean Architecture across 7 services (25 → ~46 projects), five new packages, a Testcontainers harness, the `Persitency` rename, and `MobileApp` into CI — is too large for one PRD. It is now a three-stage program with the **full Clean Architecture target preserved**, staged so the integration-test harness exists *before* the endpoint rewrite rather than being built alongside it. Program research: `docs/pdlc/brainstorm/brainstorm_refactor-minimal-apis_2026-08-18.md`.
>
> **Scope moved out of F-017:** the integration-test capability (CONSTITUTION §5's "all integration tests pass") now belongs to **F-018**, and is built with Testcontainers rather than bare `WebApplicationFactory`. F-017 keeps the container/CD hardening and the §7 security-scan gate.

> **Roadmap drift repaired 2026-08-18.** F-014 through F-017 existed as feature records in `docs/pdlc/tasks/` since 2026-08-15 but were never added to this table, which stopped at F-013. They are now listed. Also note F-001–F-012 are marked `Shipped` but predate PDLC ship tracking — they have no episode files, no CHANGELOG entries and no tags; `v0.1.0` is the first PDLC-tracked release.
>
> **F-018 is being worked ahead of F-014–F-017** at the user's explicit request — a structural refactor of every endpoint is cheaper before those four add more endpoints to it.

---

## Status Key

- **Planned** — Not yet started
- **In Progress** — Currently in brainstorm, build, or ship
- **Shipped** — Completed and deployed (date + episode link filled in)
- **Deferred** — Deprioritized or postponed
- **Dropped** — Removed from roadmap (reason noted)
