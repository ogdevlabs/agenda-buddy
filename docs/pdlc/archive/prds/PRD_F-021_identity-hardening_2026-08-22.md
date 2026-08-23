# PRD: Identity Hardening

**Date:** 2026-08-22
**Status:** Approved
**Feature slug:** identity-hardening
**Feature ID:** F-021
**Episode:** *(assigned after delivery)*

---

## Overview

F-016 proved that no endpoint leaks PII. F-021 makes the same kind of claim about the auth system itself: that
signing in, staying signed in, and being defended while doing so all work correctly. It fixes three verified
defects — a token refresh that can permanently destroy a user's account, a login endpoint that accepts
unlimited password guesses, and credentials that cross the network in cleartext with no HSTS anywhere in the
solution.

This connects directly to `INTENT.md`'s launch criteria, which list **"Zero Sev-1 bugs — no data loss or
booking corruption bugs"**. The refresh defect is a data-loss bug on the one collection with no recovery
path: the Atlas cluster has **no backups**, and Identity writes no audit events and no log lines, so an
account lost this way leaves no trace of ever having existed.

**Claim: the auth system itself is safe, and the controls that make it safe are verifiable.**

---

## Problem Statement

Three problems, one owner.

**1. A routine background operation can permanently destroy an account.** `RefreshAsync` deletes the entire
`CredentialEntity` and re-inserts it (`Identity/Services/IdentityService.cs:135` → `:155`). Any fault between
those two lines loses the user's email, password hash, role and reset flag irrecoverably — and the
`catch (…) when (IsMongoDown(ex))` at `:157` means **the destructive path is the handled path**: a transient
database blip during a refresh returns a tidy 503 to a user whose account no longer exists. A mobile client
refreshes every hour, so this is not a rare path. The atomic delete *is* a correct single-use-token guard;
the defect is its granularity — it should target the embedded `refresh_token` subdocument, and
`IRepository<T>` offers no primitive that can.

**2. `POST /api/v1/auth/login` accepts unlimited attempts.** `AddRateLimiter`, `UseRateLimiter`,
`RequireRateLimiting`, `FixedWindow` and `SlidingWindow` have **zero occurrences** solution-wide, and
`CredentialEntity` has no failed-attempt counter and no lock field. Nothing slows a credential-stuffing run,
and nothing notices one.

**3. Credentials cross plaintext, and nothing tells the client not to.** All seven services register
`UseHttpsRedirection` *after* `UseAuthentication` — including Identity, which receives passwords. Reordering
is worth doing, but it does not fix what it appears to fix: by the time any middleware runs, the password or
bearer token has **already travelled in cleartext**. The control that prevents the next request from making
the same mistake is HSTS, and `UseHsts`/`AddHsts` appear nowhere.

---

## Target User

**Every user of every service**, so the `INTENT.md` primary persona — the independent service provider
managing 5–50 clients — plus their customers as secondary users. There is no narrower sub-group: item 1 hits
anyone whose client refreshes a token, item 2 protects every account, item 3 covers every request.

Two stakeholders deserve naming explicitly, because the design is shaped around them:

- **The provider who must not be locked out of their own business.** F-022 (password reset) does not exist, so
  a lock with no automatic expiry would leave a real provider with no way back in — and would let an attacker
  lock any provider out on purpose by guessing wrong. This is why the lock self-clears.
- **The developer running the stack locally.** Services run as **Production** under the AppHost, so a control
  gated on `IsProduction()` is a control that fires on every local run. This is why the gates are
  configuration, not environment.

---

## Requirements

**Refresh-token rotation**

1. The system MUST rotate a refresh token **without deleting** the `CredentialEntity`, using a targeted update
   of the embedded `refresh_token` subdocument.
2. The system MUST preserve **single-use** refresh-token semantics: the old token must be unusable after
   rotation, enforced atomically by a condition in the update filter rather than by a prior delete.
3. `IRepository<T>` MUST gain **one narrow partial-update primitive** — a filter plus an update document —
   available to all services. It MUST NOT grow into a general query-builder abstraction.
4. The in-memory repository used by unit tests MUST be able to **simulate a fault between read and write**, so
   the destruction scenario is expressible as a test. It cannot today (`11-testing.md:65`).
5. Refresh MUST be refused for an account that is currently locked.

**Login throttling and lockout**

