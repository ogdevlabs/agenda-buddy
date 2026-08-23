# Verification — Identity Hardening (F-021)

**Date:** 2026-08-22 · **Branch:** `feat/F-021-identity-hardening`
**PRD:** [`PRD_F-021_identity-hardening_2026-08-22.md`](../../prds/PRD_F-021_identity-hardening_2026-08-22.md)

**Claim under test: the auth system itself is safe, and the controls that make it safe are verifiable.**

---

## 1. Suites

| Suite | Command | Before | After |
|---|---|---|---|
| Backend unit | `dotnet test agenda-buddy-backend.slnf` | 358 | **431** (+73) |
| Integration | `dotnet test AgendaBuddy.IntegrationTests/AgendaBuddy.IntegrationTests.csproj` | 99 | **118** (+19) |
| Mobile | `dotnet test MobileApp.Tests/MobileApp.Tests.csproj /p:MobileWorkloads=false` | 74 (67 + 7 skipped) | **74**, untouched |
| **Total** | three commands, no single one runs all | 531 | **623** |

0 failing, 0 warnings, 0 skipped in the backend and integration suites. Integration duration **1 m 28 s**
against the 600 s budget `F-016-T20` enforces in CI.

---

## 2. Acceptance criteria

Every criterion below was written as a **failing** test before the implementation existed. The red run is
recorded in §4 — 14 failing tests across four new classes, all failing on behaviour rather than on
compilation, because the interface member and the two optional constructor parameters landed first as
signatures.

### Refresh-token rotation

| AC | Criterion | Test | Verdict |
|---|---|---|---|
| 1 | Rotation changes only `refresh_token` | `IdentityRefreshRotationTest.Rotation_ChangesOnlyTheRefreshToken` + `CredentialUpdatePrimitiveTest.SettingTheRefreshSubdocument_LeavesEverySiblingFieldIntact` | ✅ |
| 2 | A fault between read and write leaves the credential intact | `IdentityRefreshRotationTest.Rotation_WhenTheWriteFaults_LeavesTheCredentialIntact` | ✅ |
| 3 | A replayed token is refused and issues no second pair | `Rotation_IsSingleUse_SoAReplayedTokenIssuesNothing` + `CredentialUpdatePrimitiveTest.AReplayedTokenHash_MatchesNothingOnceRotated` | ✅ |
| 4 | A locked account cannot refresh | `T104_Rotation_OnALockedAccount_IsRefused`, `T104_Rotation_ResumesOnceTheLockExpires` | ✅ |
| 5 | The primitive creates nothing when the filter matches nothing | `InMemoryCredentialRepositoryUpdateTest.AFilterThatMatchesNothing_WritesNothingAndCreatesNothing` + `CredentialUpdatePrimitiveTest.AFilterMatchingNothing_ReturnsNullAndCreatesNoDocument` | ✅ |

**AC-2 is the criterion this feature exists for, and it was unexpressible before it.**
`11-testing.md:65` recorded that `InMemoryRepository` could not simulate a fault between a read and a
write. It now has `FaultBetweenMatchAndWrite`, invoked after the filter matches and before any mutation —
exactly the window the delete-then-insert left open. The injected fault is a `MongoException`, so the test
reproduces the *handled* path the PRD singles out: the caller sees 503, and the question is whether the
account is still there. It is, and the old refresh token still works, so the client's retry succeeds.

### Login throttling and lockout

| AC | Criterion | Test | Verdict |
|---|---|---|---|
| 6 | Excess requests from one IP get 429 + `Retry-After`, against a running service | `AuthRateLimitTest.T101_RequestsBeyondTheAllowance_Get429WithRetryAfter` | ✅ |
| 7 | A locked account is refused indistinguishably from a wrong password | `IdentityLockoutTest.AfterTheThreshold_…_LooksIdenticalToAWrongPassword` + `AuthRateLimitTest.ALockedAccount_IsRefusedIndistinguishablyFromAWrongPassword` | ✅ |
| 8 | The lock expires with no write and no background job | `IdentityLockoutTest.WhenTheWindowElapses_TheCorrectPasswordSucceeds_WithNoUnlockWrite`, `ALockUntilInThePast_ReadsAsUnlockedWithoutAWrite` | ✅ |
| 9 | A failed login for an unknown email creates no document | `T102_AFailedLoginForAnUnknownEmail_CreatesNoDocument` | ✅ |
| 10 | A successful login resets the counter | `ASuccessfulLogin_ResetsTheCounterAndClearsAnyLock` | ✅ |
| 11 | The counter write is a targeted atomic increment | `T102_AFailedLogin_IncrementsTheCounterAtomically_NeverReplacingTheDocument`, `Rotation_NeverReplacesTheWholeDocument` | ✅ |

