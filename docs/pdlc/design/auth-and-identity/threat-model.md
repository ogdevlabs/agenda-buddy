# Threat Model — auth-and-identity
<!-- pdlc-template-version: 1.0.0 -->

**Triage:** Full
**Convened:** 2026-07-30
**Lead:** Phantom (Security Reviewer)
**Participants:** Phantom, Neo, Bolt, Echo, Pulse, Atlas, Muse, Jarvis, Friday
**Status:** Pending human approval (Step 12)

---

## Triage Record

| Question | Answer | Evidence |
|---|---|---|
| Does this feature introduce or modify a trust boundary? | yes | Brand new auth boundary: unauthenticated client → Identity service; token-bearing client → six consumer services. No auth existed before. |
| Does this feature touch regulated data (PII, payment, health, biometric, children's)? | yes | Email addresses (PII), passwords (stored as bcrypt hashes), session refresh tokens (stored in MongoDB). |
| Does this feature add a new attack surface? | yes | Four new endpoints on Identity (`/auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout`); JWT Bearer middleware wired across six existing services. |

**Triage outcome:** Full (3/3)

---

## Trust Boundaries

| ID | Boundary | What crosses | Trust direction | Diagram reference |
|---|---|---|---|---|
| TB-1 | Client → Identity service | Email, password (plaintext in TLS), role on register; email + password on login; opaque refresh token on refresh/logout | Untrusted → semi-trusted | ARCHITECTURE.md §Data Flow |
| TB-2 | Identity service → MongoDB (IdentityDb) | Credential reads/writes: email, bcrypt hash, refresh token SHA-256 hash, expiry | Semi-trusted → trusted | ARCHITECTURE.md §Integration |
| TB-3 | Client → Consumer services | JWT Bearer token in Authorization header | Untrusted → semi-trusted | ARCHITECTURE.md §Authenticated Request |
| TB-4 | Consumer service middleware → Handler | Populated HttpContext.User (sub, role claims) | Semi-trusted → trusted (internal) | ARCHITECTURE.md §Authenticated Request |

---

## Threats Identified

### T-001 — Brute-force credential stuffing on /auth/login

- **STRIDE category:** Elevation of Privilege
- **Trust boundary:** TB-1
- **Asset affected:** User accounts; provider and customer data behind those accounts
- **Attack vector:** An attacker submits thousands of `/auth/login` requests using leaked email/password pairs from other breaches (credential stuffing) or iterates common passwords against known emails (password spraying). No rate limiting, no lockout, no CAPTCHA — the endpoint accepts unlimited attempts. bcrypt at cost 12 slows each attempt to ~100–300ms, but a distributed botnet absorbs that easily.
- **Severity:** HIGH
- **DREAD breakdown:** Damage H · Reproducibility H · Exploitability H (no technical barrier) · Affected users (all accounts) · Discoverability H (well-known attack; any public endpoint is probed)
- **Mapped frameworks:** OWASP API Security Top 10 — API4:2023 Unrestricted Resource Consumption; OWASP Top 10 — A07:2021 Identification and Authentication Failures; CWE-307
- **Current mitigation status:** None. bcrypt cost factor provides per-attempt slowdown but does not prevent volume attacks.
- **Proposed action (party recommendation):** Accept for v1; Mitigate later
  - **Accept rationale (Atlas):** Platform has no public users yet; threat-actor profile is opportunistic, not targeted. bcrypt cost 12 raises the floor. Risk is documented and visible.
  - **Mitigate later:** Rate-limit `/auth/login` by IP (e.g., 10 attempts / 15 min / IP) and by email (5 attempts / 15 min / email) in a dedicated security-hardening feature. Record as ADR.
- **Decision (human, at Step 12 approval):** *[blank]*
- **Cross-talk note:** Pulse raised that bcrypt cost 12 at high concurrency creates a CPU amplification risk — 100 parallel login requests could saturate the Identity service's thread pool. Friday estimated this is low risk at current scale but noted it becomes a DoS vector when the platform has real users. Recorded as T-007 (separate).

---

### T-002 — RSA private key compromise

- **STRIDE category:** Spoofing, Elevation of Privilege
- **Trust boundary:** TB-1, TB-3
- **Asset affected:** All issued JWTs; every authenticated endpoint across all six services
- **Attack vector:** If the RSA private key (`JWT_PRIVATE_KEY`) is leaked — via a misconfigured container environment, a log line that dumps env vars, a developer accidentally committing it to git, or a breach of the Identity service host — an attacker can forge arbitrary JWTs for any email and any role. All six consumer services accept the forged tokens as valid. There is no per-token validation against a server-side store.
- **Severity:** CRITICAL
- **DREAD breakdown:** Damage H (total auth bypass) · Reproducibility H (once key is known, unlimited forgeries) · Exploitability M (requires key exfiltration first) · Affected users (all) · Discoverability L (key itself is not discoverable without host access)
- **Mapped frameworks:** OWASP Top 10 — A02:2021 Cryptographic Failures; CWE-321; CWE-798
- **Current mitigation status:** Partial. Design specifies env-var injection only; startup fail-fast if key is absent. Key material is never written to config files or source code.
- **Proposed action (party recommendation):** Mitigate now
  - **Specific controls (Neo + Phantom):**
    1. Startup validation already in design — `ApplicationException` if `JWT_PRIVATE_KEY` absent. Keep this.
    2. Add explicit log line at startup: `"RSA key loaded (fingerprint: {sha256_of_public_key})"` — key material never logged, only fingerprint.
    3. Add to deployment runbook: key rotation procedure (generate new pair, deploy public key to consumers, deploy private key to Identity, verify fingerprint matches across all services).
    4. Add `.env` and `*.pem` to `.gitignore` — belt-and-suspenders if a developer generates a local key pair.
  - **Bolt's effort estimate:** All four controls are documentation + one log line. Low implementation cost.
- **Decision (human, at Step 12 approval):** *[blank]*

---

### T-003 — Algorithm confusion attack (alg:none / HS256 downgrade)

- **STRIDE category:** Spoofing, Elevation of Privilege
- **Trust boundary:** TB-3
- **Asset affected:** Authorization on all six consumer services
- **Attack vector:** An attacker submits a JWT with `"alg": "none"` (no signature) or `"alg": "HS256"` (symmetric, using the public key as the shared secret). A naive JWT library that trusts the `alg` header would accept these. The attacker sets any `sub` and `role` claim they want — full privilege escalation to Provider with any email.
- **Severity:** HIGH
- **DREAD breakdown:** Damage H · Reproducibility H · Exploitability M (JWT structure knowledge required) · Affected users (all) · Discoverability H (well-known class of attack)
- **Mapped frameworks:** OWASP API Security Top 10 — API2:2023 Broken Authentication; CWE-347
- **Current mitigation status:** Mitigated by design: `TokenValidationParameters.ValidAlgorithms = ["RS256"]` in `AddAgendaBuddyAuthentication()`. Explicit rejection.
- **Proposed action (party recommendation):** Mitigate now (already in design — verify in implementation)
  - **Echo's test requirement:** Add a unit test in `Identity.Tests` that submits a JWT with `alg: none` and a JWT with `alg: HS256` to each consumer service's auth middleware and asserts both return 401. This is the regression guard.
- **Decision (human, at Step 12 approval):** *[blank]*

---

### T-004 — Missing handler-level ownership check (IDOR)

- **STRIDE category:** Elevation of Privilege, Information Disclosure
- **Trust boundary:** TB-4
- **Asset affected:** Appointments, calendar availability, provider profiles, customer profiles
- **Attack vector:** A valid Provider JWT passes middleware (role check passes) but the handler omits the sub-vs-email ownership check. The provider can read or mutate any other provider's appointments, availability, or profile. This is Insecure Direct Object Reference — the resource ID in the URL is the only access gate, and it's not owner-validated.
- **Severity:** HIGH
- **DREAD breakdown:** Damage H (cross-provider data leak and mutation) · Reproducibility H (any valid provider token; just change the ID in the URL) · Exploitability H (trivially exploitable once you have a valid token) · Affected users (all providers) · Discoverability H (standard IDOR scan pattern)
- **Mapped frameworks:** OWASP Top 10 — A01:2021 Broken Access Control; OWASP API Security Top 10 — API1:2023 Broken Object Level Authorization; CWE-639
- **Current mitigation status:** Partial. PRD and ARCHITECTURE.md specify ownership checks in Booking, Calendar, Provider, and Customer handlers. Risk is that implementation misses one or implements it inconsistently.
- **Proposed action (party recommendation):** Mitigate now
  - **Specific controls (Neo + Echo):**
    1. Define a shared `OwnershipGuard.AssertOwner(HttpContext, string entityEmail)` helper in Library that centralizes the sub-vs-email check and throws `ForbiddenException` on mismatch. Handlers call it rather than duplicating the check.
    2. Echo writes an explicit IDOR test for every affected endpoint: valid token for user A cannot access resource owned by user B.
    3. Plan-phase Beads tasks must each include "ownership check implemented and tested" as an explicit acceptance criterion — Atlas to enforce this at task definition.
- **Decision (human, at Step 12 approval):** *[blank]*
- **Cross-talk note:** Atlas flagged that this is the highest-business-impact threat in the model — a provider seeing another provider's client list is a trust-destroying incident. Echo confirmed that IDOR tests are the most commonly missed test class in auth implementations. Neo proposed `OwnershipGuard` to prevent the copy-paste failure mode.

---

### T-005 — User enumeration via login timing side-channel

- **STRIDE category:** Information Disclosure
- **Trust boundary:** TB-1
- **Asset affected:** User email list (privacy)
- **Attack vector:** `/auth/login` must look up the credentials document by email before comparing the password. If the email is not found, the service returns 401 without running `bcrypt.Verify`. If the email exists, `bcrypt.Verify` runs (~100–300ms). An attacker measuring response time can distinguish "email exists but wrong password" (slow) from "email not found" (fast) and harvest a list of registered email addresses.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M (email list exposure, not account takeover) · Reproducibility H · Exploitability M (requires timing measurement tooling) · Affected users (all registered users' privacy) · Discoverability M
- **Mapped frameworks:** CWE-208 (Observable Timing Discrepancy); OWASP Top 10 — A07:2021
- **Current mitigation status:** Partial. The API contract already specifies identical 401 response bodies for unknown email vs. wrong password (prevents body-based enumeration). Timing is not addressed.
- **Proposed action (party recommendation):** Mitigate now
  - **Specific control (Bolt):** When email is not found, run a dummy `BCrypt.Verify("dummy", dummyHash)` call before returning 401 to normalize response time. The dummy hash is a pre-computed bcrypt hash of a random string, stored as a static field. Cost: ~5 lines of code, negligible.
- **Decision (human, at Step 12 approval):** *[blank]*

---

### T-006 — NoSQL injection via user-controlled email field

- **STRIDE category:** Tampering, Elevation of Privilege
- **Trust boundary:** TB-1, TB-2
- **Asset affected:** MongoDB credentials collection; potentially all collections if the DB user has broad permissions
- **Attack vector:** If the email field is passed unsanitized as a MongoDB query operator (e.g., `{"$gt": ""}` or `{"$where": "..."}`), a MongoDB injection attack could bypass credential lookup, return arbitrary documents, or execute server-side JavaScript. This requires the application to construct raw query strings from user input rather than using the MongoDB driver's typed query API.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage H (auth bypass if exploitable) · Reproducibility M (depends on query construction) · Exploitability L (MongoDB.Driver's typed `FilterDefinition<T>` API parameterizes by default; requires a deviation to be vulnerable) · Affected users (all) · Discoverability M
- **Mapped frameworks:** OWASP Top 10 — A03:2021 Injection; OWASP API Security Top 10 — API3:2023; CWE-943
- **Current mitigation status:** Partial. The design specifies `MongoDbRepository<CredentialEntity>` for all DB access, which uses `MongoDB.Driver`'s typed `FilterDefinition<T>` — this parameterizes queries by default and does not interpolate user input into query strings.
- **Proposed action (party recommendation):** Mitigate now (verify in implementation)
  - **Specific controls:** (1) `[EmailAddress]` data annotation validates format before any DB call — rejects operator strings like `{"$gt":""}` at input validation. (2) All MongoDB queries in `MongoDbRepository<CredentialEntity>` use `Builders<T>.Filter` (typed) — never `BsonDocument` constructed from user input. Echo adds a test asserting that a registration request with `{"email": {"$gt": ""}}` returns 400, not 500 or 200.
- **Decision (human, at Step 12 approval):** *[blank]*

---

### T-007 — bcrypt amplification DoS on /auth/login

- **STRIDE category:** Denial of Service
- **Trust boundary:** TB-1
- **Asset affected:** Identity service availability
- **Attack vector:** bcrypt at cost factor 12 takes ~100–300ms of CPU per verification. An attacker submitting 50–100 concurrent login requests (trivially achievable) can saturate the Identity service's thread pool and make the service unresponsive to legitimate users. This is a CPU-amplification DoS — each request is cheap for the attacker (one HTTP call) but expensive for the server (~200ms of CPU-intensive work).
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M (Identity service DoS; consumer services remain up but no new logins possible) · Reproducibility H · Exploitability H (just send parallel requests) · Affected users (all during attack) · Discoverability H
- **Mapped frameworks:** OWASP API Security Top 10 — API4:2023 Unrestricted Resource Consumption; CWE-400
- **Current mitigation status:** None. Rate limiting is out of scope for v1.
- **Proposed action (party recommendation):** Accept for v1; Mitigate later
  - **Accept rationale:** Pre-launch platform with no public traffic. bcrypt cost factor is a necessary security control and cannot be lowered. Rate limiting is the correct fix but is deferred.
  - **Mitigate later:** Same rate-limiting feature as T-001. Record as ADR. Note: bcrypt work factor should be re-evaluated if response times exceed 500ms p95 under normal load (NFR in PRD) — drop to cost 10 if necessary.
- **Decision (human, at Step 12 approval):** *[blank]*

---

### T-008 — PII in JWT payload (email as `sub` claim)

- **STRIDE category:** Information Disclosure
- **Trust boundary:** TB-3
- **Asset affected:** User email addresses
- **Attack vector:** The JWT payload contains `sub = email`. The payload is base64-encoded (not encrypted) — anyone holding the token can decode it and read the email. If tokens are logged, stored in browser localStorage, or transmitted over an unencrypted channel (misconfigured TLS), the user's email is exposed.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M (email exposure, not credential exposure) · Reproducibility M (requires token access) · Exploitability L (requires MITM, log access, or client-side storage breach) · Affected users (all) · Discoverability M
- **Mapped frameworks:** OWASP Top 10 — A02:2021 Cryptographic Failures; CWE-200
- **Current mitigation status:** Partial. CONSTITUTION.md §4 prohibits PII in logs. TLS is assumed (Docker Compose + deployment runbook responsibility).
- **Proposed action (party recommendation):** Accept
  - **Accept rationale (Atlas + Phantom):** Email-as-`sub` is standard JWT practice (RFC 7519). The alternative (opaque UUID as sub) would require a sub→email lookup on every ownership check in every handler, adding latency and DB calls. The email in the JWT is visible to the authenticated user (they own it). The risk is token logging — mitigated by the existing PII-in-logs prohibition in CONSTITUTION.md. Record as ADR.
- **Decision (human, at Step 12 approval):** *[blank]*

---

## Threats Noted but Not Prioritized

| ID | Title | STRIDE | Boundary | Why deprioritized |
|---|---|---|---|---|
| T-NL-1 | No audit log of auth events (login, registration, logout) | Repudiation | TB-1 | Platform has no compliance requirement for auth audit logs in v1. Low impact at current scale. |
| T-NL-2 | Missing HSTS / security headers on Identity service | Information Disclosure | TB-1 | API-only service; no HTML rendered. HSTS and CSP are relevant for browser-facing services. Client is responsible for TLS configuration. |
| T-NL-3 | MongoDB connection string in environment variable | Information Disclosure | TB-2 | Same risk exists across all six existing services. Not new to this feature. Mitigated by Docker secrets / env var injection at deploy time. |
| T-NL-4 | Refresh token hash in MongoDB exposed via DB breach | Tampering | TB-2 | SHA-256 hash of opaque token. Raw token is never stored. Without the raw token, the hash cannot be replayed. |

---

## Open Questions for Human

1. **Regulatory exposure:** Do any of Agenda Buddy's target markets (US, EU, other) impose a regulatory obligation around storing email + hashed passwords (e.g., GDPR breach notification, CCPA)? This affects whether T-001 (no rate limiting) and T-NL-1 (no audit log) need to be promoted from "mitigate later / not prioritized" to "mitigate now."

2. **Threat-actor profile:** Is the primary threat actor for v1 (a) opportunistic automated scanners, (b) a provider's disgruntled customer, or (c) a competitor? The answer changes whether T-001 (brute force) warrants an earlier fix. If the platform targets healthcare or legal professionals, the threat profile is more targeted.

---

## Approval Outcomes (filled in at Step 12)

| Threat ID | Party recommendation | Human decision | Rationale |
|---|---|---|---|
| T-001 | Accept / Mitigate later | *[blank]* | — |
| T-002 | Mitigate now | *[blank]* | — |
| T-003 | Mitigate now | *[blank]* | — |
| T-004 | Mitigate now | *[blank]* | — |
| T-005 | Mitigate now | *[blank]* | — |
| T-006 | Mitigate now | *[blank]* | — |
| T-007 | Accept / Mitigate later | *[blank]* | — |
| T-008 | Accept | *[blank]* | — |

**ADR registry updates required (pending human approval):**
- ADR for T-001: accepted-risk record — no rate limiting on `/auth/login` in v1
- ADR for T-007: accepted-risk record — bcrypt amplification DoS deferred; rate limiting in future hardening feature
- ADR for T-008: accepted-risk record — email as JWT `sub` claim; PII in payload accepted as standard practice

**Beads tasks to be created at Plan (Step 13):**
- T-002 mitigations: startup fingerprint log line + `.gitignore` update + deployment runbook key rotation procedure
- T-003 mitigation: unit tests for `alg:none` and HS256 rejection
- T-004 mitigation: `OwnershipGuard.AssertOwner()` helper in Library + IDOR tests for all four affected services
- T-005 mitigation: dummy bcrypt call on email-not-found path
- T-006 mitigation: verify typed query usage + NoSQL injection input test

---

## Revision History

| Date | Author | Change |
|---|---|---|
| 2026-07-30 | Phantom (initial draft) | Created at Step 10.5 — Full party, 3/3 triage |