6. `POST /api/v1/auth/login` MUST be rate-limited **per source IP** by rate-limiter middleware.
7. The system MUST count **consecutive failed login attempts per account**, incremented with the primitive
   from requirement 3. It MUST NOT read-modify-write the credential document, and it MUST NOT upsert — a
   failed attempt against an unknown email MUST NOT create a document.
8. After a threshold of consecutive failures, an account MUST be **locked for a bounded window** that
   **expires automatically**. There MUST be no permanent lock and no administrative unlock surface.
9. A `lock_until` value in the past MUST be treated as unlocked **without requiring a write**, and without any
   background job to clear it.
10. A successful login MUST reset the failed-attempt counter.
11. The per-IP limit MUST be evaluated **before** the per-account counter is written, so an unauthenticated
    caller cannot force unbounded writes to another user's document.
12. A throttled request MUST return **429** with `Retry-After`; a locked account MUST NOT reveal, through
    status code or body, whether the email exists.

**Transport**

13. `UseHttpsRedirection` MUST be registered **before** `UseAuthentication` and `UseAuthorization` in all
    **seven** services.
14. The system MUST add HSTS. It MUST NOT emit `Strict-Transport-Security` over a plain-HTTP endpoint.

**Gating and observability**

15. Rate limiting and HSTS MUST each be gated by **explicit configuration** —
    `Security:RateLimiting:Enabled` and `Security:Hsts:Enabled` — **not** by `IsProduction()`.
16. Both flags MUST default **off** for local AppHost runs, be **on** in the cloud configuration, and be
    switchable **on** by the integration harness so both controls are exercised by tests.
17. Credential mutations — create, rotate, lock, unlock, reset — MUST be logged with operation and outcome.
18. No log line MUST contain a raw email address. Email is PII (`CONSTITUTION.md` §4) and
    `PiiRedactingProcessor` redacts **spans, not logs**.
19. The login path MUST leave a **seam** for a future `MustResetPassword` check without implementing one.
20. Thresholds (attempt counts, window lengths) SHOULD be chosen from a **measured** BCrypt cost per attempt
    on this hardware, established during Design.

---

## Assumptions

1. **The refresh flow is exercised in practice.** The mobile client stores a refresh token; F-015 will wire
   the flow that uses it. Even unwired, `POST /api/v1/auth/refresh` is a live, reachable route.
2. **A single `IMongoClient` and a single Mongo deployment.** The partial-update primitive assumes
   document-level atomicity for one document, which MongoDB guarantees. No multi-document transaction is
   needed, and none is introduced.
3. **The integration harness will not be broken by the limiter.** Verified, not assumed: `TokenFactory` mints
   JWTs locally (`TokenFactory.cs:39,85-86`); no test calls the login route. The roadmap's warning to the
   contrary was a false premise.
4. **Per-account throttling cannot live purely in middleware.** ASP.NET resolves a rate-limiter partition key
   from `HttpContext` before model binding, and the account identifier is in the request body. The
   per-account half therefore belongs in `IdentityService`, against the same counter lockout needs — one
   mechanism, two consumers.
5. **`Production` is a local environment here.** Verified live: `/swagger/v1/swagger.json` returns 404 on all
   seven running services, because `AppHostWiring.cs` adds each project with `launchProfileName: null` and
   `launchSettings.json:9` sets `DOTNET_ENVIRONMENT=Development` for the AppHost process only.
6. ~~**BCrypt's work factor is currently unmeasured.**~~ **MEASURED at Design, 2026-08-22 — and it inverted
   the feature's threat story.** Work factor 12 costs **262 ms per verify** on this hardware (20 iterations
   after JIT warm-up, `BCrypt.Net-Next` 4.0.3) = **3.8 attempts/sec/core**, ~31/sec across all 8 cores. So
   password *guessing* was never the dominant threat; **CPU exhaustion** is — every unauthenticated login or
   register request buys 262 ms of server CPU, and ~4 req/sec pins a core. Two consequences: the limiter now
   covers **`register` as well as `login`** (it hashes at the same cost), and it must be evaluated **before**
   any BCrypt work. See `ARCHITECTURE.md` §2 and threat **T-101**.

---

## Acceptance Criteria

**Refresh-token rotation**

1. Given a valid refresh token, when the token is rotated, then the `CredentialEntity` still holds its
   original `email`, `password_hash`, `role` and `must_reset_password`, and only `refresh_token` has changed.
   `[security]` 🧪 test-first
