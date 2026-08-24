# PRD: API Gateway and Mobile Contract

**Date:** 2026-08-23
**Status:** Approved
**Feature slug:** `api-gateway-and-mobile-contract`
**Feature ID:** F-015
**Episode:** *(assigned after delivery)*

---

## Overview

`MobileApp` (F-012) is the only client of Agenda Buddy's seven backend services, and it cannot reach any of
them. INTENT.md's launch criteria name "provider can book a first appointment in under 2 minutes" and "all
core CRUD operations covered" — neither is true on the shipped mobile client, because nothing it calls
resolves against the real backend. F-015 gives the client a single address to call (a gateway in front of
all seven services), corrects every route it calls, and removes the fabricated seed data that has been
masking the gap since F-012 shipped.

**Claim: a provider or customer using MobileApp against a live AppHost sees their real data, on every
screen, with zero fabricated fallback — ever.**

---

## Problem Statement

**1. No client can address seven dynamically-ported services.** F-013 made the Aspire AppHost assign every
service's port dynamically; there is no fixed port left to be right about, and no API gateway or reverse
proxy exists anywhere in the repo (`01-api-surface.md:51`, `09-integrations.md:155`).

**2. Every configured base URL is dead code.** `MauiApp.CreateBuilder()` registers no configuration source —
neither `appsettings.json` nor `appsettings.Development.json` is ever loaded. Both of `MobileApp`'s named
`HttpClient`s always fall back to the hardcoded `http://localhost:6036/` (`MauiProgram.cs:32,38`) — Identity,
over plaintext HTTP.

**3. Every domain route path, verb, and payload shape the client sends is wrong.** `GET booking?date=` has
no matching backend route at all. `PUT booking/{id}` sends `{"status": "Confirmed"}` against an endpoint
that, after F-014, ignores the status field entirely and expects a full `AppointmentEntity`. Only
login/register/device-token work, and only because they happen to sit on the hardcoded fallback port and
happen to already carry the `api/v1/` prefix.

**4. The gap is invisible because `SeedDataProvider` masks it.** `DashboardViewModel` and `CalendarViewModel`
substitute fabricated appointments — including fictitious client names and phone numbers — whenever a real
call fails **or returns zero results**. This is a correctness and privacy-perception problem independent of
the routing bug, and it structurally prevents two already-built UX fixes from ever firing: the error banner
(`HasError` is hardcoded to always be `false`) and the empty-state UI (`IsEmpty` can never be `true`, because
a zero-length result is replaced before it is assigned).

**5. The refresh-token flow is half-wired.** `AuthService` stores and clears a refresh token but never calls
`POST api/v1/auth/refresh` — the 60-minute access-token lifetime becomes a hard logout. `LogoutAsync` clears
only local storage, so a logged-out session's refresh token stays valid on the server for its full 24-hour
lifetime.

**6. The client's own contract with F-014 is unaddressed.** F-014 shipped nine new routes and four explicit
client obligations (`ux-review.md`): a "why revenue is unavailable" reason to render instead of a number,
an empty notifications list that is the normal state rather than an error, non-charging payment language,
and a dedicated status-transition route that must replace the client's current `PUT`-based call.

---

## Target User

The two existing personas from `INTENT.md` — the **Independent Service Provider** and their **Customers**.
This is not a new user group; it is the fix that lets both use the mobile app they already have installed.
Today, both see fabricated seed data regardless of role, because no real call succeeds.

**Note:** `INTENT.md`'s Out of Scope and Key Constraints sections still read "mobile app — future phase" and
"no authentication layer exists yet." Both are stale (F-012 and F-001 shipped). Flagged for a documentation
correction; not a blocker for this feature.

---

## Requirements

1. The system MUST route all seven backend services through a single gateway address, which `MobileApp`
   uses as its only configured base URL.
2. The gateway MUST forward the caller's JWT unmodified to the destination service. It MUST NOT re-validate,
   strip, or terminate authentication itself.
3. The gateway MUST return a distinct error identifying which backend service failed when that specific
   service is unreachable, times out, or errors — as opposed to the gateway itself being unreachable.
4. The gateway's routing to each backend service MUST remain correct across an AppHost restart that
   reassigns dynamic ports — no stale destination-address caching.
5. Every `MobileApp` `*ApiService` call MUST target the correct `api/v1/...` route, HTTP verb, and payload
   shape for its corresponding backend endpoint.
