# Threat Model — api-gateway-and-mobile-contract
<!-- pdlc-template-version: 1.0.0 -->

**Triage:** Full
**Convened:** 2026-08-23
**Lead:** Phantom (Security Reviewer)
**Participants:** Solo — one model reasoning as each role, no agents spawned (consistent with F-014/F-016/F-021's sessions; recorded as a fidelity caveat, not glossed).
**Status:** Pending human approval (Step 12)

---

## Triage Record

| Question | Answer | Evidence |
|---|---|---|
| Does this feature introduce or modify a trust boundary? | **yes** | A brand-new network-facing process (the gateway) sits between the mobile client and all seven backend services — `ARCHITECTURE.md` §2–3. |
| Does this feature touch regulated data (PII, payment, health, biometric, children's)? | **yes** | The gateway proxies every domain request, including session notes (`NoteService`), payments, and appointment data carrying customer PII — passthrough only, but it is a new egress path for that data. |
| Does this feature add a new attack surface (endpoint, event consumer, file upload, query interface, LLM tool, mobile handler)? | **yes** | The gateway is a new mobile-facing HTTP handler exposing every `api/v1/{service}/**` path in one place, where previously no client could reach any of them at all. |

**Triage outcome:** Full (3/3)

---

## Trust Boundaries

| ID | Boundary | What crosses | Trust direction | Diagram reference |
|---|---|---|---|---|
| TB-1 | Mobile client → Gateway | JWT (Bearer), request/response bodies (PII, payment, notes) | untrusted → semi-trusted | `ARCHITECTURE.md` §4 |
| TB-2 | Gateway → each of 7 backend services | Forwarded JWT (unmodified), forwarded body | semi-trusted → trusted | `ARCHITECTURE.md` §2–3 |
| TB-3 | Gateway → Aspire service discovery (`IConfiguration`) | Destination addresses (host:port), re-read live per request | trusted → trusted (internal) | `ARCHITECTURE.md` §6 (spike) |

---

## Threats Identified

### T-301 — The gateway is a new single point of failure for every mobile call

- **STRIDE category:** Denial of Service
- **Trust boundary:** TB-1, TB-2
- **Asset affected:** Availability of all seven backend services, from the mobile client's perspective
- **Attack vector:** Not attacker-driven — an operational risk introduced by the fix itself. Before this
  feature, no mobile call reached any backend, so no single component's failure could take down "all of
  them" for the client (there was nothing working to take down). After this feature, if the one gateway
  process is down, every backend service becomes unreachable to `MobileApp` even if all seven are
  individually healthy — a new aggregated failure mode that did not exist in the (broken) status quo.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M (client-wide outage, no data loss) · Reproducibility H (any gateway crash) · Exploitability L (not attacker-triggered under normal use) · Affected users: all mobile users · Discoverability H (immediately visible as "app is down")
- **Mapped frameworks:** OWASP API Top 10 — API4:2023 (Unrestricted Resource Consumption, adjacent); CWE-1088 (Synchronous Access of Remote Resource without Timeout, adjacent to the SPOF class)
- **Current mitigation status:** Partial — `WaitFor` on all seven services means the gateway only reports healthy once its destinations are, which prevents routing to a not-yet-ready backend, but does not address the gateway itself being a single instance.
- **Proposed action (party recommendation):** Accept, for this feature's scope
  - **If "Accept":** A single Aspire-run gateway instance matches how every other resource in this AppHost already runs (single instance, no replicas) — this feature does not regress local development, and no real (multi-replica, load-balanced) deployment exists yet to make this a production concern. Re-evaluation trigger: the first real (non-Aspire) deployment, which is F-017's scope and ADR-035's deferral.
- **Decision (human, at Step 12 approval):** *(pending)*
- **Cross-talk note:** Surfaced during Progressive Thinking (Discover) as a risk to spike, sharpened here into a named, severity-scored threat.

### T-302 — Overly broad route matching could expose an internal-only path

- **STRIDE category:** Elevation of Privilege
- **Trust boundary:** TB-1, TB-2
- **Asset affected:** Internal topology and any non-`api/v1` surface on the seven backend services (e.g. each service's own `/health`, `/alive`, or Development-only diagnostics)
- **Attack vector:** If the gateway's YARP route table is built with a catch-all forward (`/**` → destination) rather than an explicit `api/v1/{service}/**` allowlist per service, a mobile client — or anyone who discovers the gateway's address — could reach a backend's `/health`, `/alive`, or any future non-domain route that was never intended to be public, revealing internal state or topology it wasn't designed to expose to untrusted callers.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M (information disclosure, not data compromise) · Reproducibility H (a single crafted request) · Exploitability M (requires guessing/discovering the gateway's address, which is not secret) · Affected users: none directly; operational/topology exposure · Discoverability M
- **Mapped frameworks:** OWASP API Top 10 — API8:2023 (Security Misconfiguration); CWE-284 (Improper Access Control)
- **Current mitigation status:** Mitigated by design — `ARCHITECTURE.md` §2 specifies an explicit `api/v1/{service}/**` route table per service, not a catch-all forward.
- **Proposed action (party recommendation):** Mitigate now
  - **If "Mitigate now":** The route table must be an explicit allowlist (already the design); this becomes a task and a testable criterion rather than trusting the design doc alone.
    - **Testable acceptance criterion (required):** Given a request to a path outside every configured `api/v1/{service}/**` prefix (e.g. a probe at a backend's bare `/health` routed through the gateway's address), the gateway responds with the `gateway-no-route` 404 shape (`api-contracts.md` §1), not a proxied response. 🧪 test-first
- **Decision (human, at Step 12 approval):** *(pending)*
- **Cross-talk note:** —

### T-303 — Forwarded `Host` header could break a backend's transport-security ordering

- **STRIDE category:** Tampering (of the request's effective identity, not its payload)
- **Trust boundary:** TB-2
- **Asset affected:** Correctness of `UseAgendaBuddyTransportSecurity()`'s redirect logic on the seven backend services (`AgendaBuddy.ServiceDefaults/TransportSecurity.cs`)
- **Attack vector:** ASP.NET Core's HTTPS-redirect and HSTS middleware construct their redirect target from the
  request's `Host` header. If YARP forwards the gateway's own `Host` (or a mismatched one) instead of
  `X-Forwarded-Host`/`X-Forwarded-Proto`, a backend service behind the gateway could construct an incorrect
  redirect URL, or the interaction with the mandated `UseAgendaBuddyTransportSecurity()` → `UseAuthentication()`
  ordering (enforced by an existing `Library.Tests` source-text test) could behave differently when the
  request arrives via a proxy than when it arrives directly.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M (broken redirects, not a bypass) · Reproducibility H · Exploitability L (requires the gateway to already be misconfigured) · Affected users: any mobile user, as a functional break · Discoverability M
- **Mapped frameworks:** CWE-441 (Unintended Proxy or Intermediary); OWASP API Top 10 — API8:2023 (Security Misconfiguration)
- **Current mitigation status:** None yet — YARP's default `HttpTransformer` does forward `X-Forwarded-*` headers, but this project has not yet asserted that a backend's transport-security behavior is unaffected when reached through the gateway.
- **Proposed action (party recommendation):** Mitigate now
  - **If "Mitigate now":** Assert the interaction explicitly rather than assume YARP's default is sufficient.
    - **Testable acceptance criterion (required):** Given a request routed through the gateway to any backend service, that service's HSTS/redirect behavior (per its existing `TransportSecurityTest`-style coverage) is unchanged from a direct call — no new redirect loop, no incorrect scheme in a `Location` header. 🧪 test-first
- **Decision (human, at Step 12 approval):** *(pending)*
- **Cross-talk note:** Chained from T-302's route-table discussion — considering what crosses the gateway boundary unmodified surfaced this as the header-level analogue of the path-level concern.

---

## Threats Noted but Not Prioritized

| ID | Title | STRIDE | Boundary | Why deprioritized |
|---|---|---|---|---|
| T-NL-1 | No independent rate limiting at the gateway layer | Denial of Service | TB-1 | Not a regression — no service except Identity (F-021) has its own limiter today, and auth passthrough means F-021's limiter still applies unchanged at the destination. A future gateway-level limiter would be additive, not a fix to a new gap. |
| T-NL-2 | The gateway logs no caller identity for authorization failures | Repudiation | TB-1, TB-2 | Matches the standing, already-recorded project-wide gap (F-016/F-021: authorization failures are entirely unlogged). Not made worse by this feature; not fixed by it either. |
| T-NL-3 | PII in gateway telemetry spans | Information Disclosure | TB-1, TB-2 | Already mitigated by design — the gateway calls `AddServiceDefaults()` like the other seven services, which registers `PiiRedactingProcessor` automatically. No new code path bypasses it. Confirm with one assertion in the gateway's test suite that the existing redaction applies, rather than treating this as unaddressed. |

---

## Open Questions for Human

1. ~~**T-301's "accept" framing**~~ — **Resolved at Step 12.** Confirmed as-drafted; recorded as ADR-040. Re-score if ADR-035's cloud deferral changes before F-017 ships.
2. ~~**Scope check on T-302/T-303's acceptance criteria**~~ — **Resolved at Step 12.** Confirmed as-drafted — both testable against the gateway alone.

---

## Approval Outcomes (filled in at Step 12)

| Threat ID | Party recommendation | Human decision | Rationale |
|---|---|---|---|
| T-301 | Accept | Accept ✓ | Confirmed at Step 12 — single gateway instance matches every other resource's single-instance posture under the local Aspire AppHost; no real deployment exists yet (ADR-035). Re-score if that deferral changes. |
| T-302 | Mitigate now | Mitigate now ✓ | — |
| T-303 | Mitigate now | Mitigate now ✓ | — |

**ADR registry updates required:**
- ADR-040 — T-301 accepted (gateway single point of failure, local-dev scope)

**Tasks + security acceptance criteria to be created at Plan (Step 13):**

| Threat ID | Task | Testable `[security]` AC |
|---|---|---|
| T-302 | Build the gateway's route table as an explicit `api/v1/{service}/**` allowlist, not a catch-all forward | `[security] (T-302)` Given a request outside every configured prefix, the gateway responds 404 (`gateway-no-route`), not a proxied response. 🧪 test-first |
| T-303 | Assert backend transport-security behavior is unchanged when reached through the gateway | `[security] (T-303)` Given a request routed through the gateway, a backend's HSTS/redirect behavior matches a direct call. 🧪 test-first |

---

## Revision History

| Date | Author | Change |
|---|---|---|
| 2026-08-23 | Phantom (initial draft) | Created at Step 10.5, Full triage, solo session |