2. Given a fault injected **between** the read and the write of a rotation, when the operation fails, then the
   credential document still exists and is unchanged. `[security]` 🧪 test-first
3. Given a refresh token that has already been used once, when it is presented again, then the response is
   401 and no second token pair is issued. `[security]` 🧪 test-first
4. Given a locked account, when a valid refresh token is presented, then the response is 401 and no token
   pair is issued. `[security]` 🧪 test-first
5. Given the new partial-update primitive, when it is called with a filter matching no document, then no
   document is created. 🧪 test-first

**Login throttling and lockout**

6. Given rate limiting enabled, when more than the configured number of login requests arrive from one IP
   within the window, then the excess requests receive **429** with a `Retry-After` header — asserted by an
   integration test against a **running service**, not a unit test on a policy object. `[security]`
   🧪 test-first
7. Given N consecutive failed logins for one account, when the next attempt is made, then it is refused as
   locked, and the response is indistinguishable from an ordinary failed login. `[security]` 🧪 test-first
8. Given a locked account, when the lock window has elapsed, then the next correct password succeeds — in a
   single test, with no intervening write and no background job. 🧪 test-first
9. Given a failed login for an email with no credential record, when the attempt is counted, then no document
   is created in the credentials collection. `[security]` 🧪 test-first
10. Given a successful login after some failures, when it completes, then the failed-attempt counter is zero.
    🧪 test-first
11. Given the counter increments, when inspected, then the write is a targeted atomic increment and not a
    whole-document replacement. 🧪 test-first

**Transport**

12. Given any of the seven services, when its middleware order is inspected, then `UseHttpsRedirection` is
    registered before `UseAuthentication`. 🧪 test-first
13. Given HSTS enabled, when a response is returned over TLS, then it carries `Strict-Transport-Security`;
    when returned over plain HTTP, it does not. `[security]` 🧪 test-first

**Gating and observability**

14. Given the AppHost's default configuration, when the stack starts, then neither the limiter nor HSTS is
    active, and repeated local requests to login are not throttled. 🧪 test-first
15. Given the harness sets both flags on, when the corresponding tests run, then both controls are exercised
    — i.e. neither control can ship unverified. `[security]` 🧪 test-first
16. Given any credential mutation, when the log output is inspected, then the operation and outcome are
    recorded and **no raw email address appears** in any line. `[security]` 🧪 test-first

*Threat IDs for the `[security]` criteria are assigned at the Design threat model (Step 10.5); each will be
linked to a test named `test_TNNN_…` so `tasks.cjs done` cannot close its task on a citation alone.*

---

## User Stories

**F-021-US-01 — A refresh never costs a provider their account** *(AC-1, AC-2, AC-3)*
**Given** a provider signed in on the mobile app, whose client refreshes its token hourly,
**When** the database blips during one of those refreshes,
**Then** the provider sees a transient failure and signs in again normally,
**And** their account — email, password, role — still exists.

**F-021-US-02 — A locked account cannot be bypassed with a live refresh token** *(AC-4)*
**Given** an account locked after repeated failed password attempts,
**And** an attacker holding a still-valid refresh token for it,
**When** the attacker presents that refresh token,
**Then** the request is refused.

**F-021-US-03 — Guessing a provider's password is slow and self-limiting** *(AC-6, AC-7, AC-9)*
**Given** an attacker running a credential-stuffing list against `POST /api/v1/auth/login`,
**When** the attempts exceed the configured per-IP rate or the per-account failure threshold,
**Then** further attempts are refused with 429 or as a locked account,
**And** the responses reveal nothing about which emails exist,
**And** no credential record is created for the addresses tried.

**F-021-US-04 — A provider who forgets their own password is not locked out forever** *(AC-8)*
**Given** a provider who mistyped their password until the account locked,
**And** no password-reset flow exists in the product yet,
**When** they wait out the lock window,
**Then** their correct password signs them in — without an administrator, a support ticket, or a database edit.

**F-021-US-05 — Hardening does not obstruct the developer running the stack** *(AC-14)*
**Given** a developer running `dotnet run --project AgendaBuddy.AppHost`,
**When** they exercise the Bruno collection or `scripts/run-ios.sh` against the local services,
**Then** nothing is throttled and no `Strict-Transport-Security` header poisons `localhost`,
**Because** the controls are gated on configuration rather than on an environment name that means
"local" here.