6. The client's appointment-status update MUST use F-014's dedicated `POST .../status` transition route
   instead of the legacy `PUT` call, and the customer-facing UI MUST NOT offer a "mark complete" action
   (provider-only, per F-014).
7. `SeedDataProvider` and every fallback call to it MUST be removed from every ViewModel that currently uses
   it.
8. A failed API response MUST surface the existing error banner (with retry); a genuine zero-result response
   MUST surface the existing empty-state UI. Neither may be intercepted by fabricated data.
9. The client MUST attempt a transparent token refresh on a `401` before treating the session as expired,
   and MUST NOT auto-retry a non-idempotent write whose prior attempt may have already succeeded.
10. `LogoutAsync` MUST call the server-side logout endpoint (invalidating the refresh token), in addition to
    clearing local storage.
11. The MobileApp API-service layer SHOULD be restructured so its route and base-URL wiring is exercised by
    tests that run under the CI-executed test project, not only the `#if MOBILE`-gated Maui bootstrap.
12. The client MUST render `revenueUnavailableReason` (never a number or a blank field) when
    `revenueAvailable` is `false`; MUST treat an empty notifications list as a normal state, not an error;
    and MUST NOT imply that a `local_`-prefixed payment has been charged or settled.
13. `docs/api/openapi/` SHOULD be regenerated to reflect F-014's nine new routes before or alongside this
    feature's client-side changes.

---

## Assumptions

- YARP can be configured to re-resolve destination addresses per request rather than caching a static
  snapshot — to be confirmed by Design's spike before the architecture is committed.
- The gateway's own address can be resolved by the mobile client the same way `MongoConnectionResolver`
  already resolves Mongo's connection string for the backend (Aspire → environment → config, with an
  actionable failure message) — not by a new hardcoded fallback repeating today's defect.