**AC-6 is asserted at the level the PRD demands.** A unit test on a policy object passes while the
middleware is unregistered or attached to no endpoint — which is not hypothetical, it is F-016's original
defect. `AuthRateLimitTest` starts a real Identity service, seeds a credential directly into MongoDB, and
drives the route until it answers 429. Four further cases pin the design decisions around it: `register`
is limited too (it hashes at the same cost), `refresh` deliberately is not, a throttled request moves the
failure counter by **zero** (threat T-102's ordering requirement), and the default configuration throttles
nothing.

**AC-7's assertion had to change shape.** The 401 body is *not* empty: `UseStatusCodePages` turns a
bodyless 401 into ProblemDetails — the same surprise F-016 hit with its central 403. Indistinguishability
is therefore asserted as **identical bodies**, after removing the per-request `requestId` that Identity's
`CustomizeProblemDetails` stamps from the current `Activity`. That is the stronger claim anyway.

**AC-11 is asserted on write *shape*, not only on effect.** `InMemoryCredentialRepository` records every
update document and counts whole-document replacements; the tests assert the counter write is
`{$inc: {failed_attempts: 1}}` and that replacements stay at **zero** for every F-021 path.

### Transport

| AC | Criterion | Test | Verdict |
|---|---|---|---|
| 12 | `UseHttpsRedirection` precedes `UseAuthentication` in all seven services | `TransportSecurityOrderTest` (7 + 7 + 1 cases) | ✅ |
| 13 | HSTS over TLS, never over plain HTTP | `ServiceDefaults.Tests.TransportSecurityTest` (unit) + `TransportSecurityHeaderTest` (running service) | ✅ |

**AC-12 is a source-text assertion, and that is a deliberate limitation.** Middleware order is not
observable from a built application — `IApplicationBuilder` exposes no ordered list, and by the time
anything can inspect the pipeline it is a composed delegate. The alternatives were hosting all seven
services (a container each, for a question about two lines) or asserting nothing. A `using`-alias or a
wrapper helper would evade the check; the failure mode is a false pass, never a false failure. The second
assertion — **no service calls `app.UseHttpsRedirection()` directly** — closes the likeliest regression,
which is not someone moving a line but someone adding one. A third case fails if an eighth service
appears without being listed.

### Gating and observability

| AC | Criterion | Test | Verdict |
|---|---|---|---|
| 14 | The AppHost's defaults activate neither control | `AppHostWiringTest.ALocalRunMarksItselfLocal_AndEnablesNeitherControl` (×7), `AuthRateLimitTest.AC14_…`, `TransportSecurityHeaderTest.AC14_…` | ✅ |
| 15 | The harness enables both, so neither ships unverified | `AuthRateLimitTest` + `TransportSecurityHeaderTest` (both via `StartService(settings:)`), `AppHostWiringTest.ACloudDeploymentEnablesHstsEverywhere` (×7) and `…TheLimiterForIdentityOnly` | ✅ |
| 16 | Mutations are logged; no raw address in any line | `T105_EveryCredentialMutation_IsLoggedWithItsOperationAndOutcome`, `T105_NoLogLine_ContainsAnEmailAddress`, `TheAccountReference_IsNotReversibleToAnAddress` | ✅ |

**AC-15 is the criterion that answers threat T-103**, and it has two halves that are easy to conflate.
The harness proving the controls *work* is one; the AppHost's cloud graph proving they are *switched on*
is the other. Only the second distinguishes "the feature was written" from "the feature is running", and
it is the half F-016's original defect failed. Both are asserted.

**Requirement 19's seam** exists and is nothing more: the successful-login record now carries
`must-reset {MustResetPassword} (unenforced — F-022)`, so the branch has an obvious home and an operator
can meanwhile see that accounts flagged for reset are signing in without one. `SeedAuthCredentials.cs:68`
has been writing that flag since F-001 and nothing has ever read it. **No enforcement was added.**

---

## 3. Deviations, and everything this does not claim

1. **One pre-existing test was deleted.** `IdentityService_ConstructorParameters_ContainNoILogger`
   asserted by reflection that `IdentityService` had **no** logger — a structural proxy for "no credential
   material in logs" that directly contradicts PRD requirement 17. Replaced by the stronger content
   assertion. **This needs maintainer acknowledgement**, exactly as F-016's ADR-025 deletion did. Full
   reasoning in ADR-034.
2. **Three pre-existing tests were vacuous and now are not.**
   `Login_ValidCredentials_DoesNotLog{Password,AccessToken,RefreshToken}` iterated a logger factory wired
   to nothing, so they asserted over an empty list and could not have failed. They now run against real
   output, with `Assert.NotEmpty` so they cannot silently return to vacuity.
3. **A latent order dependency in an F-016 test was exposed and fixed.**
   `TelemetryPiiTest.RedactionPreservesThePathShape` took the *first* non-empty `url.path` from a
   process-wide exporter. That passed only while it was the sole class in the assembly starting a server;
   F-021 added a second, and it began failing on some runs and passing on others — asserting a path that
   belonged to another test's route. It now selects by `http.route`, the discipline its sibling test
   already documented. Confirmed stable over five consecutive runs.
4. **No integration test drives `register` → `login` → `refresh` end to end over HTTP.** All three mint
   tokens, which needs `JWT_PRIVATE_KEY`, and `CryptoSessionFixture` deliberately never materialises a
   private key as a *string* (F-016 AC-3 — this repository is public, and `ISSUE-002` is its own standing
   proof that deleting a secret from the working tree does not delete it from history). Rotation semantics
   are covered instead by unit tests over the in-memory implementer **and** by
   `CredentialUpdatePrimitiveTest` against real MongoDB. What is genuinely not covered end to end is the
   composition of the two. Stated rather than glossed.
5. **HSTS is inert until TLS is terminated somewhere.** The header instructs a *future* request to use
   HTTPS; there is no deployed TLS endpoint, and F-017 owns that. Correct to add now (T-NL-1), and
   `includeSubDomains`/`preload` are deliberately omitted because those are the hard-to-reverse parts.
6. **The per-IP limit is per-process** (threat T-106, accepted). With N Identity replicas an attacker gets
   N× the allowance. There is one replica and no deployment; the per-account counter, which lives in
   MongoDB, is unaffected either way. Re-evaluation trigger: the first deployment with more than one
   replica.
7. **A locked account answers faster than a wrong password** (T-NL-2, accepted). The lock is checked
   *before* `BCrypt.Verify`, so it costs no CPU — which is a measurable oracle for "this address exists
   and is locked". Hiding it means spending 262 ms per locked attempt, which re-arms T-101, a
   higher-severity threat. The trade is deliberate.
8. **Behind a proxy that does not forward the client address, every caller shares one limiter bucket.**
   `RemoteIpAddress` is null in that case and requests are filed under a single `unattributed` partition —
   correct for a CPU-exhaustion control, which must not fail open, but it means the limit is global rather
   than per-client until `UseForwardedHeaders` is configured. **F-017's concern**, recorded here because
   this feature is what makes it matter.
9. **The generated OpenAPI specs were not regenerated, and would not change.** No route was added or
   removed and no metadata changed; the handlers return `IResult` without `Produces`, so the specs already
   advertise only `200` for these routes. `api-contracts.md` §Summary flagged this in advance — the spec
   will not show `429`, and that under-documentation is pre-existing (F-018's T16/T17 own spec drift).
10. **No `Security` keys were added to any `appsettings.json`.** The defaults live in code, and the
    startup warning names the exact key to set, which is a better discovery path than six redundant
    `false` values to keep in sync. The configuration surface is documented in `api-contracts.md` §5.
11. **The missing unique index on `credentials.email` was confirmed, not fixed** (`data-model.md` §4,
    `agenda-buddy-b0w`). Precisely: **no application code creates any index** — so a database provisioned
    by the AppHost, a standalone run or the harness has nothing beyond `_id`. The one `createIndex` in the
    repository is `scripts/seed/seed-mongo.sh:39`, a script the README, `14-glossary.md` and ADR-013 all
    record as **stale** (it hardcodes `mongo:27017` and seeds databases no service reads), so the
    constraint exists on no path anyone actually uses. Registration correctness, not hardening; F-021
    changed nothing about it.

---

## 4. The red run

Captured before any behaviour was implemented, with the interface member, the options class and the two
optional constructor parameters in place so the failures were about logic rather than compilation:

```
Identity.Tests: Failed: 14, Passed: 65, Total: 79

IdentityRefreshRotationTest.Rotation_WhenTheWriteFaults_LeavesTheCredentialIntact
IdentityRefreshRotationTest.T104_Rotation_OnALockedAccount_IsRefused
IdentityRefreshRotationTest.T104_Rotation_ResumesOnceTheLockExpires
IdentityLockoutTest.T102_AFailedLogin_IncrementsTheCounterAtomically_NeverReplacingTheDocument
IdentityLockoutTest.AfterTheThreshold_TheAccountIsLocked_AndTheRefusalLooksIdenticalToAWrongPassword
IdentityLockoutTest.ALockedAccount_SpendsNoBcryptAndTakesNoFurtherWrite
IdentityLockoutTest.WhenTheWindowElapses_TheCorrectPasswordSucceeds_WithNoUnlockWrite
IdentityLockoutTest.ASuccessfulLogin_ResetsTheCounterAndClearsAnyLock
LoginLogSanitizationTest.T105_EveryCredentialMutation_IsLoggedWithItsOperationAndOutcome
LoginLogSanitizationTest.T105_NoLogLine_ContainsAnEmailAddress
LoginLogSanitizationTest.Login_ValidCredentials_DoesNotLog{Password,AccessToken,RefreshToken}
InMemoryCredentialRepositoryUpdateTest.ItReturnsThePostImage_…
```

Two of those reds were informative beyond going green:

- **`ItReturnsThePostImage_…` failed against my own test double**, because it returned the *live* stored
  object rather than a copy — so two successive post-images compared equal, which is the opposite of what
  the post-image guarantee is for. MongoDB returns a deserialized document; the double now returns a
  snapshot.
- **`Login_ValidCredentials_DoesNotLogPassword` went red the moment it was wired to a real logger**, on
  the `Assert.NotEmpty` guard rather than on the password. That is the assertion that proved those three
  tests had never been testing anything.

---

## 5. Security scan (CONSTITUTION §7 — always required)

Run by hand, as at F-013 and F-016. **F-017 still owns automating it.**

- **Dependency audit** — `dotnet list package --vulnerable --include-transitive`: one vulnerable package
  solution-wide, `SSH.NET 2024.2.0` (HIGH, `GHSA-q939-rpr3-3284`), in `AgendaBuddy.IntegrationTests` only.
  Unchanged from F-016 and dispositioned by **ADR-030** (unreachable, and the unreachability is *tested*).
  F-021 adds **no new package reference** to any project: rate limiting and HSTS are both in the ASP.NET
  Core shared framework.
- **Secret scan** — the six patterns F-016 used, over the changed files: clean. No PEM payload, no
  connection string, no assigned-secret literal. The one new persisted field pair (`failed_attempts`,
  `lock_until`) holds no secret material, and the refresh token is still stored only as SHA-256.
- **New log sink reviewed against §4** — this is the one place F-021 could have introduced a PII leak, and
  it is asserted rather than reviewed: no log line may contain an address, its local part, a password or
  either token (AC-16).

---

## 6. Live verification at the Ship gate — 2026-08-22

Run against a live stack after the merge, because "the tests pass" and "the feature works" are different
claims and F-016's ship gate established that this project checks the second one. Two configurations:
the **Aspire AppHost** (all seven services, both controls off — the default a developer meets) and
**Identity standalone in `Production` against the AppHost's MongoDB** with both controls **armed**, which
is the only way to observe them in a real process rather than in `TestServer`.

### 6.1 The regression risk of reordering seven pipelines

| Check | Result |
|---|---|
| All 7 services under the AppHost | ✅ `/health` = `Healthy`, `/alive` = 200 on every one, after the transport-security reorder |

### 6.2 The end-to-end flow no integration test covers (deviation 4, now closed by observation)

Against a live Identity + MongoDB:

| Step | Expected | Observed |
|---|---|---|
| `POST /register` | 201 + token pair | ✅ 201 |
| `POST /refresh` with that token | 200, a **different** refresh token | ✅ 200, rotated |
| `POST /refresh` replaying the consumed token | 401 | ✅ 401 |
| `POST /login` with the original password | 200 — **the credential survived rotation** | ✅ 200 |
| The stored document afterwards | every field intact, `failed_attempts: 0`, `lock_until` **field absent**, exactly one document | ✅ all four |

That last row is the whole feature in one line: before F-021 this sequence had a window in which the
document did not exist at all.

### 6.3 Threat T-103's mitigation, observed in both directions

| Configuration | Expected | Observed |
|---|---|---|
| Under the AppHost (`Security__Local=true`) | silent — "off" is deliberate here | ✅ zero warnings |
| Standalone `Production`, no local marker, both flags unset | warn, naming each key | ✅ both warnings, verbatim: *"HSTS is OFF: set `Security:Hsts:Enabled=true`…"* and *"Rate limiting is OFF: set `Security:RateLimiting:Enabled=true`. login and register each spend ~262 ms of CPU on BCrypt per request…"* |
| Standalone with both flags on | silent | ✅ zero warnings |

### 6.4 The limiter, in a real process with a real client address

`TestServer` leaves `RemoteIpAddress` null, so the harness could only exercise the `unattributed`
partition. This run has an actual address.

| Check | Result |
|---|---|
| Requests beyond a 3/minute allowance | ✅ 400, 400, then **429** for every subsequent request |
| `Retry-After` on the 429 | ✅ `Retry-After: 60` |
| BCrypt spent on a throttled request | ✅ none — the bodies were invalid, and the limiter answers before validation, so a throttled caller gets 429 rather than 400 (as `api-contracts.md` §2 predicted) |

### 6.5 Lockout and indistinguishability, live

Threshold set to 3 for the run:

| Step | Expected | Observed |
|---|---|---|
| 3 wrong passwords | 401 each | ✅ 401, 401, 401 |
| The **correct** password afterwards | 401 — refused by the lock | ✅ 401 |
| Wrong-password body vs locked body | identical apart from `requestId` | ✅ byte-identical after removing it: `{"status":401,"title":"Unauthorized","type":"…rfc9110#section-15.5.2"}` |
| The stored document | `failed_attempts: 3`, `lock_until` set, every other field intact, one document | ✅ all four |

### 6.6 AC-16, and the check that stops it being a vacuous pass

The first attempt at this **was** vacuous and is worth recording: grepping the AppHost console for the
address returned zero occurrences — but so did grepping it for `credential.`, because Aspire streams
service logs to the dashboard over OTLP and not to the AppHost's stdout. Zero occurrences of a leak in a
file containing none of the relevant output proves nothing. Repeated against a real Identity process whose
console was captured:

| Check | Result |
|---|---|
| `credential.*` lines present at all | ✅ **6** — the non-vacuity guard |
| What they say | `credential.created ok for acct_01fa6a06332a as Customer` · three `credential.login-failed wrong-password … N consecutive` · `credential.locked … until … after 3 consecutive failures` · `credential.login-failed locked for acct_… until …` |
| The address anywhere in the log | ✅ 0 |
| Its local part anywhere | ✅ 0 |
| The character `@` anywhere | ✅ 0 |
| The password anywhere | ✅ 0 |

The last log line is worth reading twice: it names the **lock**, not a wrong password, which is the lock
check firing *before* `BCrypt.Verify` (D-9) observed rather than argued.

### 6.7 What the live run could not check

- **HSTS over TLS.** No HTTPS listener is configured locally, so only the negative half is observable
  live: with the flag **on**, a plain-HTTP response carries no `Strict-Transport-Security` (✅ confirmed).
  The positive half is covered by `ServiceDefaults.Tests.TransportSecurityTest` and
  `TransportSecurityHeaderTest`, both of which issue `https://` requests. Nothing here can close it —
  F-017 owns TLS termination.
- **Lock expiry.** The window is 15 minutes at its shortest useful setting; the unit tests advance a fake
  clock instead. AC-8 is a test claim, not a live one.

### 6.8 An empirical upgrade to a filed finding

`db.credentials.getIndexes()` on the live AppHost database returns exactly `["_id_"]`. `agenda-buddy-b0w`
was filed from a grep for `CreateIndex`; it is now **observed** on a running database. Nothing enforces
one credential per email anywhere in a path this project actually uses.

---

## 7. What a reviewer should look at first

1. **`IdentityService.RefreshAsync`** — one filter now carries the single-use guard, the expiry check and
   the lock check. If any condition leaves that filter, a defect returns silently.
2. **`IRepository<T>.FindOneAndUpdateAsync`'s no-upsert property** — AC-9 depends on it, and it is
   expressed as the *absence* of an option. `CredentialUpdatePrimitiveTest` is what stops that being a
   comment.
3. **The ordering in `LoginAsync`**: lock check before `BCrypt.Verify`, counter write only on the
   verify-failed path. Reversing either is a plausible "simplification" that re-arms T-101 or breaks the
   proof that the short circuit fires.
4. **`AppHostWiring`'s cloud branch** — the only thing that distinguishes "written" from "switched on".