**F-021-US-06 — Neither control can ship unverified** *(AC-15, AC-6, AC-13)*
**Given** the integration harness can host a service with the security flags enabled,
**When** the suite runs,
**Then** the limiter's 429 and the HSTS header are both asserted against a real running service,
**Because** a security control no test can reach is the exact failure F-016 was created to end.

---

## Testing Approach: Test-Driven Development (TDD)

**Tests are written first.** During Construction (`/build`), for **every acceptance criterion above**, a
**failing test is written and run before any implementation code** — the Red → Green → Refactor cycle:

1. **Red** — write the smallest failing test that pins the acceptance criterion, named with the
   Given/When/Then language from the matching user story. Run it; confirm it fails for the right reason
   (logic not implemented — not a syntax/import error).
2. **Green** — write the minimum implementation that makes the test pass. Run the test and the full suite; no
   regressions.
3. **Refactor** — clean up without changing behavior; suite stays green.

The build loop enforces this at a mandatory **TDD gate** (build Step 9a-bis): implementation code for a
criterion may not be written until a failing test for it exists. The only exceptions are pure scaffolding,
config-only, and infrastructure-only work — and even those require an **explicit human TDD override**. There
is no silent skip. (TDD can be disabled only by editing `CONSTITUTION.md` § Test Gates — the Constitution
always wins.)

**Security acceptance criteria are enforced mechanically.** Any `[security]`-tagged criterion above
(threat-derived, materialized on its task via `tasks.cjs ac add`) is not just governed by the prose gate:
`node scripts/tasks.cjs done` **structurally refuses** to close a task whose `[security]` AC has no linked
test. Name each security test after its threat id (`test_TNNN_…`) and link it with `tasks.cjs ac link-test`.

**Test layers** for this feature: **Unit** (required, §7) and **Integration** (`AgendaBuddy.IntegrationTests`
— not a §7-required gate, but AC-6, AC-13 and AC-15 are **only meaningful** against a running service, so it
is required *by this PRD*) plus **Security** (§7's always-required dependency-audit + secret-scan gate).
E2E, performance, accessibility and visual regression have no command in this project and do not apply.

⚠️ **A fault-injection capability is new.** AC-2 cannot be written against today's `InMemoryRepository`
(`11-testing.md:65`). Requirement 4 exists to make AC-2 expressible — that test capability must land before
the AC-2 implementation, or TDD has nothing to bite on. This is the same trap F-016 hit with T-004, where a
mitigation was "verified" by citation because no test could reach it.

---

## Non-Functional Requirements

**Security**

- The per-IP limit must be evaluated before any per-account write (requirement 11), so the counter cannot be
  turned into a write-amplification vector against a cluster that has **no backups**.
- Locked and non-existent accounts must be indistinguishable in status code, body and — as far as practical —
  timing. Identity already carries a timing mitigation for enumeration (threat T-005); lockout must not
  reintroduce the difference it removed.
- No new secret material, and no change to JWT signing.

**Performance**

- A successful login adds at most one extra document write (the counter reset), and only when the counter is
  non-zero.
- A failed login adds exactly one atomic increment.
- The `lock_until`-in-the-past path adds **no** write (requirement 9).

**Operability**

- Both controls are switchable without a redeploy of code — configuration only.
- Turning both flags off must return the services to exactly today's behaviour, so the feature is trivially
  revertible in an incident.

**Constraints inherited from `CONSTITUTION.md`**

- Business logic stays in the `Library` service layer, not in API handlers (§ Coding Conventions).
- All data access through the repository pattern — which is precisely why requirement 3 adds a primitive
  rather than reaching for `IMongoCollection` in `IdentityService`.
- New persisted fields carry `[BsonElement("snake_case")]` attributes.

---

## Out of Scope

- **`AssertOwner`'s null-claim pass.** Item 4 of the inherited scope — **already fixed by F-016** (T-001 /
  AC-21, regression tests at `OwnershipGuardTest.cs:116,128`). Excluded because it is merged, not because it
  is deferred.
- **Authorization-failure logging** (review advisory A-1) — F-021 logs *credential* mutations only. Broad
  authz logging stays with F-024.
- **Token revocation / `jti` denylist** — **F-023**. It needs its own design decision on the denylist store
  and per-request check cost.
- **Password reset, change, or forced-reset enforcement** — **F-022**, which needs `NotificationService` from
  F-014. F-021 leaves a seam at the login path (requirement 19) and nothing more. Note for F-022:
  `SeedAuthCredentials.cs:68` already writes `MustResetPassword = true` for migrated users and nothing reads
  it.