- No real provider or customer has ever used the shipped mobile app against live data (Discovery, Adversarial
  finding #5) — there is no existing field usage or installed-base migration to protect.
- F-014's nine routes and response contracts are stable for the duration of this feature. If F-025 or a
  later feature changes them, this feature's client-side code changes too.
- iOS's `Info.plist` ATS exception (currently scoped to literal `localhost`) can be widened to whatever host
  the gateway resolves to at runtime via a build-configuration change, without requiring an App Store review
  cycle mid-feature.

---

## Acceptance Criteria

1. A provider and a customer using `MobileApp` against a live AppHost see their real dashboard, calendar,
   customers, messages, and notifications — zero fabricated `SeedDataProvider` data, whether the prior cause
   would have been failure or genuine emptiness. 🧪 test-first
2. Every `MobileApp` `*ApiService` call resolves against its corresponding backend route with a 2xx or a
   correctly-typed error response — not a 404 caused by a wrong path, verb, or prefix — verified against a
   live AppHost. 🧪 test-first
3. The gateway forwards a request carrying a valid JWT to the correct backend service, and the backend
   receives and validates that JWT unmodified (same claims, same signature). 🧪 test-first
4. An anonymous or invalid-JWT request through the gateway receives the same 401/403 the backend would
   return directly — the gateway does not weaken or bypass authorization. 🧪 test-first
5. When one specific backend service is stopped while the gateway and the other six remain up, a request to
   that service returns an error identifying the failed service, and requests to the other six succeed
   normally. 🧪 test-first
6. The gateway continues routing correctly to a service after the AppHost reassigns that service's dynamic
   port (e.g. following a restart), without the gateway process itself needing to restart. 🧪 test-first
7. Updating an appointment's status from `MobileApp` calls F-014's `POST .../status` route; the
   customer-facing UI never presents a "mark complete" control. 🧪 test-first
8. A network or server failure surfaces the existing error banner with a retry action; a genuine zero-result
   list surfaces the existing empty-state UI. `SeedDataProvider` is unreachable from any ViewModel. 🧪 test-first
9. When an access token expires mid-session, the next request triggers a transparent refresh, and either the
   original request completes without the user re-entering data, or the user is shown their exact prior
   input to resubmit — the session is not silently dropped to the login screen while a valid refresh token
   exists. 🧪 test-first
10. A non-idempotent write (e.g. creating a note or a payment) that times out at the gateway hop, after the
    backend may have already written it, is never silently auto-retried by the client. 🧪 test-first
11. Calling logout invokes the server-side logout endpoint; the previously-valid refresh token is rejected on
    a subsequent refresh attempt. 🧪 test-first
12. MobileApp's route-path and base-URL resolution logic is covered by tests that execute under the
    CI-run test project, not only the `#if MOBILE`-gated Maui bootstrap. 🧪 test-first
13. The report screen renders `revenueUnavailableReason` when `revenueAvailable` is `false` (never a number
    or a blank field); the notifications screen renders an empty state (not an error) on a genuinely empty
    list; the payment screen's copy does not claim a `local_`-prefixed payment has been charged. 🧪 test-first

**Threat-derived security ACs, added post-Define at the Design threat-modeling gate (Step 10.5/14.5, issue #55):**

14. `[security]` (T-302) Given a request outside every configured `api/v1/{service}/**` prefix (e.g. a probe
    at a backend's bare `/health` routed through the gateway), when the gateway receives it, then it responds
    404 (`gateway-no-route` shape) — not a proxied response. 🧪 test-first
15. `[security]` (T-303) Given a request routed through the gateway to any backend service, when that
    service's HSTS/redirect behavior is exercised, then it matches a direct call — no new redirect loop, no
    incorrect scheme in a `Location` header. 🧪 test-first

---

## User Stories

**F-015-US-01: A provider sees real dashboard data**
*Acceptance criteria: 1, 2, 8*
Given a provider has logged into MobileApp and has real appointments in the backend
When they open the dashboard against a live AppHost
Then they see their actual appointments, not fabricated seed data
And a genuine empty result shows the empty-state UI rather than seed data

**F-015-US-02: The gateway forwards authenticated requests correctly and isolates failures**
*Acceptance criteria: 3, 4, 5, 6*
Given the gateway is running in front of all seven services
When an authenticated client calls a route that maps to a healthy backend service
Then the request reaches that service with the caller's JWT intact and returns the same result the service would give directly
And when that specific service is down, the client sees an error naming which service failed rather than a generic outage

**F-015-US-03: Completing an appointment uses the dedicated status route**
*Acceptance criteria: 7*
Given a provider is viewing one of their own appointments
When they mark it complete
Then the client calls the dedicated status-transition route
And a customer viewing the same appointment never sees a "mark complete" option

**F-015-US-04: A session survives a mid-flow token expiry, and logout is real**
*Acceptance criteria: 9, 11*
Given a user's access token expires while they are mid-way through a multi-step action
When the next request 401s
Then the client transparently refreshes the token and either completes the original request or lets the user resubmit without re-entering their data
And when the user logs out, the server invalidates their refresh token so it cannot be reused afterward

**F-015-US-05: A write that times out ambiguously is never silently duplicated**
*Acceptance criteria: 10*
Given a client submits a write and the gateway hop times out after the backend may have already processed it
When the client detects the ambiguous failure
Then it does not automatically resubmit the write, and instead tells the user the result is unknown

**F-015-US-06: The report and notifications screens reflect F-014's honest-reporting contract**
*Acceptance criteria: 12, 13*
Given a provider's report has no computable revenue, or their notifications list is genuinely empty
When they view those screens
Then the report explains why revenue is unavailable instead of showing a number or a blank field
And the notifications screen shows a normal empty state rather than an error

---

## Testing Approach: Test-Driven Development (TDD)

**Tests are written first.** During Construction (`/build`), for **every acceptance criterion above**, a **failing test is written and run before any implementation code** — the Red → Green → Refactor cycle:

1. **Red** — write the smallest failing test that pins the acceptance criterion, named with the Given/When/Then language from the matching user story. Run it; confirm it fails for the right reason (logic not implemented — not a syntax/import error).
2. **Green** — write the minimum implementation that makes the test pass. Run the test and the full suite; no regressions.
3. **Refactor** — clean up without changing behavior; suite stays green.

The build loop enforces this at a mandatory **TDD gate** (build Step 9a-bis): implementation code for a criterion may not be written until a failing test for it exists. The only exceptions are pure scaffolding, config-only, and infrastructure-only work — and even those require an **explicit human TDD override**. There is no silent skip. (TDD can be disabled only by editing `CONSTITUTION.md` § Test Gates — the Constitution always wins.)

**Security acceptance criteria are enforced mechanically (issue #55).** Any `[security]`-tagged criterion above (threat-derived, materialized on its task via `tasks.cjs ac add`) is not just governed by the prose gate: `node scripts/tasks.cjs done` **structurally refuses** to close a task whose `[security]` AC has no linked test. Name each security test after its threat id (`test_TNNN_…`) and link it with `tasks.cjs ac link-test`. This makes it impossible to close a threat mitigation on a citation alone. ⚠️ **`scripts/tasks.cjs` does not exist in this repository** (unchanged since F-013) — the task store is hand-written, and each `[security]` AC names its test in the task body instead, the same fallback used by F-014 and F-021.

**Test layers** for this feature: **Unit** (required, §7) and **Integration** (not required by §7, but **required by this PRD** — AC 2–6 and 9–11 are only meaningful against a live gateway routing to a real backend, not a mock). **Mobile** (`MobileApp.Tests`, `/p:MobileWorkloads=false`) — the layer this feature directly extends, since AC 12 is exactly "make this layer able to see the wiring it currently cannot." **Security scan** (always required, §7).

---

## Non-Functional Requirements

- The gateway hop's latency overhead versus a direct call to a backend service MUST be measured (not
  assumed) once Design's YARP spike produces a working prototype, and the measurement recorded in the design
  docs — mirroring this project's established discipline of measuring rather than estimating (e.g. F-021's
  262ms BCrypt measurement).
- The gateway MUST NOT log any JWT, credential, or PII in its request/response handling — the same standard
  F-021 set for Identity's credential-mutation logging.
- The gateway MUST NOT be treated as satisfying any TLS/transport-security requirement. It proxies plaintext
  HTTP exactly as the backend does today; TLS termination remains F-017's scope, and any documentation this
  feature writes must say so explicitly.
- `MobileApp.csproj`'s existing dependency footprint (`MongoDB.Driver`, `Stripe.net`, `BCrypt.Net-Next`,
  pulled in transitively via `Library`) MUST NOT grow further as a result of this feature's changes.
- The gateway MUST be covered by CI (build, and the new tests AC 3–6 require) the same way the seven
  existing services are — a new project with no CI coverage would reintroduce exactly the kind of
  structurally-untestable gap this feature exists to close (AC 12).

---

## Out of Scope

- **TLS termination** for any backend service or the gateway — F-017's scope.
- **Client-generated idempotency keys** for provably-safe write retries — the stronger fix for AC 10's
  ambiguous-timeout problem, but it touches write endpoints across Booking, Customer, and Provider, not just
  the mobile client. Filed as a follow-up.
- **A UX treatment for multi-device refresh-token conflicts** — recorded as a known risk (below), not built
  here.
- **New mobile UI screens, layouts, or navigation.** This feature wires existing screens to real data; it
  does not redesign them. No UX Discovery ran (Skip triage — this is a wiring feature, not a UI-design
  feature, the same reasoning F-014 used).
- **Password reset, token revocation, data-subject rights** (F-022–F-024) — unrelated dependencies, untouched
  here.

---

## Known Risks

- **Multi-device refresh-token conflicts.** F-021's single-use refresh semantics mean a second device (or a
  race on the same device) gets one success and one rejected replay. This will surface as an apparent bug.
  Deferred — no PRD AC; needs its own UX decision about how to communicate it, in a future pass.
