# Threat Model — secure-public-endpoints (F-016)
<!-- pdlc-template-version: 1.0.0 -->

**Triage:** Full
**Convened:** 2026-08-18
**Lead:** Phantom (Security Reviewer)
**Participants:** Phantom (lead), Neo, Bolt, Echo, Pulse, Atlas, Muse, Jarvis, Friday — 9 agents
**Spawn mode:** `solo` — the session carries a standing "do not call the Agent tool unless requested" instruction, which overrides STATE.md's `Party Mode: agent-teams`. Recorded rather than silently substituted.
**Status:** **Approved** (Step 12, 2026-08-18) — all 7 mitigate-now recommendations confirmed; 3 open questions resolved in favour of the stronger option each time

> **Note on posture.** This is a threat model *of a security fix*. The interesting findings are not "the old code was insecure" — the PRD already documents that. They are **the ways the new design could fail, be bypassed, or make something worse.** Five of the eight threats below are introduced or newly *reachable* because of this feature.

---

## Triage Record

| Question | Answer | Evidence |
|---|---|---|
| Does this feature introduce or modify a trust boundary? | **yes** | It *creates* an authorization boundary at five routes that had none (ARCHITECTURE §3.1), adds ownership scoping to two more, adds the solution's **first two `AssertRole` call sites** (§3.2), and moves exception handling into Production for the first time (AD-1). |
| Does this feature touch regulated data (PII, payment, health, biometric, children's)? | **yes** | Provider and customer names + emails; appointment records linking a named customer to a named provider — for a therapist or coach the association itself is sensitive third-party data (`data-model.md` §2). Cluster currently holds synthetic data only, but the data *classes* are PII. |
| Does this feature add a new attack surface? | **yes** | New client-controlled input (`page`, `pageSize` query parameters); a new response type; a new `IExceptionHandler` executing in Production; and a **new test harness whose connection-string resolution path can reach a live cluster**. |

**Triage outcome: Full (3/3).**

---

## Trust Boundaries

| ID | Boundary | What crosses | Trust direction | Diagram reference |
|---|---|---|---|---|
| TB-1 | Anonymous internet → the 5 newly-authenticated GET routes | bearer token (or none) | untrusted → semi-trusted | ARCHITECTURE §9 |
| TB-2 | Authenticated caller → **another principal's** data | `sub` claim vs `{email}` route value | semi-trusted → trusted | ARCHITECTURE §3.3, §9 |
| TB-3 | Authenticated caller (any role) → privileged write | `role` claim | semi-trusted → trusted | ARCHITECTURE §3.2 |
| TB-4 | Request handler → audit store (`events`) | serialised payload | trusted → trusted (amplification) | ARCHITECTURE §5 |
| TB-5 | **Integration test process → a database** *(new — created by this feature)* | resolved MongoDB connection string | test → **potentially production** | ARCHITECTURE §6 |
| TB-6 | Exception handler → client | error body | trusted → untrusted (egress) | ARCHITECTURE §2, `api-contracts.md` §3 |

---

## Threats Identified

### T-001 — Null-claim ownership bypass promotes an attacker to *owner* and returns the full appointment book

- **STRIDE category:** Elevation of Privilege / Information Disclosure
- **Trust boundary:** TB-2
- **Asset affected:** Every provider's embedded appointment book and subscribed-customer list — the exact data this feature exists to protect.
- **Attack vector:** The design selects the response shape by comparing the caller's `sub` claim to the provider's email (ARCHITECTURE §3.3). `OwnershipGuard.AssertOwner` does that comparison with `string.Equals(sub, entityEmail, OrdinalIgnoreCase)` and **`:9-10` does not guard against a null claim** — so `string.Equals(null, null)` returns `true` and the guard **passes** (`13-security.md:135`). `AssertOwnerAny` explicitly checks `sub is null` first; `AssertOwner` does not. If the projection decision reuses that comparison and both sides can be null, a caller presenting a token with no `NameIdentifier` claim against a record with a null email is treated as **the owner** and receives the unprojected entity.
- **Severity:** **HIGH**
- **DREAD breakdown:** Damage **H** (full third-party PII) · Reproducibility **M** (needs a token minted without `sub`, or a null-email record) · Exploitability **M** · Affected users **all providers and their clients** · Discoverability **M** (the asymmetry is documented in the repo's own catalog, and the repo is public)
- **Mapped frameworks:** OWASP API Top 10 **API1:2023 Broken Object Level Authorization**; **API3:2023 Broken Object Property Level Authorization**; CWE-476 (null-deref-adjacent logic error), CWE-863 (incorrect authorization)
- **Current mitigation status:** **None.** The hole exists today but is currently *unreachable at these routes*, because nothing at them branches on ownership. **This feature makes it reachable.**
- **Proposed action (party recommendation):** **Mitigate now**
  - Fix `AssertOwner` to reject a null `sub` before comparing — matching `AssertOwnerAny`'s existing behaviour. One line. Bolt: trivial. Neo: confirmed architectural fit — it is a defect in the guard, not a design change.
  - **This reassigns PRD requirement 18.** The PRD deferred the `AssertOwner` null-claim fix to F-021 ("SHOULD be left to F-021 unless section B work touches that file first"). **It must be fixed in F-016**, because F-016 is what makes it exploitable.
  - **Testable acceptance criterion (required):** *Given a valid token carrying no `NameIdentifier`/`sub` claim, when `GET /api/v1/providers/{email}` is called, the response is 403 (or the projected `ProviderSummary`) and never the full `ProviderEntity`; and `OwnershipGuard.AssertOwner(user, null)` throws `ForbiddenException` rather than returning.*
- **Decision (human, at Step 12 approval):** **Confirmed — mitigate now.** PRD requirement 18 moves from F-021 into F-016.
- **Cross-talk note:** Found by chaining Neo → Phantom. Neo's §3.3 described selecting the response shape by ownership comparison; Phantom recognised the comparison primitive as the one with the documented null asymmetry. Neither doc alone surfaces it — ARCHITECTURE treats ownership as a solved primitive, and the PRD had already filed the null bug under a *different feature*.

---

### T-002 — The harness's fail-closed guard is bypassable if it matches on hostname

- **STRIDE category:** Tampering / Denial of Service
- **Trust boundary:** TB-5
- **Asset affected:** The live MongoDB Atlas cluster — every provider, customer, appointment, session note, payment record and password hash. **There are no backups.**
- **Attack vector:** Not an external attacker — a **developer accident with an outsized blast radius.** `MongoConnectionResolver` resolves Aspire → environment → appsettings, so a single stray `ConnectionStrings__mongodb` in a shell, a leftover `launchSettings` entry, or an inherited CI variable is enough for the harness to resolve a real cluster. Integration setup then creates and drops databases and inserts fixtures. The credential is **still valid and still recoverable from the public repo's git history** (`ISSUE-002`, unrotated). The naive implementation of requirement 5 — assert the host is `localhost`/`127.0.0.1` — **does not close this**: a developer may legitimately run Mongo on localhost, and an SSH tunnel or `kubectl port-forward` to Atlas also presents as localhost.
- **Severity:** **CRITICAL**
- **DREAD breakdown:** Damage **H** (irreversible, no backups) · Reproducibility **H** (one environment variable) · Exploitability **H** (no attacker needed) · Affected users **all** · Discoverability **H** (the failure is silent — tests pass)
- **Mapped frameworks:** CWE-15 (external control of system setting), CWE-665 (improper initialization); OWASP **A05:2021 Security Misconfiguration**
- **Current mitigation status:** **None** — no integration harness exists yet, so there is nothing to bypass. This threat is created wholesale by this feature.
- **Proposed action (party recommendation):** **Mitigate now**
  - The guard MUST assert **identity, not shape**: compare the resolved connection string against the endpoint the Testcontainers API reports for the container this fixture started (its dynamically-assigned host port), obtained from the container object itself. A hostname or port *pattern* is explicitly insufficient.
  - Abort **before any test body executes** and before any database/collection is created — fail in fixture construction, not in a test.
  - Pulse adds: also refuse if the resolved string contains `mongodb+srv://` (Atlas's scheme) or any credential component, as a cheap second layer.
  - **Testable acceptance criterion (required):** *Given `ConnectionStrings__mongodb` is exported to a value that is not the endpoint reported by the fixture's own Testcontainer, when the integration suite starts, it aborts during fixture construction with a message naming the offending host, and **no database or collection is created**.*
- **Decision (human, at Step 12 approval):** **Confirmed — mitigate now.** Guard asserts container identity, not hostname; holds in CI as well as locally (open question 4).
- **Cross-talk note:** Phantom raised the guard; **Pulse broke the naive version** by pointing out that localhost is not a trust signal under port-forwarding — which is exactly how this project reaches Atlas today. Neo then confirmed the fix belongs to the fixture's construction path, not a test attribute.

---

### T-003 — Any authenticated user can still extract the entire customer table, 100 rows at a time

- **STRIDE category:** Information Disclosure
- **Trust boundary:** TB-2
- **Asset affected:** Every customer's name and email address; plus `totalCount` as a business-intelligence signal.
- **Attack vector:** PRD requirement 9 makes `GET /api/v1/customers` **authenticated**. It does not make it **authorized**. Anyone can self-register as a `Customer` at the anonymous `POST /api/v1/auth/register` — there is no invitation, no email verification, and no rate limit (`13-security.md:109`). So an attacker signs up, obtains a valid token, and iterates `?page=1..N` to reconstruct the full customer table exactly as before, throttled only by their own patience. `totalCount` in the response envelope even tells them how many pages to fetch. **Pagination bounds each response; it does not bound extraction.**
  Ask the product question: *why would any user list every customer?* There is no discovery use case for customers — F-003 defines discovery as customers finding **providers**, not each other. The route has no legitimate caller other than a provider looking at their own subscribers.
- **Severity:** **HIGH**
- **DREAD breakdown:** Damage **H** (complete PII table) · Reproducibility **H** · Exploitability **H** (free self-registration, no rate limit) · Affected users **all customers** · Discoverability **H** (it is the obvious next probe after finding the route needs auth)
- **Mapped frameworks:** OWASP API Top 10 **API1:2023 BOLA**, **API4:2023 Unrestricted Resource Consumption**; **A01:2021 Broken Access Control**; CWE-200, CWE-863
- **Current mitigation status:** **Partial after this feature** — authentication removes the anonymous attack and cuts the cost from "one request" to "register, then N requests." That is a real reduction and not nothing. It is not sufficient.
- **Proposed action (party recommendation):** **Mitigate now**
  - `GET /api/v1/customers` must be **role-scoped, not merely authenticated.** Atlas's read: the only defensible caller is a provider, and arguably only for their own subscribed customers. Minimum viable fix inside this feature: require the `Provider` role. Better fix: scope the result to the calling provider's `SubscribedCustomerCollection`.
  - Muse notes the UX cost is nil — no shipped screen consumes this route (the mobile client cannot reach it at all).
  - Friday: the role check is the same one-liner as requirement 13, so the marginal cost over the approved scope is near zero.
  - **Testable acceptance criterion (required):** *Given a valid token whose only role is `Customer`, when `GET /api/v1/customers` is called, the response is 403 and no customer record is returned.*
- **Decision (human, at Step 12 approval):** **Confirmed — mitigate now, require the `Provider` role.** Scope addition accepted over owner-scoping (deferred as more work) and over accepting the risk.
- **Cross-talk note:** Atlas → Phantom → Bolt. Atlas asked the product question ("who is this endpoint *for*?") which reframed a hardening item as a missing authorization rule. Phantom connected it to anonymous self-registration; Bolt confirmed the fix is the same primitive as requirement 13, so it is cheap.

---

### T-004 — The new Production exception handler becomes an internals-disclosure channel

- **STRIDE category:** Information Disclosure
- **Trust boundary:** TB-6
- **Asset affected:** Internal implementation detail — exception types, stack frames, driver messages, connection strings embedded in `MongoException` text.
- **Attack vector:** AD-1 registers `UseExceptionHandler()` **unconditionally**, so for the first time these six services have an exception handler running in Production. Today Production returns a bare, empty-bodied 500 — accidentally the most conservative possible behaviour. If the new handler populates `ProblemDetails.Detail` from `exception.Message`, or lets the existing Development lambda's text branch run in Production, then driver and framework exception text reaches clients. `MongoException` messages can carry host names and, in the worst case, connection-string fragments. An attacker triggers it cheaply: `MongoDbRepository.cs:28` throws `FormatException` on any non-24-hex id (`10-error-handling.md:116`).
- **Severity:** **MEDIUM**
- **DREAD breakdown:** Damage **M** · Reproducibility **H** (a malformed id in any path reaching `GetByIdAsync`) · Exploitability **H** · Affected users **all** (system-wide disclosure, not per-user) · Discoverability **M**
- **Mapped frameworks:** OWASP **A05:2021 Security Misconfiguration**; CWE-209 (generation of error message containing sensitive information), CWE-497
- **Current mitigation status:** **Accidental** — there is no Production handler at all, so nothing leaks. This feature removes that accident and must replace it with a deliberate control.
- **Proposed action (party recommendation):** **Mitigate now**
  - The handler returns **status, title and `requestId` only**. No `detail`, no exception type, no message, no stack trace — in any environment. It handles `ForbiddenException` and returns `false` for everything else, so unmapped exceptions keep their existing path rather than gaining a new body.
  - **Testable acceptance criterion (required):** *Given a request that triggers `ForbiddenException` while `ASPNETCORE_ENVIRONMENT=Production`, the 403 response body contains `status`, `title` and `requestId` and contains no exception type name, no exception message, and no stack frame.*
- **Decision (human, at Step 12 approval):** **Confirmed — mitigate now, require the `Provider` role.** Scope addition accepted over owner-scoping (deferred as more work) and over accepting the risk.
- **Cross-talk note:** Neo → Phantom. Neo's AD-1 was written as a pure improvement (it fixes a latent Production defect, which it does). Phantom's counter: any change that starts emitting bodies where none were emitted is an egress change and must be modelled as one, regardless of intent.

---

### T-005 — Reducing the audit payload without adding an actor destroys the forensic value of the audit trail

- **STRIDE category:** Repudiation
- **Trust boundary:** TB-4
- **Asset affected:** Incident-response capability — the ability to answer *who read this data, and when*.
- **Attack vector:** Requirement 16 reduces query-handler audit writes to operation metadata. `Event` has exactly five fields and **no actor** (`15-cqrs-and-messaging.md:215`), so a post-F-016 audit record for a read says *"a `GetProvidersQuery` succeeded at 14:03"* — with no indication of who did it and no correlation id. Combined with T-003, an attacker enumerating the customer table leaves a trail of hundreds of indistinguishable, unattributable records. Today the PII dump is a severe exposure, but it does at least record *what was read*. **After this feature, that is gone and nothing replaces it** — so the change is a net improvement in confidentiality and a net **regression** in accountability. Compounding: `jti` is minted and never recorded, so even the token cannot be tied to the action.
- **Severity:** **MEDIUM**
- **DREAD breakdown:** Damage **M** (no data lost; response capability lost) · Reproducibility **H** (it is the designed behaviour) · Exploitability **n/a** (it is a detection gap, not an attack) · Affected users **the operator** · Discoverability **L** (invisible until an incident)
- **Mapped frameworks:** OWASP **A09:2021 Security Logging and Monitoring Failures**; OWASP API Top 10 **API9:2023 Improper Inventory Management** (adjacent); CWE-778 (insufficient logging)
- **Current mitigation status:** **None**, and the feature makes it worse in this one dimension.
- **Proposed action (party recommendation):** **Mitigate now — the cheap version**
  - Add an `actor` field to `Event`, populated from the `sub` claim. Until F-016 these endpoints had **no authenticated caller to record**; this feature is what makes an actor field possible at all, which is precisely why it belongs here rather than later.
  - Cost: one `[BsonElement("actor")]` field plus one assignment per handler. It is a **data-model change** (`data-model.md` currently states "no schema changes"), which is why it needs the human's decision rather than Phantom's.
  - Echo notes the alternative — accept, and rely on OpenTelemetry traces for attribution — is weak: there is no log sink and `requestId` is not exported anywhere (`10-error-handling.md:138`), so nothing outside the `events` collection is durable.
  - **Testable acceptance criterion (required):** *Given an authenticated `GET /api/v1/providers`, the resulting `events` document records the caller's `sub` in an `actor` field and still contains no provider email, customer email, or appointment record.*
- **Decision (human, at Step 12 approval):** **Confirmed — mitigate now, add the `actor` field.** Accepted knowing it costs the clean no-migration rollback.
- **Cross-talk note:** Phantom → Echo → Atlas. Phantom flagged the accountability regression; Echo killed the "rely on telemetry" alternative by pointing at the missing sink; Atlas confirmed the business case (a provider asking "who saw my client list?" is a question the product should be able to answer).

---

### T-006 — A future refactor moves the Calendar ownership guard behind the cache and leaks cross-tenant data silently

- **STRIDE category:** Information Disclosure
- **Trust boundary:** TB-2
- **Asset affected:** Any provider's full appointment list, including customer emails.
- **Attack vector:** Both Calendar routes cache under `$"availability-{email}"` / `$"appointments-{email}"` — keyed on the request **subject**, never the **caller** (`Calendar/Program.cs:101,129`). Today that is safe *by accident*: with no ownership guard, every authenticated caller is entitled to every entry. This feature adds the guard, and the design places it **before** the cache read, which keeps it safe. But the safety now rests entirely on statement ordering inside a `Program.cs` route lambda, with **no test that pins it** and no type-level protection. Anyone who later reorders, extracts a helper, or caches the *response* instead of the *data* reintroduces a cross-tenant leak that no existing test detects. Given F-019/F-020 will rewrite every one of these files, this is not hypothetical.
- **Severity:** **MEDIUM** (latent; **HIGH** if it lands)
- **DREAD breakdown:** Damage **H** · Reproducibility **H** (once introduced, deterministic) · Exploitability **H** · Affected users **all providers** · Discoverability **L** for an attacker, **L** for a reviewer — which is what makes it dangerous
- **Mapped frameworks:** OWASP API Top 10 **API1:2023 BOLA**; CWE-524 (use of cache containing sensitive information), CWE-863
- **Current mitigation status:** None needed today; the risk is created by adding the guard to a cached route.
- **Proposed action (party recommendation):** **Mitigate now — with a test, not a comment**
  - Echo's design: an integration test that **warms the cache as the owner, then requests the same `{email}` as a different authenticated principal, and asserts 403.** A cache-ordering regression fails that test immediately; a comment would not.
  - Neo records the ordering as a design invariant in `api-contracts.md` §5.5 (done).
  - ⚠️ Echo's caveat: `CacheAside` has **no test at all** and returns `default!` on a 500 ms lock timeout, surfacing as a spurious 404/204 (`11-testing.md:90`). This test can therefore flake as a 404 instead of failing as a 200 — the assertion must be "**not** 200 with data", not "exactly 403", or Build will chase phantom failures.
  - **Testable acceptance criterion (required):** *Given the cache is warm for `{email}` from a request by its owner, when a different authenticated principal requests the same `{email}`, the response is 403 and contains no appointment data.*
- **Decision (human, at Step 12 approval):** **Confirmed — mitigate now.** Handler emits status/title/requestId only, in every environment.
- **Cross-talk note:** Neo raised the ordering as documentation (ARCHITECTURE §8); **Echo converted it into a test**, on the grounds that an invariant guarded only by prose is not guarded. Bolt then flagged the `CacheAside` timeout as a flake source, which shaped the assertion.

---

### T-007 — Role-gating profession creation gates the wrong thing, because no administrative role exists

- **STRIDE category:** Elevation of Privilege / Tampering
- **Trust boundary:** TB-3
- **Asset affected:** The global `professions` reference catalogue — shared, seeded, and read by every user.
- **Attack vector:** Requirement 13 adds `AssertRole` to `POST /api/v1/professions` so an arbitrary Customer cannot write to the global catalogue. But the role allow-list is exactly `{Provider, Customer}` (`Identity/Program.cs:100-106`) — **there is no admin role**. So the only implementable check is `AssertRole(user, "Provider")`, which means *any* self-registered provider can still pollute shared reference data read by every user. Since registration is open and unthrottled, the improvement is marginal: it raises the bar from "any account" to "any account that chose `Provider` at signup."
- **Severity:** **MEDIUM**
- **DREAD breakdown:** Damage **M** (data-integrity/defacement of shared reference data, not confidentiality) · Reproducibility **H** · Exploitability **H** (pick a role at signup) · Affected users **all** · Discoverability **M**
- **Mapped frameworks:** OWASP **A01:2021 Broken Access Control**; OWASP API Top 10 **API5:2023 Broken Function Level Authorization**; CWE-269 (improper privilege management)
- **Current mitigation status:** None — any authenticated user can write today.
- **Proposed action (party recommendation):** **Mitigate now**, but the party could not choose *which* mitigation without the human. Three honest options:
  1. **Remove the route.** Professions are seeded from `ProfessionSeedData.cs`; no shipped flow creates one. Deleting the endpoint is the strongest fix and reduces surface. Neo's preference.
  2. **Introduce an `Admin` role.** Correct, and scope creep — it touches Identity's role allow-list, token minting, and seeding, in a feature that deliberately excludes Identity (ARCHITECTURE §7).
  3. **Accept `Provider`-only.** Ship requirement 13 as literally written and record the residual risk. Atlas's view: acceptable for a pre-launch product with synthetic data.
  - **Testable acceptance criterion (required, option-independent):** *Given a valid token whose only role is `Customer`, when `POST /api/v1/professions` is called, the response is 403 and no profession is created.* (If option 1 is chosen, the criterion becomes 404/405 — the route is gone.)
- **Decision (human, at Step 12 approval):** **Confirmed — mitigate now, by DELETING `POST /api/v1/professions`.** Supersedes PRD requirement 13 — no route means no role check to add.
- **Cross-talk note:** Bolt → Phantom → Neo. Bolt went to implement the role check and found there was no role to check *for*. That is the whole finding: requirement 13 was written assuming a privilege tier the system does not have.

---

### T-008 — A token minted for any purpose authorizes the new role checks at every service

- **STRIDE category:** Spoofing / Elevation of Privilege
- **Trust boundary:** TB-1, TB-3
- **Asset affected:** All six domain services' newly-added authorization checks.
- **Attack vector:** `ValidateAudience = false` and no `aud` claim is issued, so **all seven services accept any token this issuer minted** (`13-security.md:71`). Compounded by absent revocation: `jti` is minted and never checked, so a token remains valid up to 60 minutes after logout (`:77`). F-016's new checks read `sub` and `role` from whatever token arrives; there is no audience scoping behind them, so a token obtained through any flow is a universal key for its lifetime.
- **Severity:** **MEDIUM**
- **DREAD breakdown:** Damage **M** · Reproducibility **H** · Exploitability **M** (requires obtaining a token first) · Affected users **all** · Discoverability **M**
- **Mapped frameworks:** OWASP API Top 10 **API2:2023 Broken Authentication**; CWE-613 (insufficient session expiration), CWE-863
- **Current mitigation status:** Partial — RS256 with `ValidAlgorithms = ["RS256"]` blocks algorithm confusion, `ClockSkew = Zero` removes the grace window, and asymmetric signing means only Identity can mint (`13-security.md:53-67`). The *validation* is strict; the *scoping* is absent.
- **Proposed action (party recommendation):** **Mitigate later**
  - Introducing an `aud` claim and enabling `ValidateAudience` requires coordinated changes across Identity's minting and all seven validators — and a token-format change during a feature that deliberately excludes Identity. Revocation needs a denylist store, which the per-process `AddDistributedMemoryCache()` cannot back (`00-overview.md` finding 7).
  - **Owner: F-023 `token-revocation`**, whose feature record already names the `aud`/`ValidateAudience` decision as in-scope.
  - **Requires an ADR** in `DECISIONS.md` recording the accepted risk, per the deferral rule.
- **Decision (human, at Step 12 approval):** **Confirmed — mitigate now, add the `actor` field.** Accepted knowing it costs the clean no-migration rollback.
- **Cross-talk note:** Phantom solo; Neo confirmed the deferral boundary (Identity is excluded from this feature by design, ARCHITECTURE §7).

---

## Threats Noted but Not Prioritized

| ID | Title | STRIDE | Boundary | Why deprioritized |
|---|---|---|---|---|
| T-NL-1 | Timing side channel in `OrdinalIgnoreCase` email comparison could confirm an email's existence | Information Disclosure | TB-2 | Not practically exploitable over HTTP against a Mongo round-trip; the noise floor is orders of magnitude above the signal. |
| T-NL-2 | `{email}` in route paths reaches telemetry as PII | Information Disclosure | TB-6 | Already mitigated: `PiiRedactingProcessor` strips email patterns from `url.path`, `url.query`, `url.full`, `http.url`, `http.target` and the span name (F-013, threat T-004). **This feature introduces no new route shape**, so existing coverage holds. ⚠️ Do not remove the processor. |
| T-NL-3 | `GET /api/v1/customers/{email}` remains a 200-vs-404 enumeration oracle for authenticated callers | Information Disclosure | TB-2 | Deliberate — ARCHITECTURE J1 records the decision to keep 404 for consistency with the eight existing call sites. Severity drops sharply once the route requires auth, and T-003's role fix reduces it further. |
| T-NL-4 | `totalCount` discloses aggregate dataset size to any authorized caller | Information Disclosure | TB-2 | Business-intelligence signal only, and it is what makes correct client pagination possible. Subsumed by T-003's role fix. |
| T-NL-5 | Integration harness exhausts the 2-CPU / 4.1 GB Rancher VM | Denial of Service | TB-5 | Developer-workstation resource contention, not a production threat. Mitigation is fewer, larger test classes (ARCHITECTURE §6). |
| T-NL-6 | `skip`/`limit` pagination degrades linearly with offset, enabling cheap load amplification at depth | Denial of Service | TB-2 | Immaterial at synthetic-data volumes. Recorded with a named trigger in `data-model.md` §5: revisit **before** real user data lands, because the keyset fix would change the contract F-015 consumes. |

---

## Open Questions for Human — ✅ all resolved at the Step 12 gate, 2026-08-18

**Resolutions:** Q1 → require the `Provider` role. Q2 → **delete the route.** Q3 → **add the `actor` field.**
Q4 → the guard is specified against the fixture's own container identity, which holds in CI as well as
locally, so no CI-specific work is needed. Q5 → `ISSUE-002` remains open and human-only; it is why T-002
is CRITICAL, and rotating the credential would materially shrink that blast radius.

Original text retained below for the audit trail.

1. **T-003 — is `GET /api/v1/customers` role-scoped, and how far?** The party's finding is that authentication alone leaves the full customer table extractable by anyone who self-registers. The minimum fix (`Provider` role) is a one-line change but is **scope beyond the approved PRD**, which asked only for authentication. The stronger fix — scoping results to the calling provider's own subscribed customers — is a genuine behaviour change. Which do you want: `Provider` role only, owner-scoped results, or accept as-is with an ADR?

2. **T-007 — what happens to `POST /api/v1/professions`?** Requirement 13 assumes a privilege tier that does not exist: the role allow-list is `{Provider, Customer}` with **no admin**. Delete the route (professions are seeded and nothing creates one), introduce an `Admin` role (correct but touches Identity, which this feature excludes), or accept `Provider`-only and record the residual risk?

3. **T-005 — add the `actor` field to `Event` now, or accept the accountability regression?** Reducing the audit payload without adding an actor makes the trail *less* useful for incident response than the PII dump was. This feature is the first point at which an actor exists to record. Adding it is one field plus one line per handler, but it makes `data-model.md`'s "no schema changes" claim false — which is a real trade, since a clean no-migration rollback is currently one of this feature's better properties.

4. **T-002 — does the harness's fail-closed guard need to hold in CI as well as locally?** The party specified it against the fixture's own container identity, which works everywhere. Worth confirming there is no CI path that supplies a shared database on the assumption that tests are read-only.

5. **Not a threat, but it governs several severities above:** `ISSUE-002` — the Atlas credential is still valid and still recoverable from this **public** repository's history. T-002's severity is CRITICAL largely because of that. Rotation is human-only and outside this feature, but it would materially reduce the blast radius of the one threat this feature creates.

---

## Approval Outcomes (filled in at Step 12)

**Approved by the maintainer 2026-08-18. All seven "mitigate now" recommendations confirmed; every open question resolved in favour of the stronger option.**

| Threat ID | Party recommendation | Human decision | Rationale |
|---|---|---|---|
| T-002 (CRITICAL) | Mitigate now | **Mitigate now ✓** | — |
| T-001 (HIGH) | Mitigate now | **Mitigate now ✓** | Confirms PRD requirement 18 moves from F-021 into F-016. |
| T-003 (HIGH) | Mitigate now — scope addition | **Mitigate now — require the `Provider` role** | Scope addition accepted. Chosen over owner-scoping (more work, deferred) and over accepting the risk. Blocks the self-registered-`Customer` extraction path outright. |
| T-004 (MEDIUM) | Mitigate now | **Mitigate now ✓** | — |
| T-005 (MEDIUM) | Mitigate now — needs schema change | **Mitigate now — add the `actor` field** | Accepted knowing it costs the clean no-migration rollback (Friday's dissent). Echo's point carried: with no log sink and `requestId` unexported, nothing outside `events` is durable, so there is no fallback attribution. |
| T-006 (MEDIUM) | Mitigate now (as a test) | **Mitigate now ✓** | — |
| T-007 (MEDIUM) | Mitigate now — option undecided | **Mitigate now — delete `POST /api/v1/professions`** | Neo's option over Atlas's. Professions are seeded from `ProfessionSeedData.cs` and no shipped flow creates one, so removing the surface beats guarding it — and it avoids inventing an `Admin` role in a feature that excludes Identity. **Requirement 13 is therefore superseded**: there is no role check to add because there is no route. |
| T-008 (MEDIUM) | Mitigate later → F-023 | **Mitigate later ✓** | ADR required. |

**ADR registry updates required:**
- ADR — accepted-risk record for **T-008** (no audience scoping, no revocation; deferred to F-023).
- ADR — **AD-1**: `UseExceptionHandler` moved outside the `IsDevelopment()` guard in six services, changing Production error behaviour (ARCHITECTURE §2).
- ADR — the **paginated response contract** (PRD AC-16, required before the endpoint work closes, because F-015 consumes it).
- ADR — for whichever option is chosen on **T-007**, and for **T-003**/**T-005** if the human accepts rather than mitigates.

**Tasks + security acceptance criteria to be created at Plan (Step 13):**

| Threat ID | Task | Testable `[security]` AC |
|---|---|---|
| T-002 | Fail-closed container-identity guard in `ServiceHostFixture` | Aborts during fixture construction against a non-container endpoint, names the host, creates no database |
| T-001 | Fix `AssertOwner` null-claim pass in `OwnershipGuard` | Null-`sub` token never receives the unprojected `ProviderEntity`; `AssertOwner(user, null)` throws |
| T-003 | Require the `Provider` role on `GET /api/v1/customers` | `Customer`-only token → 403, no records returned |
| T-004 | `AgendaBuddyExceptionHandler` emits no exception detail | 403 body in Production carries status/title/requestId only — no type, message, or stack frame |
| T-005 | `actor` on `Event`, populated from `sub` | Audit doc records the caller's `sub` and still contains no entity PII |
| T-006 | Guard-before-cache regression test on both Calendar routes | Cache warm for owner → different principal gets 403, no appointment data |
| T-007 | **Delete `POST /api/v1/professions`** (route, handler wiring, and its `RequestCollection`/`EventsHelper` path) | The route no longer exists: an authenticated `POST /api/v1/professions` returns 404/405, and no profession can be created through the API by any role |

---

## Revision History

| Date | Author | Change |
|---|---|---|
| 2026-08-18 | Phantom (initial draft) | Created at Step 10.5. Triage Full (3/3). 8 threats across 6 trust boundaries: 1 CRITICAL, 2 HIGH, 5 MEDIUM; 6 LOW noted. Five threats are introduced or newly reachable *because of* this feature (T-001, T-002, T-004, T-005, T-006). |
| 2026-08-18 | Neo (Step 12 approval) | Approval outcomes recorded. All 7 mitigate-now confirmed. T-003 → `Provider` role. T-007 → **route deleted**, superseding PRD requirement 13. T-005 → **`actor` field added**, superseding `data-model.md`'s "no schema changes". T-008 deferred to F-023 with an ADR. |
