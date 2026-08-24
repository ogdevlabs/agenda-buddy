---
feature: api-gateway-and-mobile-contract
date: 2026-08-23
status: inception-complete
last-updated: 2026-08-23T17:30:00Z
approved-by: ogdevlabs
approved-date: 2026-08-23T16:00:00Z
prd: docs/pdlc/prds/PRD_F-015_api-gateway-and-mobile-contract_2026-08-23.md
---

# Brainstorm Log: API Gateway and Mobile Contract

## Divergent Ideation
_Not run._

## Socratic Discovery

**Completed:** 2026-08-23T14:30:00Z
**Interaction mode:** Sketch

### Round 1 — Problem Statement

**Q1:** What problem does this specific feature solve? (Be concrete — what is the user unable to do today, and what is the cost of that gap?)
**A:** The mobile client (`MobileApp`, F-012) cannot reach any backend service in production. Three compounding faults: (1) no API gateway/reverse proxy exists, so a single `BaseAddress` cannot address seven dynamically-ported services (`01-api-surface.md:51`, `09-integrations.md:155`); (2) both configured base URLs are dead code — neither `appsettings.json` nor `appsettings.Development.json` is ever loaded, so both HTTP clients always fall back to the hardcoded `http://localhost:6036/` (Identity, plaintext) (`16-mobile-client.md`, verified 2026-08-22); (3) every domain route path the client calls omits `api/v1/` and uses wrong resource names/verbs (`GET booking?date=` has no matching route at all). Only login/register/device-token work, and only by coincidence — they happen to sit on the fallback port and happen to carry the right prefix. Cost: F-012 reads as Shipped while every other screen silently renders fabricated `SeedDataProvider` data — fictitious client names, phone numbers, session notes — which also masks two real UX bugs (the error banner and empty-state UI can structurally never fire). Source: `docs/pdlc/context/16-mobile-client.md`, `01-api-surface.md:178-180`.

**Q2:** Who specifically will use this feature — and in what context?
**A:** Not a new persona — this unblocks the two existing INTENT.md personas (Independent Service Provider, and their Customers) from using the mobile app they already have installed. Both currently see fabricated seed data regardless of role. **Flagged discrepancy:** `INTENT.md` still lists "mobile app — out of scope, future phase" and "no authentication layer exists yet" under Out of Scope / Key Constraints — both are stale (F-012 shipped the mobile app, F-001 shipped auth). Carried forward as a documentation fix, not blocking this feature.

**Q3:** What does success look like for this feature? What specific metric moves, and by how much?
**A:** Harder metric than "feels reachable": **0 of MobileApp's real domain calls hit the `SeedDataProvider` fallback path when tested against a live AppHost.** Today, booking, most of calendar, messaging, and notifications all fail this — only auth-adjacent calls succeed.