- **AppHost restarts reassigning dynamic ports mid-session** may still produce an ambiguous error rather than
  a clean "reconnecting" state in local development. Deferred as a local-dev-only limitation; not a
  production concern once a real deployment exists.
- **"Never auto-retry on ambiguous timeout" (AC 10) is a conservative mitigation, not a full fix.** The
  stronger fix (idempotency keys) is out of scope. A user who hits this will need to manually check whether
  their write succeeded before retrying.
- **Two incompatible backend error envelope shapes** (RFC 7807 from six domain services; Identity's ad-hoc
  `{error, message}`) both need correct client-side parsing. If Design finds this is larger than expected,
  it may need to become its own task rather than a detail inside another one.
- **This feature's scope (four independently risky work-streams) is larger than any prior shipped feature
  here.** Kept as one PRD per the Discovery decision, split into waves at Plan — but if Plan's readiness
  party finds it genuinely too large for one wave sequence, revisiting the split decision is still on the
  table.

---

## Standards Alignment

Define Step 6.5 (`--ideate`, advisory tier) was **skipped by user choice**, not because the plugin is
unavailable — `nordstrom-standards-readiness` is installed, but its six source standards repos have failed
to resolve under this machine's `gh` auth for **nine consecutive gates** across this project (an SSO/VPN
condition, not a wrong repo name — see the project's reference memory). Logged to STATE.md's Guardrail Log.
The enforcing `--design` gate at Plan will attempt the check again.

