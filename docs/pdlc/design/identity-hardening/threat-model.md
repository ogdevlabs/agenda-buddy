# Threat Model — identity-hardening (F-021)
<!-- pdlc-template-version: 1.2.0 -->

**Date:** 2026-08-22 · **Lead:** Phantom (Security Reviewer) · **Tier:** Full
**Design under review:** [`ARCHITECTURE.md`](ARCHITECTURE.md) · [`data-model.md`](data-model.md) · [`api-contracts.md`](api-contracts.md)

> ⚠️ **Ran in `solo` mode** — one model reasoning as each role, because this session carries a standing
> instruction not to spawn agents, which overrides STATE's `Party Mode: agent-teams`. Fidelity is lower than
> independent context windows. Same condition as every F-016 meeting; recorded rather than glossed.

---

## Triage Record

| Question | Answer | Evidence |
|---|---|---|
| Does this feature introduce or modify a trust boundary? | **yes** | It changes the authentication surface itself — login/register throttling, a new lock state consulted before credential verification, and transport-security ordering across all 7 services (`ARCHITECTURE.md` §1, §3.1, §4) |
| Does this feature touch regulated data (PII, payment, health, biometric, children's)? | **yes** | Email is PII under `CONSTITUTION.md` §4, and F-021 adds a **new log sink** that could carry it (`ARCHITECTURE.md` D-8). It also handles BCrypt password hashes on the rotation path |
| Does this feature add a new attack surface? | **yes** | No new endpoint, but a new **unauthenticated-write path**: any anonymous caller can now cause a write to another user's credential document by submitting a wrong password for their email (`data-model.md` §5), plus two new configuration flags whose "off" state silently disables a security control |

**Triage tier: 3/3 → Full.**

---

## Trust Boundaries

| ID | Boundary | What crosses | Trust direction | Diagram reference |
|---|---|---|---|---|
| TB-1 | Anonymous internet → Identity `login` / `register` | Email + plaintext password | untrusted → semi-trusted | `ARCHITECTURE.md` §3.1 |
| TB-2 | Anonymous internet → Identity `refresh` | An opaque refresh token (a bearer credential) | untrusted → semi-trusted | `ARCHITECTURE.md` §3.2 |
| TB-3 | Identity → `IdentityDb.credentials` | Password hashes, refresh-token hashes, and now attacker-influenced counter writes | semi-trusted → trusted | `data-model.md` §5 |
| TB-4 | All 7 services → any HTTP client | Bearer tokens, and now an HSTS directive | trusted → untrusted (egress) | `ARCHITECTURE.md` §4 |
| TB-5 | Identity → log sink | Credential-mutation records (must not contain PII) | trusted → semi-trusted (egress) | `ARCHITECTURE.md` D-8 |

---

## Threats Identified

### T-101 — Unauthenticated CPU exhaustion via BCrypt amplification

- **STRIDE category:** Denial of Service
- **Trust boundary:** TB-1
- **Asset affected:** Availability of Identity — and therefore of every service, since all six others validate tokens Identity issues
- **Attack vector:** Every `login` or `register` request spends **262 ms of server CPU** on BCrypt at work factor 12 — measured, not estimated (`ARCHITECTURE.md` §2). Roughly **4 requests/sec pins one core**; ~31/sec saturates all 8. The attacker needs no valid account: Identity's constant-time enumeration mitigation verifies an unknown email against a dummy hash (`IdentityService.cs:96`), so **random addresses cost the same 262 ms**. A single host on a domestic connection can take Identity down, and no per-account control can see this traffic because it generates no per-account state.
- **Severity:** **HIGH**
- **DREAD breakdown:** Damage [H — full auth outage] · Reproducibility [H] · Exploitability [H — a `for` loop and `curl`] · Affected users [all] · Discoverability [M — requires knowing the work factor, which is inferable from response latency]
- **Mapped frameworks:** OWASP API Top 10 **API4:2023 Unrestricted Resource Consumption** · CWE-400 (Uncontrolled Resource Consumption) · CWE-770
- **Current mitigation status:** **None.** Zero rate-limiter references solution-wide.
- **Proposed action (party recommendation):** **Mitigate now** — per-IP sliding-window limiter on **both** `login` and `register`, evaluated before any BCrypt work or database access. 10 requests/minute ≈ 2.6 s of CPU/min/IP against a legitimate need of 2–3 attempts.
  - **Testable acceptance criterion:** Given rate limiting enabled, when more than the configured number of requests arrive from one IP inside the window, then the excess receive `429` with `Retry-After`, **and no BCrypt work is performed for them** — asserted against a running service. *(PRD AC-6)*
- **Decision (human, at Step 12 approval):** *pending*
- **Cross-talk note:** This threat **inverted the feature's own premise.** F-021 was written around credential guessing; the measurement showed guessing runs at 3.8 attempts/sec/core, while the *same* cost makes DoS trivial. Pulse's measurement + Phantom's reading of the T-005 dummy-hash mitigation together produced it — neither lens alone would have.

### T-102 — Write amplification against the credential collection

- **STRIDE category:** Denial of Service / Tampering
- **Trust boundary:** TB-3
- **Asset affected:** Integrity and availability of `IdentityDb.credentials` — a collection with **no backups**
- **Attack vector:** The new failed-attempt counter means an anonymous caller can force a write to **any** account's document by submitting a wrong password for a known email. Unbounded, that is attacker-controlled write volume against the one collection whose loss is unrecoverable, on a cluster whose credential is **still unrotated** (`agenda-buddy-41s`). It is also a targeted lock: N wrong guesses locks a chosen provider.
- **Severity:** **MEDIUM** (would be HIGH without T-101's mitigation, on which this depends)
- **DREAD breakdown:** Damage [M — no data loss, but contention and a locked victim] · Reproducibility [H] · Exploitability [M — needs a valid email] · Affected users [targeted individual, or many] · Discoverability [M]
- **Mapped frameworks:** OWASP API Top 10 API4:2023 · CWE-799 (Improper Control of Interaction Frequency)
- **Current mitigation status:** N/A — introduced by this feature.
- **Proposed action (party recommendation):** **Mitigate now**, by ordering: the per-IP limiter is evaluated **before** the per-account write, so the write is rate-limited before it happens. The lock's **automatic expiry** bounds the targeted-lock damage to one window; there is deliberately no permanent lock (`ARCHITECTURE.md` D-5).
  - **Testable acceptance criterion:** Given N consecutive failed logins, when the counter is written, then the write is a targeted atomic increment and never a whole-document replacement. *(PRD AC-11)* — and, given a throttled IP, no counter write occurs at all *(PRD AC-6 side effect)*.
- **Decision (human, at Step 12 approval):** *pending*
- **Cross-talk note:** Echo raised the read-path-becomes-write-path concern; Phantom sharpened it into an attacker-controlled write; Neo's ordering answer (per-IP first) resolves both at once.

### T-103 — Security control silently disabled by configuration

- **STRIDE category:** Elevation of Privilege (via control bypass)
- **Trust boundary:** TB-1, TB-4
- **Asset affected:** Every guarantee this feature claims
- **Attack vector:** Not an attack so much as a **latent absence**. Both controls are gated on `Security:RateLimiting:Enabled` and `Security:Hsts:Enabled`, defaulting **off** so local development is unobstructed. A deployment that never sets them ships an Identity with no throttling and no HSTS, while the PRD, the episode and the roadmap all record the feature as delivered. This is the same failure shape as F-016's original defect — `AssertRole` was *present in the codebase* and never called.
- **Severity:** **MEDIUM**
- **DREAD breakdown:** Damage [H if it happens] · Reproducibility [H — one missing key] · Exploitability [n/a — no attacker needed] · Affected users [all] · Discoverability [L for us, M for an attacker probing for 429s]
- **Mapped frameworks:** OWASP Web Top 10 **A05:2021 Security Misconfiguration** · CWE-1188 (Insecure Default Initialization)
- **Current mitigation status:** N/A — introduced by this feature's own design choice.
- **Proposed action (party recommendation):** **Mitigate now** — each service **warns loudly at startup** when a flag is off while it is not running locally (`ARCHITECTURE.md` D-7), and the integration harness switches both **on** so neither control can ship unexercised.
  - **Testable acceptance criterion:** Given the harness enables both flags, when the suite runs, then the `429` behaviour and the `Strict-Transport-Security` header are each asserted against a running service. *(PRD AC-15)*
- **Decision (human, at Step 12 approval):** *pending*
- **Cross-talk note:** Surfaced as risk R4 at Define and deliberately left unresolved for Design; the maintainer chose "warn loudly" over "fail fast" so a config slip is visible without becoming an outage.

### T-104 — Lockout bypass via a live refresh token

- **STRIDE category:** Elevation of Privilege
- **Trust boundary:** TB-2
- **Asset affected:** The lock control itself
- **Attack vector:** An attacker who has already obtained a refresh token — from a stolen device, a leaked log, or a shared machine — could keep minting access tokens indefinitely while the account is locked, if refresh ignored lock state. Refresh tokens live **24 hours** (`IdentityService.cs:149`), so the bypass window is long, and the mobile client holds one continuously.
- **Severity:** **MEDIUM**
- **DREAD breakdown:** Damage [H — the lock is defeated] · Reproducibility [H] · Exploitability [L — requires already holding a token] · Affected users [targeted] · Discoverability [L]
- **Mapped frameworks:** OWASP API Top 10 API2:2023 (Broken Authentication) · CWE-613 (Insufficient Session Expiration)
- **Current mitigation status:** N/A — the lock does not exist yet.
- **Proposed action (party recommendation):** **Mitigate now** — the lock condition is part of the rotation **filter**, so a locked account cannot refresh, at no extra query cost (`data-model.md` §5).
  - **Testable acceptance criterion:** Given a locked account and a valid refresh token, when the token is presented, then the response is `401` and no token pair is issued. *(PRD AC-4)*
- **Decision (human, at Step 12 approval):** *pending*

### T-105 — PII disclosure through the new log sink

- **STRIDE category:** Information Disclosure
- **Trust boundary:** TB-5
- **Asset affected:** Customer and provider email addresses
- **Attack vector:** F-021 adds credential-mutation logging to a service that currently has **no log sink at all**. The natural implementation logs the account being mutated — i.e. the email — which is PII under §4. `PiiRedactingProcessor` protects **spans, not logs**, so nothing downstream would catch it, and `Identity/Program.cs:100-102` already carries a standing instruction against body logging on exactly these routes because of this hazard. Logs then flow to the Aspire dashboard and any future aggregator.
- **Severity:** **MEDIUM**
- **DREAD breakdown:** Damage [M — the cluster holds synthetic data today, so exposure is of dev records] · Reproducibility [H — it would happen on every mutation] · Exploitability [L — needs log access] · Affected users [all] · Discoverability [H once logs are read]
- **Mapped frameworks:** OWASP Web Top 10 A09:2021 (Security Logging and Monitoring Failures) · CWE-532 (Insertion of Sensitive Information into Log File) · GDPR Art. 5(1)(c) data minimisation
- **Current mitigation status:** N/A — introduced by this feature.
- **Proposed action (party recommendation):** **Mitigate now** — log the operation, the outcome, and a **non-reversible hash prefix** of the account identifier. Never the address.
  - **Testable acceptance criterion:** Given any credential mutation, when log output is inspected, then the operation and outcome are recorded and **no raw email address appears** in any line. *(PRD AC-16)*
- **Decision (human, at Step 12 approval):** *pending*
- **Cross-talk note:** Jarvis flagged the collision between "log credential mutations" and §4; Phantom escalated it to a threat because the F-013 precedent is exact — telemetry was switched on and immediately began exporting real customer emails in `url.path` (threat T-004).

### T-106 — Distributed rate-limit evasion across replicas

- **STRIDE category:** Denial of Service
- **Trust boundary:** TB-1
- **Asset affected:** The effectiveness of T-101's mitigation
- **Attack vector:** ASP.NET's rate limiter keeps state **in process**. This project registers `AddDistributedMemoryCache()` everywhere, so there is no shared store (`00-overview.md` finding 7). With N Identity replicas behind a load balancer, an attacker's effective allowance is **N × the configured limit**, and round-robin balancing distributes their requests for them. Conversely, per-IP limiting behind a NAT or corporate egress throttles unrelated legitimate users sharing one address.
- **Severity:** **LOW** (today — Identity runs as a single instance locally and has never been deployed)
- **DREAD breakdown:** Damage [M] · Reproducibility [H once replicated] · Exploitability [H] · Affected users [all] · Discoverability [M]
- **Mapped frameworks:** OWASP API Top 10 API4:2023 · CWE-770
- **Current mitigation status:** Partial by accident — there is only one instance.
- **Proposed action (party recommendation):** **Accept**, documented. Fixing it needs a distributed store F-021 is not scoped to add, and the per-account counter (which *is* shared, being in MongoDB) still holds across replicas — so the two controls degrade unevenly rather than both failing. Re-evaluation trigger: **the first deployment that runs more than one Identity replica**, which cannot happen before F-017.
  - **Residual risk:** an attacker with access to N replicas gets N× the per-IP allowance for the CPU-exhaustion attack. The per-account lock is unaffected.
- **Decision (human, at Step 12 approval):** *pending*

---

## Threats Noted but Not Prioritized

| ID | Title | STRIDE | Boundary | Why deprioritized |
|---|---|---|---|---|
| T-NL-1 | HSTS is decorative without TLS termination | Info Disclosure | TB-4 | The header instructs a *future* request to use HTTPS; with no deployed TLS endpoint (F-017 owns it) there is nothing to upgrade to. Correct to add now, ineffective until then — and the design deliberately omits `preload`/`includeSubDomains`, which are the hard-to-reverse parts |
| T-NL-2 | Lock state enables timing-based enumeration | Info Disclosure | TB-1 | A locked account returns `401` *before* spending 262 ms on BCrypt, so it answers measurably faster than a wrong password. That is a real oracle for "this address exists **and** is locked". Deprioritized because the alternative — burning 262 ms of CPU to hide it — directly re-arms T-101, which is a higher-severity threat. The trade is deliberate and worth stating in the PRD rather than hiding |
| T-NL-3 | No lockout notification | Repudiation | TB-1 | A locked-out user is told nothing, so a targeted lock campaign is invisible to its victim. Needs `NotificationService`, which **F-014** wires |
| T-NL-4 | Access tokens survive a lock for up to 60 minutes | Elev. of Privilege | TB-2 | Locking stops new tokens; it cannot revoke an issued one. That is **F-023** (`jti` denylist), explicitly out of scope by the maintainer's Discover decision |
| T-NL-5 | The unrotated Atlas credential | Tampering | TB-3 | Anyone with the leaked credential writes directly to `credentials`, bypassing every control here. Human-only (`agenda-buddy-41s`); caps what this feature can claim |

---

## Open Questions for Human

1. **T-NL-2 accepted as a trade?** Answering a locked account fast is an enumeration oracle; answering it slowly re-arms the CPU-exhaustion threat. The design chooses fast (and says so). Confirm, or accept the CPU cost to close the oracle.
2. **T-106 accepted?** Per-IP limiting is per-process and this project has no distributed cache. Accepting means the limit is per-replica — currently harmless, since there is exactly one replica and no deployment.
3. **Is the `credentials` collection's missing unique index on `email` worth a bead now** (`data-model.md` §4), or left for whoever owns registration correctness? It is not F-021's, but F-021 is the feature reading that collection on every login.

---

## Approval Outcomes (filled in at Step 12)

| Threat ID | Party recommendation | Human decision | Rationale |
|---|---|---|---|
| T-101 | Mitigate now | *pending* | |
| T-102 | Mitigate now | *pending* | |
| T-103 | Mitigate now | *pending* | |
| T-104 | Mitigate now | *pending* | |
| T-105 | Mitigate now | *pending* | |
| T-106 | Accept (documented) | *pending* | |

---

## Mitigation → Task → `[security]` AC mapping

Materialized at Plan (Step 13) with `tasks.cjs ac add … --tag security --threat T-NNN`, so `tasks.cjs done` cannot close a task whose security AC has no linked test.

| Threat ID | Task (assigned at Plan) | Testable `[security]` AC |
|---|---|---|
| T-101 | Per-IP limiter on `login` + `register`, before BCrypt | PRD **AC-6** — `429` + `Retry-After`, no BCrypt spent, asserted against a running service |
| T-102 | Atomic `$inc` counter, ordered behind the limiter | PRD **AC-11** (targeted write, never a replacement) + **AC-9** (no upsert for unknown emails) |
| T-103 | Startup warning when a flag is off outside local; harness enables both | PRD **AC-15** — both controls exercised by tests |
| T-104 | Lock condition inside the rotation filter | PRD **AC-4** — locked account + valid refresh token ⇒ `401` |
| T-105 | Hash-prefix logging helper | PRD **AC-16** — no raw email in any log line |
| T-106 | *(accepted — ADR at Plan, no task)* | — |

Threat IDs continue from F-016's T-001…T-008 series, starting at **T-101** to make the feature boundary unambiguous in test names (`test_T101_…`).

---

## Revision History

| Date | Author | Change |
|---|---|---|
| 2026-08-22 | Phantom (initial draft, solo mode) | Created at Step 10.5. Triage 3/3 → Full. Six threats identified, five to mitigate now, one accepted; five deprioritized |