**Q4:** What are the technical constraints or dependencies for this feature?
**A:** Depends on **F-013** (Aspire assigns ports dynamically — no fixed `603x` port is addressable outside a standalone/Compose run, `01-api-surface.md:180`) and **F-014** (the client must speak its new contract: `revenueAvailable`/`revenueUnavailableReason`, empty-notifications-is-the-normal-state, non-charging payment semantics, and the new dedicated status-transition route). No gateway technology exists yet in the repo (no YARP, nginx, Envoy, or Ingress — Compose's `PATH_BASE` env vars anticipate one but nothing implements it, `09-integrations.md:155`). The MobileApp TFM that actually runs in CI (`net10.0` fallback) has **no DI registration and no Shell at all** (`#if MOBILE`), so every wiring defect this feature touches is structurally untestable today (`16-mobile-client.md`, `11-testing.md`).

### Round 2 — Future State / Key Capabilities

**Q1:** How should `MobileApp` address seven dynamically-ported services — (A) a real gateway process (e.g. YARP) in front of all seven, giving the client one base URL, or (B) no new process, with the AppHost passing each service's discovered address to the client for it to route itself?
**A:** **(A) Real gateway process.** Rationale: it's the only shape that survives a real (non-Aspire) deployment where the client cannot discover ports itself, and it becomes the natural home for F-017's future TLS termination and any cross-cutting concern (rate limiting, auth) that should live in front of all seven services rather than duplicated in each.

**Q2:** What happens to `SeedDataProvider` once real APIs work — it currently fires on both failure *and* genuine empty results, showing another user's fabricated PII?
**A:** **Remove the fallback entirely.** Real errors surface the already-built (but currently unreachable) error banner; real empty results surface the already-built (but currently unreachable) empty-state UI. No fabricated data shown, ever, once this feature ships.

**Q3:** The tested MobileApp TFM has no DI/Shell at all, so every wiring fix here is structurally untestable under CI — the same shape of gap that let the Calendar IDOR hide before F-016 built an integration harness. Fix in this feature, or file separately?
**A:** **Fix it in F-015.** The wiring itself (route paths, base URL resolution, gateway routing) must be covered by tests that run in CI — otherwise this exact defect class can silently return the next time a route changes, and nothing would catch it.

**Q4:** Confirming scope on the two named auth-flow gaps: the refresh token is stored but never called (hard logout at 60 minutes), and `LogoutAsync` never calls the server (refresh token stays valid 24h after logout). Both in scope?
**A:** **Yes, both in scope.** Wire transparent refresh-on-401 and a real server-side logout call.

### Round 3 — Acceptance Criteria

**Q1:** Primary acceptance bar — a provider and a customer using `MobileApp` against a live AppHost see their real dashboard, calendar, customers, messages, and notifications, with zero fabricated data ever (whether the old cause would have been failure or genuine emptiness). Agreed?
**A:** **Yes**, this is the primary bar.

**Q2:** The client's `PUT booking/{id}` call sends a partial `{status}` body against an endpoint that now (per F-014) ignores the status field entirely and expects a full `AppointmentEntity`. F-014 already built a dedicated `POST .../status` route for exactly this. Fix the client to call the new route instead?
**A:** **Yes.** Replace the `PUT`-based status update with the dedicated transition route. This also means the customer-facing UI must hide "mark complete" — it is provider-only per F-014's contract, and would otherwise always be refused with 403.

**Q3:** Verification bar for the auth-flow fixes — live end-to-end against a running AppHost (register → login → use the app past a simulated near-expiry → transparent refresh → logout → confirm the old refresh token is rejected), matching the live-verification standard F-014/F-016/F-021 used at their Ship gates?
**A:** **Yes, live end-to-end.** Matches the project's established pattern that real defects are found by running the software, not by reviewing it.

## Progressive Thinking (Agent Team Meeting)

**MOM:** [api-gateway-and-mobile-contract_progressive-thinking_mom_2026_08_23.md](../mom/api-gateway-and-mobile-contract_progressive-thinking_mom_2026_08_23.md)
**Facilitation:** solo — one model reasoning as each role, no agents spawned (consistent with F-014/F-016/F-021's sessions).

### Confirmed Facts
No gateway/reverse proxy exists anywhere in the repo. Both `MobileApp` config files are dead code — the app always uses the hardcoded fallback `http://localhost:6036/` over plaintext HTTP. Domain route paths are wrong (missing `api/v1/`, wrong verbs, mismatched payload shapes). `SeedDataProvider` fires on both failure and genuine emptiness, structurally hiding the already-built error banner and empty-state UI. The refresh token is never sent; `LogoutAsync` never calls the server. The tested `net10.0` TFM has no DI/Shell (`#if MOBILE`), so every wiring defect here is untestable today. F-014 shipped a contract (revenue-reason, empty-notifications-normal, non-charging payments, dedicated status route) this client must speak.

### Accepted Inferences
The gateway is a new AppHost resource (8th project), wired the same way `AppHostWiring.cs` already composes Mongo/Kafka. YARP is the lowest-friction technology choice (first-party, .NET-native, minimal new dependency footprint). Testability fix means extracting `*ApiService` HTTP logic behind interfaces constructible without the Maui bootstrap. The gateway becomes F-017's future TLS-termination point. OpenAPI regeneration belongs in this feature since F-015 is the first consumer of F-014's nine new routes.

### Key Consequences
New gateway project + AppHost resource + CI build target. Every `*ApiService`'s routes corrected; the `PUT`-based status call replaced by F-014's dedicated route (and "mark complete" hidden from customers). Two new test layers: gateway route-forwarding, and MobileApp wiring under a testable DI graph. The gateway must forward JWTs unmodified — becoming an auth bypass would be worse than today. The built error/empty-state UI becomes reachable for free once the seed fallback is removed.

### Risks & Unknowns
The gateway must not be mistaken for a TLS fix — it will still proxy plaintext HTTP; F-017 owns TLS termination. YARP's destination-address caching against Aspire's dynamic ports needs a spike before Design commits. The client's own discovery of the gateway's address needs a `MongoConnectionResolver`-shaped resolution story, not a new hardcoded fallback repeating today's defect. UX-contract copy (revenue-unavailable reason text, non-charging-payment language) needs an explicit Define-level requirement rather than being left implicit.

### Conflicts Resolved
One tension, resolved without user escalation: the gateway could be mistaken for a security improvement (Phantom's concern) — resolved by scoping TLS termination explicitly out of F-015 and into F-017, stated in the PRD.

### Design Priorities
1. Gateway route-forwarding + auth passthrough, correct and tested (highest risk).
2. Every domain route path/verb/payload shape corrected on the client.
3. MobileApp wiring made testable under CI.
4. `SeedDataProvider` removed; existing error/empty-state UI takes over.
5. Refresh-on-401 + server-side logout, verified live end-to-end.
6. OpenAPI specs regenerated.

## Adversarial Review

**Completed:** 2026-08-23T15:00:00Z

### Findings

1. **Assumption gap.** Fixing route paths + adding a gateway is assumed sufficient for "reachability," but two incompatible error envelope shapes coexist in the backend (RFC 7807 from six domain services, ad-hoc `{error, message}` from Identity), and `MobileApp` parses neither today — it only checks `IsSuccessStatusCode` and discards every body (`10-error-handling.md:208`). Correct paths alone still leave error messages unreachable.
2. **Scope leak.** No retry, circuit breaker, or timeout policy exists on any outbound call anywhere in this system (`09-integrations.md`). A bare reverse-proxy gateway adds a hop with no resilience policy of its own — a slow/down service could now hang the client worse than a direct (if wrong) connection does today.
3. **Success metric fragility.** "0 domain calls hit `SeedDataProvider`" becomes trivially true the moment the fallback code is deleted — it doesn't prove reachability was *fixed*, only that the masking was *removed*. Needs a positive companion metric: e.g., X% of domain calls return 2xx against a live AppHost, asserted by a real test suite.
4. **Technical risk blindspot.** Nobody confirmed whether YARP's reverse-proxy config can track Aspire's dynamically-reassigned destination ports without going stale across an AppHost restart, or whether it needs the same service-discovery env vars .NET's `HttpClient` already reads.
5. **User problem validity.** The "problem" is 100% code-inspection-derived — no real provider or customer has ever used the shipped mobile app against live data, since it has never worked. Worth stating in the PRD rather than implying validated field pain.
6. **Dependency blindspot.** iOS's `Info.plist` carries an ATS exception scoped to insecure loads at literal `localhost` (`13-security.md:267`). If the gateway's address in any real run isn't literally `localhost` (a LAN IP, a simulator host, a deployed hostname), iOS blocks the connection outright — a platform-specific dependency nobody has named.
7. **Edge case silence.** What happens to an active session when the AppHost restarts mid-use and Aspire reassigns ports? Does the gateway have a stable-enough identity to retry against, or does a restart force an app relaunch?
8. **Requirement conflict.** CONSTITUTION §9: "New packages require discussion before adding — keep the dependency footprint minimal." Adding YARP plus an entirely new deployable is a nontrivial addition that was decided in Round 2 without being weighed explicitly against that constraint.
9. **Definition-of-done gap.** "MobileApp wiring made testable under CI" (Round 2, accepted) has no falsifiable acceptance criterion yet — "testable" could mean one new unit test or full route-path assertions across every `*ApiService`.
10. **Timeline/sizing naivety.** This feature bundles four independently risky work-streams (new gateway service, route/payload corrections across 5+ services, a testability refactor of the DI/Shell split, refresh+logout wiring with live verification) — every prior shipped feature here was scoped tighter than this.
11. **Edge case silence.** F-021's refresh-token semantics are single-use (`FindOneAndUpdateAsync` keyed on the presented hash) — a second device or a race within the same device gets one success and one rejected replay. Undiscussed: does the client handle "my refresh was already consumed elsewhere" gracefully, or does it assume exactly one active session?
12. **Requirement conflict / scope leak.** F-014's `ux-review.md` named four client obligations for F-015; only obligation #4 (status route) became an explicit Discover decision. Obligations #1–3 (revenue-unavailable copy, empty-notifications-is-normal, non-charging-payment language) were acknowledged but risked being silently dropped from the PRD.

### Follow-up Q&A

**Q:** F-015 bundles four independently risky work-streams. Split into multiple features, or keep one PRD with wave-level splitting at Plan?
**A:** **One PRD, split into waves at Plan.** The four pieces are tightly coupled — testability has to exist to prove the route fixes are real; refresh/logout needs the gateway live to test. Flagged for extra scrutiny at Plan's readiness party rather than splitting now.

**Q:** Should Design spike YARP's compatibility with Aspire's dynamic ports before committing to the architecture?
**A:** **Yes, spike it first** — same discipline F-018 used for its two gating risks before Design committed.

**Q:** What should Define do with F-014's other three client obligations (revenue-unavailable copy, empty-notifications-is-normal, non-charging-payment language)?
**A:** **Write functional requirements + placeholder copy now.** The PRD states what must be conveyed (e.g. "client must render `revenueUnavailableReason`, never a number or a blank field") with example wording; final copy is deferred to Design/Muse.

**Remaining findings (2, 3, 5, 6, 7, 8, 9, 11)** stay visible above and feed Define's requirements/known-risks and Design's threat model without a dedicated follow-up.

## Edge Case Analysis

**Completed:** 2026-08-23T15:20:00Z

### Findings

| # | Category | Scenario | Trigger Condition | Addressed? | Risk if Unhandled |
|---|----------|----------|------------------|------------|-------------------|
| 1 | Concurrency and timing | Two devices, same user; one refreshes (single-use token per F-021), the other's next refresh is rejected | Same account logged in on 2+ devices | No | Second device is force-logged-out for doing nothing wrong — looks like a bug |
| 2 | Integration failure modes | One backend service is down while the gateway and the other six are up | A service crashes/restarts while the AppHost keeps running | No | Gateway could turn a single-service outage into an opaque all-service failure |
| 3 | Migration and transition states | AppHost restart reassigns dynamic ports mid-session | Developer restarts the AppHost while the app is open | Partial (flagged in Adversarial #7, no decision made) | Active session errors ambiguously instead of showing a clear reconnecting state |
| 4 | Partial completion and rollback | Gateway-hop times out **after** the backend already wrote the data (e.g. mid-`POST`) | Slow network/hop during a write | No | Client retries and creates a duplicate booking/note/payment |
| 5 | Invalid and malformed inputs | Gateway receives a request for a route with no backend mapping | Stale client build, or a route typo | No | Gateway swallows it or returns a confusing generic error instead of a clear 404 |
| 6 | User flow branches | Access token expires mid-multi-step flow; refresh succeeds | User mid-flow (e.g. mid-booking) when the 60-minute token boundary is crossed | Partial (refresh decided; retry-original-request behavior unspecified) | User loses in-progress work on a technically-successful refresh |

### Triage Decisions

| # | Decision | Notes |
|---|----------|-------|
| 1 | Known risk | Recorded rather than fixed — a second-device rejection reads as a bug but is F-021's correct single-use semantics working as designed. Deferred; no PRD AC |
| 2 | In scope | Gateway must return a distinct error identifying which backend service failed, not a generic gateway-error shape — cheap given YARP already exposes per-destination health |
| 3 | Known risk | Recorded as a known limitation of the local-dev AppHost restart path; not an AC for this feature |
| 4 | In scope | Client must **never auto-retry** a non-idempotent write on an ambiguous (post-write-possible) timeout — surface "unknown result, check before retrying" instead. Idempotency keys (the stronger fix) are out of scope — they touch every write endpoint across three services, not just the mobile client — and are filed as a follow-up |
| 5 | In scope | Gateway returns a clear 404 for an unmapped route, not a swallowed or generic error |
| 6 | In scope | Placeholder AC: the original request is **not** silently lost — after a successful transparent refresh, the client either auto-retries the original request once or surfaces its exact prior state so the user can resubmit without re-entering data. Exact mechanism left to Design |

## External Context
_None ingested._

## Discovery Summary

**Feature:** api-gateway-and-mobile-contract (F-015) — make the mobile client (F-012) actually reach the backend.

**Problem:** Three compounding faults. No API gateway/reverse proxy exists for seven dynamically-ported
services. Both of `MobileApp`'s configured base URLs are dead code — the app always falls back to a
hardcoded plaintext Identity URL. Every domain route path, verb, and payload shape the client sends is
wrong. Only login/register/device-token work, and only by coincidence. Every other screen silently renders
fabricated `SeedDataProvider` data.

**User:** the two existing personas from `INTENT.md` (Independent Service Provider, and their Customers) —
not a new user group, just unblocking the app they already have installed.

**Success metric:** 0 of MobileApp's real domain calls hit the `SeedDataProvider` fallback when tested
against a live AppHost. Companion positive metric: X% of domain calls return 2xx against a live AppHost,
asserted by a real test suite — the negative metric alone is satisfiable by deletion, not by fixing
reachability.

**Technical constraints:**
- Depends on F-013 (Aspire's dynamic ports — no fixed port survives outside a standalone/Compose run) and
  F-014 (must speak its new contract: `revenueAvailable`/reason, empty-notifications-is-normal, non-charging
  payment semantics, the dedicated status-transition route).
- No gateway technology exists yet; chosen approach is a real YARP reverse-proxy process as a new AppHost
  resource — **spiked against Aspire's dynamic destination-port reassignment before Design commits**, the
  same discipline F-018 used for its two gating risks.
- The tested MobileApp TFM (`net10.0` fallback) has no DI/Shell at all (`#if MOBILE`) — fixing this
  testability gap is in scope for this feature, not filed separately.
- `SeedDataProvider` is removed entirely; the already-built (but currently unreachable) error banner and
  empty-state UI take over.
- Refresh-on-401 and server-side logout are wired and **verified live end-to-end** against a running
  AppHost, matching F-014/F-016/F-021's Ship-gate standard.

**Out of scope:**
- TLS termination — owned by F-017; the gateway must not be mistaken for a security fix, since it still
  proxies plaintext HTTP.
- Idempotency keys for safe write-retries — the stronger fix for gateway-hop timeouts after a write, but
  touches every write endpoint across three services, not just the mobile client. Filed as a follow-up.
- OpenAPI spec regeneration is **in scope** despite being mechanical — F-015 is the first feature to actually
  read F-014's nine new routes.

**Key risks / assumptions:**
- The gateway must return a distinct error identifying which backend service failed (vs. a generic
  gateway-unreachable error), so the client can tell "Booking is down" from "you have no internet."
- Two incompatible backend error envelope shapes coexist (RFC 7807 vs. Identity's ad-hoc `{error, message}`)
  and `MobileApp` parses neither today — correcting routes alone does not make error messages reachable.
- Multi-device refresh-token conflicts (F-021's single-use semantics) and AppHost-restart port reassignment
  mid-session are recorded as **known risks**, not acceptance criteria.
- The client must never auto-retry a non-idempotent write after an ambiguous (post-write-possible) timeout —
  surface "unknown result, check before retrying" instead of silently duplicating a booking/note/payment.
- The problem itself is 100% code-inspection-derived — no real provider or customer has ever used the shipped
  mobile app against live data, since it has never worked. Stated explicitly rather than implied as
  validated field pain.
- This feature bundles four independently risky work-streams (gateway, route/payload corrections,
  testability refactor, auth-flow wiring) — larger than any prior shipped feature here. Kept as one PRD,
  split into waves at Plan, flagged for extra scrutiny at the Plan readiness party.

**Confirmed by:** ogdevlabs, 2026-08-23.

## Design Discovery (Bloom's Taxonomy)

### Round 1 — Mechanics

**Q1:** Confirm the request-path shape — today: MobileApp → hardcoded `http://localhost:6036/` (Identity, wrong) → 404 → `SeedDataProvider`. After F-015: MobileApp → gateway (one discovered address) → gateway resolves the "booking" destination → Booking service → real data.
**A:** Confirmed.

**Q2:** How does the client learn the gateway's own address? `run-ios.sh` already discovers each service's dynamic port by probing `/alive`, but only prints it for a human today.
**A:** **Extend `run-ios.sh`'s existing discovery** to also find the gateway's port, and inject it via an environment variable `MauiProgram.cs` reads (`MAUI_API_BASE_URL`) for standalone/simulator runs — reusing an existing mechanism rather than inventing a new one.

**Q3:** When a specific backend service crashes mid-request, YARP returns a 502. Should the gateway attach the failed destination's name to the error before returning it (so the client just displays it), rather than the client guessing from the route it called?
**A:** **Confirmed** — gateway-side translation.

### Round 2 — Apply

**Q1:** Should the gateway be a thin Minimal API `Program.cs` calling `AddReverseProxy().LoadFromConfig(...)`, matching CONSTITUTION §9's minimal-footprint rule and the one-`Program.cs`-per-service pattern?
**A:** **Yes.**

**Q2:** Should the gateway's route-to-destination mapping be built programmatically from the same Aspire service-discovery config keys `AddServiceDefaults()` already wires into every other service, rather than a separate static YARP cluster file?
**A:** **Yes** — reuses the AppHost's own mechanism rather than a config that could drift from it.

**Q3:** Should the gateway also call `AddServiceDefaults()` for consistent health checks / telemetry / resilience?
**A:** **Yes.**

### Round 3 — Trade-offs and Judgments

**Q1:** Gateway-side or client-side translation of a downstream 502 into a named-service error (AC 5)?
**A:** **Gateway-side** — the mapping lives in exactly one place.

**Q2:** Read destination addresses live via Aspire service discovery per request (survives a mid-session AppHost restart, costs a per-request lookup), or YARP's default static config resolved once at startup?
**A:** **Live per request, pending the spike's measurement** — this is the thing Design must actually confirm before committing.

**Q3:** Extract each `*ApiService`'s route-building logic into a plain, Maui-free class (unit-testable under the CI-run TFM), or build a fake `IServiceProvider` to construct the `#if MOBILE`-gated types?
**A:** **Extract into plain classes** — the same "narrow the untestable surface" approach F-016 used splitting `DockerPreflight`'s probe from its diagnose logic.

### Synthesis

**Neo's sketch:** An 8th AppHost resource (`Gateway`), a thin ASP.NET Core Minimal API project using YARP, calling `AddServiceDefaults()` and `AddReverseProxy()`, with route/cluster config built programmatically from Aspire service-discovery keys, resolved live per request pending the spike. `AppHostWiring.cs` gets a `WithReference`/`WaitFor` edge to all seven services. The gateway's address is discovered by extending `run-ios.sh` and injected as `MAUI_API_BASE_URL`. No new data model — the gateway is stateless routing. No new API endpoints — it fronts the existing surface unchanged in shape; every `*ApiService`'s routes are corrected and repointed at the gateway; the status-update call switches to F-014's dedicated route. The gateway attaches failed-destination names to error responses; `*ApiService` route-building logic is extracted into plain, testable classes.

**User validation:** Matches — proceed to writing the formal design documents.

## Threat Modeling Triage
- Trust boundary changes: yes — new gateway process between mobile client and all 7 services
- Regulated data: yes — session notes, payments, appointment PII flow through the gateway (passthrough)
- New attack surface: yes — new mobile-facing HTTP handler exposing every domain route in one place
- Triage tier: **Full** (solo session — see `docs/pdlc/design/api-gateway-and-mobile-contract/threat-model.md`)

## Design-Laws Audit Triage
- UI surface: yes — hides an existing control, changes copy on 3 screens, makes existing error/empty states reachable
- New flow / pattern: no
- First-experience pathway: no
- Triage tier: **Lite** (solo — see `docs/pdlc/design/api-gateway-and-mobile-contract/ux-review.md`)

## Readiness Party Triage
- Task count: 14
- Waves: 5
- Domains: backend, frontend, devops, security, ux
- Unresolved MUST requirements: no
- Triage tier: **Full** (solo — overall **Fair**, 1 open gap: `estimate-mis-scoped` on Wave 3's T07/T09 parallelism claim. See PRD `## Readiness Assessment`.)