---

## Readiness Assessment

**Triage:** Full (14 tasks, 5 waves, multi-domain: backend/frontend/devops/security/ux) · **Date:** 2026-08-23
· **Overall:** Fair — 1 open gap

| Dimension | Rating | Evidence / gaps |
|---|---|---|
| Completeness | Strong | All 13 requirements have ≥1 AC (Requirements 1–13 → ACs 1–13); scope exclusions explicit (§Out of Scope); NFRs specified (§NFR, including the "measure, don't assume" latency requirement). |
| Traceability | Strong | All 15 ACs (13 + 2 threat-derived `[security]`) map to exactly one task each — AC1→T08, AC2→T07, AC3/AC4/AC5→T04, AC6→T12, AC7→T07, AC8→T08, AC9/AC10→T09, AC11→T10, AC12→T06, AC13→T11, AC14(T-302)→T03, AC15(T-303)→T04. **Named nuance, not a gap:** T01 (scaffold), T02 (spike), T05 (AppHost wiring), T13 (OpenAPI regen), and T14 (closing verification) own no numbered AC directly — each traces to prose in Requirements/ARCHITECTURE.md instead (infrastructure and closing tasks, not orphans; T14 explicitly attests the other tasks' ACs rather than owning new ones). *(AC3/AC4 corrected from T05→T04 during Build — see plan file's wave-order correction note.)* |
| Durability | **Fair** | Dependency graph is acyclic, 5 waves, all threat mitigate-now findings (T-302, T-303) became `[security]`-tagged tasks (T03, T04), all 4 UX fix-now findings became task-level detail (T07, T08, T11). **Adversarial re-check dropped this from Strong:** Wave 3 plans T07 and T09 as fully parallel — both modify `MobileApp`'s `Infrastructure`/`Services` layer (T07: `*ApiService` route corrections; T09: `AuthService`/`JwtDelegatingHandler` refresh wiring) with no formal dependency edge between them, but real file-adjacency risk. F-016's own wave standups repeatedly found a plan's "parallel" claim wrong once work started. **Gap category:** `estimate-mis-scoped` (wave-order risk, not task-sizing). |

**Open gaps (1):** `estimate-mis-scoped` — confirm at the Wave 3 standup whether T07 and T09 can genuinely run in parallel or need a coordination edge (e.g. T09 after T07, or an explicit file-ownership split) before both are claimed simultaneously.

**Recommendation:** advisory — resequence T07→T09 (or split file ownership explicitly) at the Wave 3 standup if working solo/sequentially makes the coordination risk moot; otherwise confirm with whoever claims T09 before starting. Human decides.

---

## Design Docs

- Architecture: [ARCHITECTURE.md](../design/api-gateway-and-mobile-contract/ARCHITECTURE.md)
- Data model: [data-model.md](../design/api-gateway-and-mobile-contract/data-model.md) — no data model changes
- API contracts: [api-contracts.md](../design/api-gateway-and-mobile-contract/api-contracts.md) — no new domain endpoints; the gateway's own contract and the corrected client route table
- Threat model: [threat-model.md](../design/api-gateway-and-mobile-contract/threat-model.md) — triage: **Full** (3/3); 3 threats (2 mitigate now, 1 accept)
- UX review: [ux-review.md](../design/api-gateway-and-mobile-contract/ux-review.md) — triage: **Lite** (1/3); 4 findings, all fix now
- Additional: `ARCHITECTURE.md` §6 — the YARP-vs-Aspire-dynamic-ports spike (gating risk, per Adversarial finding #4), to be run before Construction commits to the live-per-request resolution approach

---

## Related Episodes

- [Episode 004: wire-unreached-services](../episodes/EPISODE_wire-unreached-services_2026-08-23.md) — F-014
  shipped the nine routes and the four client obligations this feature must satisfy.
- [Episode 001: aspire-wiring](../episodes/EPISODE_aspire-wiring_2026-08-17.md) — F-013 introduced the dynamic
  port assignment this feature's gateway must track.

---

## Approval

**Approved by:** ogdevlabs
**Date approved:** 2026-08-23
**Notes:**
