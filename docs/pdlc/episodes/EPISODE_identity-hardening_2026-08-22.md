# Episode 003: Identity Hardening

**Episode ID:** 003
**Feature name:** Identity Hardening — a refresh that cannot destroy an account, a login that cannot be hammered, credentials that stop crossing plaintext unremarked
**Feature slug:** identity-hardening
**Feature ID:** F-021
**Date built:** 2026-08-22, on `feat/F-021-identity-hardening` — **PR [#39](https://github.com/ogdevlabs/agenda-buddy/pull/39)**, CI green (`build-and-test`, `Integration — real services + MongoDB`, `Mobile — Unit Tests`), mergeable
**Phase delivered in:** Construction
**Status:** **Draft** — not shipped. `main` was deliberately rolled back to `5ef3e10` to keep this work off
it, so the episode closes when the PR merges and `/ship` runs.

---

## What Was Built

F-016 proved that no endpoint leaks PII. F-021 makes the same kind of claim about the auth system itself,
and it closes three verified defects.

**1. A routine background operation could permanently destroy an account.** `RefreshAsync` deleted the
entire `CredentialEntity` and re-inserted it (`IdentityService.cs:135` → `:155`). Any fault between those
lines lost the email, password hash, role and reset flag irrecoverably — and because the re-insert sat inside
`catch … when (IsMongoDown(ex))`, **the destructive path was the handled path**: a transient database blip
returned a tidy 503 to a user whose account no longer existed. A mobile client refreshes hourly, so this was
not a rare path. It is now one `FindOneAndUpdateAsync` whose filter carries the presented token hash (single
use), the expiry check and a "not locked" condition; the document is never deleted.

**2. `POST /api/v1/auth/login` accepted unlimited attempts.** `AddRateLimiter` appeared nowhere in the
solution and `CredentialEntity` had no failure counter. There is now a per-IP sliding window on `login`
**and** `register`, plus a per-account counter and a lock that expires by itself.

**3. Credentials crossed plaintext with nothing telling the client not to.** All seven services registered
`UseHttpsRedirection` *after* `UseAuthentication`, so the bearer token was parsed out of a plaintext request
before the redirect was issued. Redirection now runs first in all seven — and HSTS was added alongside it,
because reordering does not fix what it appears to fix: by the time any middleware runs, the credential has
**already crossed the wire**.

**One measurement rewrote the feature's own threat story.** BCrypt verify at work factor 12 costs **262 ms**
on this hardware — measured, 20 iterations after JIT warm-up. That is 3.8 attempts/sec/core, so password
*guessing* was never the pressing threat. The mirror image is: every unauthenticated `login` or `register`
request **buys 262 ms of server CPU**, so roughly **4 requests/sec pins a core** and ~31/sec saturates the
machine — an unauthenticated denial of service against the service that issues the tokens all six others
validate. Two consequences followed: the limiter covers `register` too, and it must be evaluated before any
BCrypt work rather than behind the handler.

Suites went from 358 + 99 + 74 to **431 + 118 + 74 = 623**.

---

## Links

| Artifact | Path |
|---|---|
| PRD | [`PRD_F-021_identity-hardening_2026-08-22.md`](../prds/PRD_F-021_identity-hardening_2026-08-22.md) |
| Brainstorm | [`brainstorm_identity-hardening_2026-08-22.md`](../brainstorm/brainstorm_identity-hardening_2026-08-22.md) |
| Design | [`docs/pdlc/design/identity-hardening/`](../design/identity-hardening/) — ARCHITECTURE, data-model, api-contracts, threat-model, ux-review |
| Verification | [`verification.md`](../design/identity-hardening/verification.md) — 16/16 ACs, the red run, and eleven things this feature does *not* claim |
| Tasks | [`docs/pdlc/tasks/F-021/`](../tasks/F-021/) — T01…T07 |
| Decisions | ADR-032, ADR-033, ADR-034; **ADR-011 superseded** |

---

## Key Decisions & Rationale

**ADR-032 — one partial-update primitive on `IRepository<T>`, shared.** The underlying cause of defect 1 was
that no primitive could express "change this one field": `UpdateAsync` replaces the whole document. The new
member returns a post-image and **never upserts**, so "a failed login for an unknown address creates
nothing" is a property of the method rather than of every call site. Blast radius measured before writing
anything: exactly two implementers, both updated.

**ADR-033 — configuration-gated, not `IsProduction()`, with the AppHost declaring which run this is.** Every
service runs as **Production under the local AppHost**, so the intuitive switch is the wrong one: it would
emit HSTS for `localhost` (which browsers cache stickily and across projects) and throttle every local run.
The price of gating on configuration is threat T-103 — a deployment that never sets the keys ships without
the controls — so the AppHost injects `Security__Local=true` locally and turns both **on** in the cloud
graph, and each service warns at startup, naming the key, when a control is off outside a local run.
**Warn, do not fail fast:** a missing key should be visible and fixable, not downtime for the service six
others depend on.

**The HTTPS redirect is deliberately *not* flag-gated**, amending the design's "under their flags". Six
services already called it unconditionally, so a flag defaulting to off would have silently removed an
existing control, and one defaulting to on would be decorative.

**ADR-034 — the reflection guard forbidding a logger had to go.** `IdentityService_ConstructorParameters_
ContainNoILogger` asserted that `IdentityService` had **no** logger at all — a structural proxy for "no
credential material in logs", written when nothing in Identity logged anything, and in direct conflict with
requirement 17. Replaced by the stronger content assertion. Account identity is logged as `acct_` plus a
12-hex SHA-256 prefix, never truncation: `PiiRedactingProcessor` redacts **spans, not logs**, and F-013's
telemetry rollout is this project's own precedent for what happens otherwise.

**The lock is time-based and self-clearing, with no admin unlock.** F-022 does not exist, so a lock needing a
human to clear it would strand a real provider — and let an attacker strand one deliberately. A `lock_until`
in the past reads as unlocked, which means expiry costs **no write** and needs no sweeper.

**The lock is checked before `BCrypt.Verify`.** Otherwise a locked account costs 262 ms per attempt and the
lock amplifies the denial of service it sits beside.

---

## What the implementation corrected in its own approved design

| Artifact | Correction |
|---|---|
| `ARCHITECTURE.md` §3.2 | The flow needs one more step than drawn. Minting the access token needs the email and role that only the *matched document* supplies, so the signing key must be in hand before the write — otherwise a key failure discovers itself after the client's refresh token is already consumed. |
| `ARCHITECTURE.md` §4 | "calls `UseHsts()` and `UseHttpsRedirection()` under their flags" was not literally implementable for the redirect. Only HSTS is flag-gated; see ADR-033. |
| `data-model.md` §5 | Said the success path resets the counter as its own write. It is folded into the rotation write that already had to happen, so the success path adds **no** round trip — better than the PRD's "at most one extra write". |
| PRD AC-7 | Assumed a 401 has an empty body, so indistinguishability could be asserted as absence. `UseStatusCodePages` turns a bodyless 401 into ProblemDetails — the same surprise F-016 hit with its central 403. Asserted as *identical* bodies instead, which is the stronger claim. |
| `_feature.md` | Its standing warning that the limiter would break F-016's harness rested on a false premise — no harness test calls `login`; `TokenFactory` mints JWTs locally. Withdrawn at Define, re-verified at Construction. |
| `data-model.md` §4 | "unique index created by `seed-mongo.sh:39`" is effectively no constraint: no application code creates any index, and that script is documented as stale. Filed as `agenda-buddy-b0w`. |

---

## What the harness caught that nothing else could

**A rejected refresh answered 500 instead of 401.** Reading the signing key strictly at the top of
`RefreshAsync` made the key a precondition for the *reject* path too. Every unit test sets
`JWT_PRIVATE_KEY` in its constructor, so none could see it; the harness hosts Identity **without** one,
because `CryptoSessionFixture` deliberately never materialises a private key as a string in a public
repository. Fixed by reading the key without throwing and moving the throw behind the match.

That is the second time F-016's harness has paid for itself, and the failure had the same shape as the first:
not wrong logic, but logic that was only ever exercised under one configuration.

---

## Test Summary

| Layer | Required (§7) | Command | Result |
|---|---|---|---|
| 1 — Unit | **yes** | `dotnet test agenda-buddy-backend.slnf` | ✅ **431** passing / 0 failing / **0 warnings**, 12 projects (baseline 358) |
| 2 — Integration | no in §7, **yes by this PRD** | `dotnet test AgendaBuddy.IntegrationTests/…csproj` | ✅ **118** passing, **1 m 28 s** of a 600 s budget (baseline 99) |
| 3–6 — E2E / perf / a11y / visual | no | — | ⊘ no command in project; logged skips |
| 7a — Dependency audit | **yes** | `dotnet list package --vulnerable --include-transitive` | ⚠️ **1 HIGH**, unchanged: `SSH.NET` in `AgendaBuddy.IntegrationTests` only (ADR-030). **F-021 adds no package reference at all** |
| 7b — Secret scan | **yes** | 6 patterns over the changed files | ✅ clean |
| Mobile | — | `…/MobileApp.Tests.csproj /p:MobileWorkloads=false` | ✅ **74** (67 passing, 7 skipped), untouched |

**TDD held.** Every criterion was a failing test first: 14 reds across four new classes, all failing on
behaviour rather than compilation, because the interface member and the two optional constructor parameters
landed as signatures beforehand. Two reds were informative beyond going green — one caught that the test
double returned the *live* stored object rather than a snapshot, and one caught that three pre-existing
sanitization tests had never asserted anything.

---

## Known Tradeoffs & Tech Debt

| Item | Disposition |
|---|---|
| **One pre-existing test deleted** (ADR-034) | **Needs maintainer acknowledgement**, exactly as F-016's ADR-025 deletion did |
| **The per-IP limiter is per-process** (T-106, accepted) | With N Identity replicas an attacker gets N× the allowance. One replica exists, none is deployed. Re-evaluation trigger: the first multi-replica deployment, which cannot happen before F-017 |
| **Behind a proxy that does not forward the client address, all callers share one bucket** | `agenda-buddy-end`. Fails closed rather than open, which is the right direction, but the limit becomes global. Needs `UseForwardedHeaders` — F-017's topology |
| **A locked account answers faster than a wrong password** (T-NL-2, accepted) | Hiding the oracle costs 262 ms per locked attempt and re-arms T-101, a higher-severity threat |
| **HSTS is inert until TLS is terminated** (T-NL-3) | F-017. `preload` and `includeSubDomains` deliberately omitted — they are the hard-to-reverse parts |
| **No end-to-end HTTP test of register → login → refresh** | All three mint tokens, which needs a private key the harness deliberately does not hold. Rotation semantics are covered by unit tests *and* against real MongoDB; their composition is not |
| **AC-12 is a source-text assertion** | `IApplicationBuilder` exposes no ordered list of middleware. A `using`-alias or wrapper would evade it; the failure mode is a false pass, never a false failure |
| **`scripts/tasks.cjs` does not exist in this repository** | Though `docs/pdlc/tasks/index.md` says it generates that file. F-021's task store is hand-written, so the structural security-AC-to-test check could not run; each `[security]` AC names its test in the task body instead |
| **§7's security scan was satisfied by hand for the third consecutive feature** | **F-017** |
| **The standards gate was skipped for the seventh consecutive time** | Its sources have never resolved once. It needs a reachable source or an explicit retirement — folded into **F-017** |
| **`credentials` has no unique index on `email`** | `agenda-buddy-b0w`, confirmed and filed. Registration correctness, not hardening |

---

## Agent Team

| Agent | Role in this episode |
|---|---|
| **Solo** | ⚠️ The whole feature ran as **one model reasoning as each role**, because the session carried a standing instruction not to spawn agents — which overrides STATE's `Party Mode: agent-teams`. Same condition as every F-016 meeting. Fidelity is lower than independent context windows, and findings should be read with that in mind. Recorded rather than glossed. |

---

## Deployment Record

| Item | Detail |
|---|---|
| **Deployed to** | Nothing. Not merged, not tagged, not deployed. `main` is at `5ef3e10` by deliberate rollback |
| **Next action** | Human: review and merge the PR, then `/ship` |
| **Cloud** | Still blocked by the same three items, the first of which is the unrotated Atlas credential (`agenda-buddy-41s`) |