- **An administrative unlock surface** — deliberately excluded; the lock expires instead.
- **CAPTCHA, device fingerprinting, or anomaly detection** — disproportionate for a pre-launch product with
  synthetic data.
- **TLS termination, certificates, and the deployment topology that makes HSTS meaningful in production** —
  **F-017** owns the container and CD story. F-021 adds the header and its switch, not the infrastructure.

---

## Known Risks

| # | Risk | Disposition |
|---|---|---|
| R1 | **`IRepository<T>` is a shared interface** compiled against by 7 services and 12 test projects; F-019/F-020 will rewrite this layer. | Accepted, with a **blast-radius review at Design** — the same treatment F-016 gave its 19 changed symbols (which found 0 at-risk callers). Keeping the primitive narrow is the mitigation. |
| R2 | **The failed-attempt counter turns a read path into a write path** on the credential collection — the one document this feature protects. | Mitigated by requirement 7 (atomic increment, never read-modify-write) and requirement 11 (per-IP limit first). AC-11 asserts the write shape. |
| R3 | **Thresholds chosen wrong** — too tight locks real providers who mistype; too loose defends nothing. | Requirement 20 measures BCrypt cost at Design so the number has a basis. The auto-expiring lock bounds the cost of being wrong. |
| R4 | **Config flags are a footgun** — a deployment that forgets to set them ships with both controls off, silently. | Design must decide whether the cloud configuration asserts them on, or whether a startup check logs loudly when they are off outside local. Carried as an open Design question. |
| R5 | **The unrotated Atlas credential** (`agenda-buddy-41s`) means a leaked credential still grants write access regardless of anything here. | Out of F-021's control; human-only. Recorded because it caps what this feature can claim. |
| R6 | **Solo-mode meetings.** Every Discover meeting ran as one model reasoning as each role, not independent agents. | Recorded, as at F-016. Fidelity is lower; findings should be read with that in mind. |

---

## Standards Alignment

_Standards readiness was **not** assessed at Define._ The `nordstrom-standards-readiness` plugin is installed,
but its six source standards repositories do not resolve under this `gh` authentication, there is no local
`.nordstrom-standards/` cache, and no prior `docs/standards-readiness/` report exists to `--delta` against —
all three re-checked on 2026-08-22. Skipped with notice per the advisory tier; the Plan gate will attempt the
design-level `--design` check.

⚠️ **This is the sixth consecutive gate this condition has blocked** (F-013 ship · F-018 Define · F-016 Define
· F-016 Plan · F-016 Review · F-021 Define). A gate marked `enforcing` that has never once executed is
governance theatre. It needs a reachable source — SSO/VPN access, or a vendored `.nordstrom-standards/` — or
an explicit decision to retire it. The standing recommendation is to fold that decision into **F-017**, which
already owns CONSTITUTION §7's unimplemented scan gate.

---

## Design Docs

All five artifacts are in `docs/pdlc/design/identity-hardening/`:

- [`ARCHITECTURE.md`](../design/identity-hardening/ARCHITECTURE.md) — the four changed places, the BCrypt measurement that reordered the feature's priorities, both data flows, and 9 architectural decisions
- [`data-model.md`](../design/identity-hardening/data-model.md) — two new fields on `CredentialEntity`, no migration, no new index; write patterns for every F-021 operation
- [`api-contracts.md`](../design/identity-hardening/api-contracts.md) — no new endpoints; `429` added to two routes, `401` gains a new cause, plus the configuration surface
- [`threat-model.md`](../design/identity-hardening/threat-model.md) — **Full** triage (3/3). Six threats, five to mitigate now, one accepted; five deprioritized
- [`ux-review.md`](../design/identity-hardening/ux-review.md) — **Skip** triage (0/3), no UI surface; carries one client obligation forward to F-015

`verification.md` is produced at Construction, not Design.

---

## Related Episodes

- **Episode 002 — `secure-public-endpoints` (`v0.2.0`)**: built the integration harness this PRD depends on
  for AC-6, AC-13 and AC-15, fixed F-021's original item 4, and established the pattern that a security claim
  is worth only as much as the test that reaches it.
- **Episode 001 — `aspire-wiring` (`v0.1.0`)**: source of the AppHost environment behaviour that makes
  requirement 15 necessary.

---

## Approval

**Approved by:** ogdevlabs
**Date:** 2026-08-22
