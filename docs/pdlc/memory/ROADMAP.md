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
| F-014 | wire-unreached-services | Register and route the six shipped-but-unreachable capabilities: NotificationService, MessageService, NoteService, PaymentService, ReportingService, DeactivateProviderCommand. All six have domain implementations and unit tests but no DI registration, no configured collection, and no HTTP route — so F-006 through F-010 are marked Shipped while being unreachable. **+ absorbed at Discover 2026-08-18: prevent double-booking** (`Start < End`, future-dated, no slot overlap) — `INTENT.md` names double-booking a core user frustration, and F-004 is marked Shipped while permitting it, which is the same "shipped but doesn't work" class this feature exists to fix | 16 | Planned | — | — | — |
| F-015 | api-gateway-and-mobile-contract | Make the mobile client actually reach the backend: add the missing `api/v1/` prefixes, introduce a gateway so one base URL can address seven services, fix the three wrong base URLs, wire the unused refresh-token flow, and make LogoutAsync call the server. **Re-scoped at Discover 2026-08-18:** the gateway is now a **prerequisite, not a nicety** — F-013 made the AppHost assign ports dynamically, so "fix the three wrong base URLs" is no longer coherent; there are no fixed URLs left to be right about. Consumes F-016's paginated response contract | 17 | Planned | — | — | — |
| F-016 | secure-public-endpoints | **Exposure closure — first in the remediation program.** Wave 1 builds the verification harness by absorbing **eight** tasks from F-018's approved plan (T01 `Persistence` rename, T05 test project + `InternalsVisibleTo`, T06 `CryptoSessionFixture`, T07 `DockerPreflight`, T08 `ServiceHostFixture`, T09 `TokenFactory`, T14 401/403 tests, **T18 integration CI job** — the last absorbed at the Plan gate after the readiness party found the feature's central claim was otherwise unenforced) — **because nothing in this solution can currently verify endpoint authz** (`docs/pdlc/context/11-testing.md:148`: `Program.cs` is not coverable, no integration test exists). Then: authenticate the **five** anonymous PII GETs (`providers`, `providers/{email}`, `customers`, `customers/{email}`, `services/{email}` — `professions*` stays anonymous as reference data); project the embedded appointment book + subscribed-customer list out of provider reads for non-owners; `OwnershipGuard` on both Calendar routes (IDOR); central `ForbiddenException`→403; `AssertRole` wired on provider + profession creation; paginate both list endpoints; stop read queries serialising full PII into `events`. **Claim: no endpoint leaks PII, and we can demonstrate it.** | 14 | Shipped | — | 2026-08-18 | [EPISODE_secure-public-endpoints_2026-08-18.md](../episodes/EPISODE_secure-public-endpoints_2026-08-18.md) (`v0.2.0`, PR #38). 20 tasks / 8 waves / 26 ACs (7 threat-derived `[security]`), ADR-022…031. Cloud deploy skipped — see `DEPLOYMENTS.md` |
| F-017 | container-and-cd-hardening | Fix the container and CI story: three Dockerfiles publish net10.0 output onto a dotnet/runtime:8.0 base and cannot run; delete the three class-library Dockerfiles and their Compose services; implement the CONSTITUTION §7 dependency-audit + secret-scan gate; add image build/scan/push to CI. *(The integration-test capability moved to F-018 on 2026-08-18.)* | 18 | Planned | — | — | — |
| F-018 | api-refactor-foundations | **Stage 1/3 of the API refactor program.** Testcontainers integration-test harness wired into CI; `MobileApp` into CI (resolves `agenda-buddy-prr`); `Persitency` → `Persistence` rename; constitution amendments + ADRs. Builds the safety net *before* any endpoint is rewritten. ⚠️ **Reduced to ~12 tasks — F-016 absorbs EIGHT** (T01, T05, T06, T07, T08, T09, T14, T18; the last added at F-016's Plan gate). What remains is OpenAPI + spec drift (T16/T17), `.editorconfig` (T03), constitution amendments (T02), the 10-green-run counter (T04), mobile CI (T19), the Tier 1–3 sweep (T11/T12/T13), Kafka fake (T10), reaping (T15) and final verification (T20). **Its plan, dependency graph and AC set all need amending on resume — do not rebuild a harness that already exists** | 19 | In Progress | — | — | Inception complete (PR #37). Construction **aborted before any code** on 2026-08-18 — resequenced behind the remediation program at the user's request |
| F-019 | api-refactor-pilot-booking | **Stage 2/3.** Full Clean Architecture (Api/Core/Domain/Infrastructure) + all five packages applied to `Booking` only, proving the target shape end-to-end. MediatR becomes the single dispatcher — finally honouring §3. Depends on F-018. *(Also inherits the `services.BuildServiceProvider()` ASP0000 fix in all 7 services — F-019 rewrites those `Program.cs` files anyway, so fixing it here avoids doing it twice.)* | 20 | Planned | — | — | — |
| F-020 | api-refactor-rollout | **Stage 3/3.** Roll the proven shape across the remaining six services; delete the six `RequestCollection` classes and six duplicated exception blocks. Scope deliberately deferred until F-019 ships. Depends on F-019 | 21 | Planned | — | — | — |
| F-021 | identity-hardening | **Second in the remediation program.** The auth system's own defects, split out of F-016 at Discover 2026-08-18 because F-016 grew past one PRD: replace `RefreshAsync`'s delete-then-insert with a targeted partial update (`IdentityService.cs:135`→`:155` — any fault between those lines **permanently destroys the account**: email, password hash and role, unlogged and unrecoverable); move `UseHttpsRedirection` **before** `UseAuthentication` in 6 services (the bearer token is currently parsed from the plaintext request before the redirect is issued); add rate limiting + account lockout on `POST /api/v1/auth/login` (currently unlimited attempts); fix `AssertOwner`'s null-claim pass (`string.Equals(null, null)` is `true`, so the guard succeeds — `AssertOwnerAny` handles this, `AssertOwner` does not). **Claim: the auth system itself is safe.** ⚠️ Its rate limiter must ship with a test-environment escape or it breaks F-016's harness | 15 | Planned | — | — | — |
| F-022 | password-reset-flow | No password reset, change, or forced-reset flow exists anywhere. `CredentialEntity.MustResetPassword` is written and **never read**, so the forced-reset flow the field exists for does not exist — a user who forgets their password has **no recovery path**. Filed rather than absorbed into the remediation program because it is a **new capability**, not a defect fix, and delivery requires `NotificationService` — which **F-014 wires**. Genuinely downstream | 22 | Planned | — | — | — |
| F-023 | token-revocation | `jti` is minted on every token (`IdentityService.cs:204`) but never recorded or checked, and there is no denylist — so an access token stays valid for up to **60 minutes after logout**. Filed rather than absorbed because it needs a real design decision (denylist store, per-request check cost, cache invalidation), not a one-task fix | 23 | Planned | — | — | — |
| F-024 | data-subject-rights | No export, deletion, or anonymisation capability. `BookingService.CancelAppointmentAsync` hard-deletes from `appointments`, but the same appointment survives **embedded in the provider document** and again in the `events` audit blobs — so "delete" leaves at least two copies, and any erasure request is unsatisfiable. Deferred, not urgent: the cluster is confirmed to hold **synthetic/development data only**, so there is no live obligation | 24 | Planned | — | — | — |

> **API refactor decomposed 2026-08-18.** `/brainstorm refactor-minimal-apis` established that the requested scope — full Clean Architecture across 7 services (25 → ~46 projects), five new packages, a Testcontainers harness, the `Persitency` rename, and `MobileApp` into CI — is too large for one PRD. It is now a three-stage program with the **full Clean Architecture target preserved**, staged so the integration-test harness exists *before* the endpoint rewrite rather than being built alongside it. Program research: `docs/pdlc/brainstorm/brainstorm_refactor-minimal-apis_2026-08-18.md`.
>
> **Scope moved out of F-017:** the integration-test capability (CONSTITUTION §5's "all integration tests pass") now belongs to **F-018**, and is built with Testcontainers rather than bare `WebApplicationFactory`. F-017 keeps the container/CD hardening and the §7 security-scan gate.

> **Roadmap drift repaired 2026-08-18.** F-014 through F-017 existed as feature records in `docs/pdlc/tasks/` since 2026-08-15 but were never added to this table, which stopped at F-013. They are now listed. Also note F-001–F-012 are marked `Shipped` but predate PDLC ship tracking — they have no episode files, no CHANGELOG entries and no tags; `v0.1.0` is the first PDLC-tracked release.
>
> ~~**F-018 is being worked ahead of F-014–F-017** at the user's explicit request — a structural refactor of every endpoint is cheaper before those four add more endpoints to it.~~
>
> **REVERSED 2026-08-18.** F-018 completed Inception (PRD, Design, 20 tasks — merged as PR #37) and then had Construction **aborted at the wave-1 standup, before a single line of code**, at the user's explicit request, so that F-014–F-017 could be delivered first.
>
> ### Platform Remediation program — decomposed at Discover 2026-08-18
>
> Rather than starting Inception on one of the four, a **single program-level Discover** was run across F-014–F-017 (the same move that decomposed `refactor-minimal-apis` into F-018/F-019/F-020 earlier the same day). Log: [`brainstorm_platform-remediation_2026-08-18.md`](../brainstorm/brainstorm_platform-remediation_2026-08-18.md).
>
> **Every premise was verified against the code first**, because this project has had two Discover premises collapse on inspection (the MAUI-workload concern and the OTLP-suppression inference were both withdrawn as wrong). All four held. Two were materially under-scoped, and **ten catalogued defects belonged to no feature at all**.
>
> **Resulting sequence — F-016 → F-021 → F-014 → F-015 → F-017 → F-018–F-020**, with F-022–F-024 filed for later. The Priority column reflects this order; feature IDs are unchanged.
>
> Why this order:
> 1. **F-016 first** because the live PII exposure is the highest-severity item, and because authn + pagination are **breaking contract changes that currently have zero consumers** — the mobile client cannot reach those routes at all (`01-api-surface.md:158`). Changing the contract now costs nothing; changing it after F-015 means rewriting the mobile client twice.
> 2. **F-016 carries the harness** because `11-testing.md:148` establishes that `Program.cs` is not coverable and **no integration test exists in the solution** — so endpoint authz, the exact thing F-016 changes, is the one thing nothing here can verify. The Calendar IDOR exists *because* of that gap: 24 tests cover the 26-line `OwnershipGuard` class while nothing checks whether an endpoint calls it. Building F-016 on unit tests alone would reproduce the conditions that created the bug.
> 3. **F-014 moved after F-016** because it adds six new route families — including `NoteService` (therapy/coaching session notes, the most sensitive data in the product) and `PaymentService` — onto a substrate where `AssertRole` is never called and a forgotten `try/catch` silently returns **500 instead of 403**. Building first and retrofitting authz means shipping new exposure.
>
> **The aborted F-018 Inception is not wasted** — its approved plan becomes F-016's wave 1.

---

## Status Key

- **Planned** — Not yet started
- **In Progress** — Currently in brainstorm, build, or ship
- **Shipped** — Completed and deployed (date + episode link filled in)
- **Deferred** — Deprioritized or postponed
- **Dropped** — Removed from roadmap (reason noted)
