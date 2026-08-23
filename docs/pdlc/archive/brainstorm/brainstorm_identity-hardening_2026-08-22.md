---
feature: identity-hardening
date: 2026-08-22
status: prd-approved
last-updated: 2026-08-22T16:40:00Z
approved-by: ogdevlabs
approved-date: 2026-08-22T16:10:00Z
prd: docs/pdlc/prds/PRD_F-021_identity-hardening_2026-08-22.md
---

# Brainstorm Log: Identity Hardening

**Feature ID:** F-021 · **Priority:** 15 · **Program:** Platform Remediation (2nd of 6)
**Claim:** the auth system itself is safe.

> **Inherited scope.** F-021 was split out of F-016 at the program Discover on 2026-08-18 because F-016 grew
> past one PRD. Its four items were each verified against code at that Discover and are restated in
> `docs/pdlc/tasks/F-021/_feature.md`. This log re-verifies them before treating any as a premise — this
> project has had two Discover premises collapse on inspection (the MAUI-workload concern and the
> OTLP-suppression inference), and F-016 found three counting errors in already-approved artifacts.

## Premise Re-verification (2026-08-22, before any questioning)

All four inherited items were checked against the merged code at `2134b8d`. **Three hold, one is already
fixed, and two are materially mis-stated.**

### 1. `RefreshAsync` delete-then-insert destroys accounts — ✅ CONFIRMED, anchors exact

`Identity/Services/IdentityService.cs`: `FindOneAndDeleteAsync` at **`:135`**, `InsertAsync` at **`:155`**,
`catch (…) when (IsMongoDown(ex))` at **`:157`**. The whole `CredentialEntity` — email, password hash, role,
`MustResetPassword` — is deleted and re-inserted. Any fault between the two lines loses the account, and the
`:157` catch converts exactly that fault into a `ServiceUnavailableException`, so **the destructive path is
the handled path**. Identity does not use the EventStore and nothing logs it, so there is no record either.

**Stated root cause also confirmed:** `IRepository<T>` has no partial-update primitive — `UpdateAsync` and
`UpdateByIdentifierAsync` both take a whole entity; there is no `$set`/`$unset`. So the fix needs a new
repository capability, not just a call-site change. The atomic delete *is* a correct single-use-token guard;
the bug is its granularity — it should target the embedded `refresh_token` subdocument.

### 2. `UseHttpsRedirection` ordering — ⚠️ CONFIRMED BUT MIS-STATED, twice

- **It is 7 services, not 6.** Every service registers `UseHttpsRedirection` *after*
  `UseAuthentication`/`UseAuthorization`, **including Identity** — the one that receives passwords.
  Identity's is conditional (`if (!app.Environment.IsDevelopment())`, `Identity/Program.cs:107-108`); the
  other six are unconditional (`Booking:114-117`, `Calendar:115-118`, `Customer:114-117`,
  `Provider:119-122`, `Services:115-118`, `Profession:114-117`).
- **The anchors in the feature record are stale** — it cites `Booking/Program.cs:83-86`; F-016 moved that
  block to `:114-117`.
- ⚠️ **And the stated fix does not achieve the stated goal.** "Redirection must precede authentication" is
  correct hygiene, but by the time *any* middleware runs, the request — password or bearer token — has
  **already crossed the network in cleartext**. Reordering stops the server from *acting* on a credential
  that already leaked; it does not prevent the leak. The control that actually helps is **HSTS**, and
  `UseHsts`/`AddHsts` appear **nowhere in the solution**. This needs to be settled at Define: reorder *and*
  add HSTS, or reorder alone and record what it does and does not buy.

### 3. No rate limiting, no lockout — ✅ CONFIRMED, and larger than recorded

`AddRateLimiter`, `UseRateLimiter`, `RequireRateLimiting`, `FixedWindow`, `SlidingWindow`: **zero
occurrences** solution-wide. Additionally, **lockout needs a schema change** — `CredentialEntity` has
`Id`, `Email`, `PasswordHash`, `Role`, `MustResetPassword`, `RefreshToken` and **no failed-attempt counter
and no lock-until field**. Rate limiting is middleware; lockout is persistence. They are not one task.

### 4. `AssertOwner` null-claim pass — ❌ **ALREADY FIXED. Drop from scope.**

F-016 fixed it as T-001 / AC-21. `AssertOwner` now delegates to a shared `IsOwner`, which requires
`sub is not null && entityEmail is not null` before comparing, and `Library.Tests/Tools/OwnershipGuardTest.cs`
carries the regression tests (`:116`, `:128`) that keep `AssertOwner` and `AssertOwnerAny` from diverging
again. Carrying this item forward would mean planning work that is already merged.

### 5. "The rate limiter will break F-016's harness" — ❌ **FALSE PREMISE**

`ROADMAP.md` and the feature record both warn that the limiter needs a test-environment escape "or it will
break F-016's harness, which authenticates repeatedly against `POST /api/v1/auth/login`". **The harness never
calls that route.** `AgendaBuddy.IntegrationTests/Harness/TokenFactory.cs:39,85-86` mints JWTs locally with
the session keypair (`SigningCredentials` + `JwtSecurityToken`); the only mention of `auth/register` in the
suite is a doc comment. So no test authenticates against the login endpoint at all.

**This makes the constraint easier, not harder** — and it removes the reason to fear an escape hatch. It also
means the *opposite* risk is the live one: a limiter nothing exercises is an unverified security control,
which is the exact pattern F-016 existed to end. Fortunately `ServiceHostFixture` takes an explicit
environment (`:92-96,119`) and nine test classes already host services as **`Production`** on purpose, so a
Production-gated limiter **remains reachable by the harness**. Both can be true.

### Adjacent finding, not in F-021's record

`CredentialEntity.MustResetPassword` is **written but never read** — and `SeedAuthCredentials.cs:68` sets it
**`true`** for migrated users. So every migrated account is flagged for a forced reset that nothing enforces;
they simply log in with the seeded password. This belongs to **F-022** (password reset), which is downstream
of F-014 for `NotificationService`. Recorded here so the seeding half is not forgotten when F-022 starts.

---

## Scope decisions taken at Discover (2026-08-22, maintainer)

| # | Decision | Rationale |
|---|---|---|
| 1 | **Reorder middleware in all 7 services AND add HSTS** | Reordering alone does not prevent the leak it is credited with preventing — the credential has already crossed the wire before any middleware runs. `UseHsts`/`AddHsts` exist nowhere in the solution. Scope is slightly larger; the security claim becomes true rather than plausible. |
| 2 | **Keep F-021 to three items** — no A-1 authz logging, no F-023 token revocation pulled forward | Item 4 (`AssertOwner`) is already merged, and the slot is left empty rather than backfilled. Smaller PRD. A-1 stays with F-024, F-023 keeps its own design decision on the denylist store. |
| 3 | ~~Rate limiter gated on Production~~ → **SUPERSEDED by decision 7 within the same Discover.** | Chosen when Production was assumed to mean "deployed". Pulse's finding disproved that: services run as Production under the local AppHost, so this gate would have thrown HSTS onto `localhost` and throttled every local run. Kept visible rather than rewritten, because the reason it changed is the useful part. |
| 4 | **Lockout is time-based with automatic unlock** — no permanent lock, no admin unlock path | F-022 does not exist, so a permanent lock leaves a real provider with no recovery. It is also attacker-triggerable: guess any provider's password wrong N times and they are locked out. A cooling-off window defends against guessing without handing anyone a denial-of-service arm. |
| 5 | **Throttle on both keys — per-IP and per-account** | Per-IP catches broad brute force; per-account catches distributed guessing at one victim, which per-IP cannot see. Per-IP must sit **in front of** the per-account counter, so an unauthenticated caller cannot force unbounded writes to a stranger's document (Phantom). |
| 6 | **Add a narrow partial-update primitive to `IRepository<T>`** — shared, not Identity-only | F-014 wires six capabilities that all currently read-modify-write, and F-019/F-020 rewrite this layer; one narrow primitive now is the cheapest point. Constraint: filter + update document, nothing more, so F-019 inherits a primitive rather than a leaky abstraction. Blast radius across 7 services and 12 test projects to be reviewed at Design. |
| 7 | **Gate the limiter and HSTS on explicit configuration** — `Security:RateLimiting:Enabled`, `Security:Hsts:Enabled` — off under the AppHost, on in cloud, **on deliberately in the harness** | Environment is not a usable switch in this project: services run as **Production locally** (verified — swagger 404s on all seven). Configuration separates *intent* from *environment*, keeps local development unobstructed, and — critically — keeps both controls reachable by the integration harness, which is what stops them from being unverified security theatre. |
| 8 | **Measure BCrypt cost per attempt during Design** | The threshold should come from a real attempts-per-second figure on this hardware, not from convention. The PRD will not claim a sized threat until it is sized. |

**Interaction mode:** Sketch (CONSTITUTION §8) — drafts proposed, maintainer confirms.

## Divergent Ideation

_Skipped — deliberate._ Divergent ideation generates 100+ ideas to widen a solution space. F-021's scope is
four named defects with verified `file:line` anchors, one of which turned out to be already fixed; the work is
convergent by construction. The skill marks "skip" as the recommended default for exactly this shape.

## Socratic Discovery

**Round 1 — Problem Statement.** Two of the four canonical questions were **dropped as already answered** by
the catalog and INTENT, per the Sketch-mode rule:

- *Who will use this, in what context* — `INTENT.md` Target User: the independent service provider, 5–50
  active clients. Auth affects every user of every service; there is no narrower sub-group.
- *Technical constraints* — established by the premise verification above: `IRepository<T>` has no
  partial-update primitive (`Library/Repositories/IRepository.cs:5-15,39`), the harness signs its own tokens,
  `CredentialEntity` has no lockout fields, and nothing anywhere registers a rate limiter or HSTS.

**Q1 — What problem does this feature solve?** *(drafted)*
Three distinct problems that share one owner, the auth system:
1. **Silent, unrecoverable data loss.** A refresh — the routine background operation a mobile client performs
   every hour — can permanently destroy a user's account, with no audit record and no log line. `INTENT.md`'s
   success table lists "**Zero Sev-1 bugs — no data loss** or booking corruption bugs" as a launch criterion;
   this is a data-loss bug on the credential store, which is the one collection with no recovery path (the
   Atlas cluster has **no backups**).
2. **Unlimited credential guessing.** Nothing rate-limits or locks `POST /api/v1/auth/login`, so an attacker
   gets unbounded attempts against BCrypt-hashed passwords of unknown strength.
3. **Credentials crossing plaintext.** No HSTS, and redirection is registered after authentication in all 7
   services.
*Source: `IdentityService.cs:135,155,157`; `INTENT.md` What Success Looks Like; premise verification above.*

**Q3 — What does success look like?** *(drafted, deliberately hard to game)*
- **No code path can delete a `CredentialEntity` while intending to update it** — enforced by a test that
  faults the write *between* the two operations, which is only possible once the partial-update primitive
  exists (the current `InMemoryRepository` cannot simulate a mid-operation fault — `11-testing.md:65`).
- **`POST /api/v1/auth/login` returns 429 after a defined threshold**, verified by a **Production-hosted**
  integration test, not a unit test.
- **HSTS present on all 7 services in Production**, asserted on a response header.
- **A locked-out or throttled user still has a way back in** — see the open question below.

**Round 2 — Future State / Key Capabilities.** Settled by the maintainer's decisions; no questions needed.

1. **A refresh rotates the refresh token without ever deleting the credential.** `RefreshAsync` targets the
   embedded `refresh_token` subdocument through a new **narrow partial-update primitive on
   `IRepository<T>`** (maintainer decision: shared, not Identity-only, so F-014's six capabilities inherit it
   rather than re-solving read-modify-write). The atomic single-use-token guarantee is preserved — the
   condition moves into the update filter instead of a delete.
2. **`POST /api/v1/auth/login` is throttled on two keys — per-IP and per-account.** Per-IP catches broad brute
   force; per-account catches distributed guessing at one victim.
3. **A failed-attempt counter with a time-based auto-unlock window.** No permanent lock and no admin unlock
   surface, deliberately: F-022 does not exist, so a permanent lock would leave a real provider with no way
   back in, and would let an attacker lock out any provider by guessing wrong N times. Requires new fields on
   `CredentialEntity` (it has none today).
4. **Credential mutations are logged** — Identity has no log sink at all today. Enough to leave a trace if a
   write ever fails again.
5. **Middleware reordered in all 7 services, plus HSTS.**

**Round 3 — Acceptance Criteria.** The hard ones, carried into Define:

| # | Criterion | Why it resists gaming |
|---|---|---|
| AC-a | A fault injected **between** the token-rotation read and write leaves the credential intact | Only expressible once the partial-update primitive exists — today's `InMemoryRepository` cannot simulate a mid-operation fault (`11-testing.md:65`) |
| AC-b | `POST /api/v1/auth/login` returns **429** past the threshold, proven by a **Production-hosted** integration test | A unit test on a policy object would pass while the middleware is unregistered |
| AC-c | The counter increments **without** read-modify-write on the credential document | Otherwise the lockout feature reintroduces the destruction class it ships beside |
| AC-d | An account locks, then **unlocks itself** after the window, in one test | Proves the recovery path exists rather than asserting the lock |
| AC-e | `Strict-Transport-Security` present on all 7 services when enabled | Header assertion, not a code-review claim |
| AC-f | No log line contains a raw email address | `CONSTITUTION.md` §4 classifies email as PII, and `PiiRedactingProcessor` redacts **spans, not logs** |

---

## Progressive Thinking (Agent Team Meeting)

⚠️ **Ran in `solo` mode** — one model reasoning as each role, because this session carries a standing
instruction not to spawn agents. That overrides STATE's `Party Mode: agent-teams`. Fidelity is lower than
independent context windows; recorded as F-016 recorded the same condition for all of its meetings.

**Concrete → Inferential → Consequential → Speculative → Conflicting → Strategic.** The findings that changed
the feature, rather than a transcript:

**Pulse (DevOps) — Concrete: "Production" is the local environment.** ⚠️ **The most consequential finding of
this Discover.** Gating the limiter and HSTS on `IsProduction()` does **not** mean "cloud only". Verified
empirically at 2026-08-22 against the running AppHost: `/swagger/v1/swagger.json` returns **404 on every
service**, because Swashbuckle registers only in Development. `AgendaBuddy.AppHost/Properties/launchSettings.json:9`
sets `DOTNET_ENVIRONMENT=Development` for the **AppHost process itself**, and `AppHostWiring.cs` adds every
project with `launchProfileName: null` — so the child services inherit **no** `ASPNETCORE_ENVIRONMENT` and
default to **Production**. Consequences if F-021 gates on Production:
- **HSTS would be emitted on every local run.** Browsers cache HSTS per host, and a poisoned `localhost`
  entry persists across projects and is awkward to clear — a developer-hostile, sticky failure.
- **The limiter would throttle local development** — the Bruno collection, `scripts/run-ios.sh`, and the ship
  smoke tests all hammer the same endpoints from one address.
- The one thing it would *not* do is protect the harness, which needs no protecting (premise 5).

**Bolt (Backend) — Inferential: per-account throttling cannot live in the rate-limiter middleware alone.**
ASP.NET's partition key is resolved from `HttpContext` **before** model binding, and the account identifier
lives in the JSON request body. Partitioning per-account therefore requires either request buffering in
middleware or — cleaner — implementing the per-account half **inside `IdentityService`** against the same
persisted counter that lockout already needs. Which means capabilities 2 and 3 collapse into one mechanism:
**one counter, two consumers.** That is a simplification worth carrying into Design, not a complication.

**Echo (QA) — Consequential: the counter turns every failed login into a write.** A read path becomes a write
path on the *credential* collection — the one document this feature exists to stop corrupting. It must use
the new `$inc`-style primitive, never read-modify-write, or the lockout feature reintroduces item 1's bug
class beside item 1's fix. AC-c exists for this.

**Phantom (Security) — Speculative: the counter is itself an attack surface.** An unauthenticated caller can
force writes to any account's document by submitting bad passwords for a known email — cheap write
amplification against a cluster with no backups. Mitigation: the **per-IP** limiter must sit in front of the
per-account counter so the write is rate-limited before it happens. Ordering matters, and it is the reverse
of the intuitive reading.

**Jarvis (Tech Writer) — Conflicting: "log credential mutations" collides with §4.** Email is PII, logs are
**not** covered by `PiiRedactingProcessor` (spans only), and `Identity/Program.cs:100-102` already carries a
standing instruction not to add request-body logging without excluding the login and device-token routes.
Resolution: log the **operation and outcome** with a non-reversible identifier (hash prefix), never the
address. AC-f.

**Neo (Architect) — Strategic: the partial-update primitive outlives this feature.** F-014 wires six
capabilities that all currently have to read-modify-write; F-019/F-020 rewrite the repository layer. Adding
one narrow primitive now is the cheapest point to do it, and it is the maintainer's choice. Constraint: keep
it *narrow* — a filter plus an update document — so F-019 inherits a primitive, not a leaky abstraction.

## Adversarial Review

**Unstated assumptions, each either confirmed or refuted:**

1. ~~"A test-environment escape is needed or the harness breaks."~~ **Refuted** — premise 5.
2. ~~"`AssertOwner` still passes on a null claim."~~ **Refuted** — fixed by F-016.
3. ~~"Reordering middleware stops credentials crossing plaintext."~~ **Refuted** — the request has already
   arrived when middleware runs. HSTS is the control; reordering is hygiene.
4. ~~"It is 6 services."~~ **Refuted** — 7, including Identity.
5. ~~"`Production` means the deployed environment."~~ **Refuted** — it is also every local AppHost run.
6. **"BCrypt makes brute force impractical, so rate limiting is belt-and-braces."** *Unresolved and worth
   stating in the PRD rather than assuming either way.* The work factor is whatever
   `BCrypt.Net.EnhancedHashPassword` defaults to; nobody has measured cost-per-attempt on this hardware. The
   feature does not depend on the answer — unlimited attempts are indefensible regardless — but the PRD
   should not claim a threat it has not sized.

**Risks:**

- **R1 — The Production-gating trap** (see Pulse, above). Highest-likelihood way this feature ships and
  immediately annoys or blocks its own developers. Needs an explicit gating decision, below.
- **R2 — Write amplification on the credential collection** from the failed-attempt counter, against a
  cluster with **no backups** and an **unrotated credential** (`agenda-buddy-41s`).
- **R3 — Touching `IRepository<T>`** changes an interface all 7 services and 12 test projects compile
  against. F-016's blast-radius review found 0 at-risk callers for its 19 changed symbols; this one needs the
  same treatment at Design.
- **R4 — Migrated accounts.** `SeedAuthCredentials.cs:68` writes `MustResetPassword = true` and nothing reads
  it. If F-021 adds a login-time check for lockout, it is touching the exact code path where a
  `MustResetPassword` gate would eventually live (F-022). Leave a seam, do not implement it.

## Edge Case Analysis

| # | Edge case | Handling |
|---|---|---|
| E-1 | Refresh arrives with a valid token **while** the credential is mid-rotation | The update filter carries the single-use condition, so the second request matches nothing and gets 401 — same outcome as today, without the delete |
| E-2 | Fault **between** read and write | Cannot destroy the document once the write is a targeted update. AC-a asserts it under injected fault |
| E-3 | Locked account presents a **valid refresh token** | Refresh must respect the lock, or lockout is bypassable by any client holding a live refresh token — the mobile client holds one for 24 h |
| E-4 | Clock skew / `lock_until` in the past on read | Treat a past `lock_until` as unlocked without a write; never require a background job to clear it |
| E-5 | Rate limiter active during the ship smoke test | Directly caused by R1. The ship gate authenticates repeatedly from one address — this session's own Verify run would have been throttled |
| E-6 | Two AppHost services behind one NAT address in cloud | Per-IP limiting on a shared egress IP throttles unrelated users. Per-account is the arm that still works; another reason both are needed |
| E-7 | Counter increments on a **non-existent** email | Must not create a document, or an attacker seeds arbitrary credential records. Update-only, never upsert |
| E-8 | HSTS enabled on a service reached over plain HTTP locally | Header is only meaningful over TLS; must not be emitted on the local HTTP endpoint |

## UX Discovery

_Skipped — no UI surface._ F-021 changes middleware, a repository primitive, a persisted counter and log
output. The only user-visible artifacts are HTTP status codes (401/403/429) and, eventually, whatever F-015's
mobile client renders for them. Muse triage: 0/3.

## Capability Scope Check

_Skipped — standalone repo._ `node scripts/capability.cjs read --json` finds no `control-manifest.toml`; this
repo is not part of a pdlc-fy multi-repo capability, so there are no sibling repos to check scope against.

## External Context

_None ingested._ Every input came from the repository itself: the code at `2134b8d`, the context catalog, the
F-016 review and episode, and live probes against a running AppHost.

## Adversarial Review
_Not run._

## External Context
_None ingested._

## Discovery Summary

**Claim:** the auth system itself is safe — and the two controls that make it safe are switchable, so they are
on where they matter and off where they would only obstruct.

### What F-021 builds (three items, not four)

1. **Non-destructive refresh-token rotation.** A new **narrow partial-update primitive on `IRepository<T>`**
   (filter + update document) lets `RefreshAsync` rotate the embedded `refresh_token` subdocument instead of
   deleting and re-inserting the whole `CredentialEntity`. The single-use guarantee moves into the update
   filter, so atomicity is preserved. Fault-injected between read and write, the account survives.
2. **Login throttling on two keys, plus a self-clearing lock.** Per-IP in rate-limiter middleware; per-account
   against a new persisted failed-attempt counter on `CredentialEntity`, incremented with the new primitive
   (never read-modify-write). The lock is **time-based and auto-clearing** — no permanent lock, no admin
   unlock surface, because F-022 does not exist yet and a permanent lock would be an attacker-triggerable
   denial of service against a real provider. Per-IP sits **in front of** the per-account counter so an
   unauthenticated caller cannot force unbounded writes to a stranger's document.
3. **Transport ordering plus HSTS, in all 7 services.** `UseHttpsRedirection` moves ahead of
   `UseAuthentication`, and HSTS is added — because reordering alone does not stop a credential that has
   already crossed the wire, and `UseHsts`/`AddHsts` exist nowhere today.

Plus, at the maintainer's direction: **credential mutations are logged** (Identity has no log sink at all),
with no raw email in any log line — email is PII under §4 and `PiiRedactingProcessor` covers spans, not logs.

### What F-021 deliberately does not build

- **Item 4, `AssertOwner`'s null-claim pass** — already fixed by F-016 (T-001/AC-21, tests at
  `OwnershipGuardTest.cs:116,128`). Dropped, not deferred.
- **Authorization-failure logging (A-1)** and **token revocation (F-023)** — the empty slot from item 4 is
  left empty to keep the PRD small.
- **Forced-password-reset enforcement** — `MustResetPassword` is written (`SeedAuthCredentials.cs:68` sets it
  `true`) and never read. F-021 touches the same login path, so it leaves a **seam**, not an implementation.
  F-022 owns it.
- **A permanent lockout or admin unlock path.**

### The decision that shaped the design

**Environment is not a usable switch in this project.** Services run as **Production under the local
AppHost** — verified live: swagger 404s on all of them, because `launchSettings.json:9` sets
`DOTNET_ENVIRONMENT=Development` for the AppHost process only and `AppHostWiring.cs` adds every project with
`launchProfileName: null`. Gating on `IsProduction()` would have emitted HSTS on `localhost` (which browsers
cache stickily, across projects) and throttled every local run — the Bruno collection, `scripts/run-ios.sh`,
and this feature's own ship smoke test. So both controls are gated on **explicit configuration**
(`Security:RateLimiting:Enabled`, `Security:Hsts:Enabled`): off under the AppHost, on in cloud, and **on
deliberately in the harness**, which is what keeps them verifiable. That last part matters — F-016 exists
because this solution could not verify its own authz claims, and shipping a security control no test can
reach would repeat that.

### Open question carried into Design

- **BCrypt cost per attempt is unmeasured.** The maintainer chose to **measure it during Design** rather than
  pick a threshold by convention, so the per-IP and per-account limits are set from a real attempts-per-second
  figure on this hardware.

### Grounding

Every premise was re-verified against the merged code at `2134b8d` before being used. **Five of the inherited
statements were wrong or incomplete** — one item already fixed, one undercounted (7 services, not 6), one
whose stated fix does not achieve its stated goal, one warning based on an endpoint the harness never calls,
and one wrong reading of what `Production` means here. That is why this log leads with premise verification:
it is the third consecutive feature in this project where a stated premise did not survive inspection.

---

## Design Discovery (Bloom's Taxonomy)

**Sketch mode, 3 rounds condensed to one batched block** — most mechanics questions were already answered by
the PRD and the premise verification, so they were dropped rather than asked. Four decisions were taken.

### The measurement that came first (PRD requirement 20)

Before asking anything, BCrypt verify cost was **measured** rather than assumed: **262 ms** at work factor 12
(`BCrypt.Net-Next` 4.0.3, 20 iterations after a JIT warm-up, 8 logical cores) = **3.8 attempts/sec/core**,
~31/sec fully saturated. This **inverted the feature's threat story** and is why two of the four questions
below exist at all. Full reasoning in `ARCHITECTURE.md` §2 and threat **T-101**.

### Round 1 — Mechanics

| Q | Decision |
|---|---|
| Which routes does the limiter cover? | **`login` + `register`.** `register` hashes at the same 262 ms, so limiting only `login` leaves an equal-cost CPU-exhaustion vector open. `refresh` spends no BCrypt and stays unlimited |
| Where is the lock checked relative to `BCrypt.Verify`? | **Before.** A locked account must not cost 262 ms per attempt, or the lock amplifies the very DoS it sits beside. Consequence accepted: a locked account answers measurably faster, which is an enumeration oracle — recorded as **T-NL-2** and deliberately traded |

### Round 2 — Apply (tech-stack mapping)

| Q | Decision |
|---|---|
| Where do the ordering fix and HSTS live? | **`ServiceDefaults`** — one policy, one `UseAgendaBuddyTransportSecurity()` extension. ⚠️ **But ordering cannot be fully centralized:** middleware order is the sequence of `app.UseX()` calls in each `Program.cs`, and `AddServiceDefaults()` runs on the *builder*, before a pipeline exists. So it is **one implementation + seven one-line call-site moves**, and service #8 inherits the policy but must still place the call. Named in `ARCHITECTURE.md` §4 rather than implied away |
| Which layer holds the counter and lock logic? | **`IdentityService`**, per CONSTITUTION's "business logic in the Library service layer, not API handlers". `Program.cs` gains middleware registration only. The per-account half *must* live here anyway: ASP.NET resolves a limiter partition key from `HttpContext` before model binding, and the account is in the JSON body |
| Extend or build? | **Extend.** One new member on `IRepository<T>` (`FindOneAndUpdateAsync(filter, update)`) using the `BsonDocument` convention the interface already exposes on four other methods — no new abstraction style |

### Round 3 — Trade-offs and judgments

| Q | Decision |
|---|---|
| R4 — how to stop shipping with the flags silently off? | **Warn loudly at startup** when a flag is off and the service is not running locally. Chosen over fail-fast (a config slip would become an outage) and over relying on cloud config alone (invisible when wrong). Recorded as threat **T-103** |
| Thresholds, given 262 ms/attempt? | **Accepted the measured draft:** per-IP **10 req/min** sliding window on `login`+`register` (≈ 2.6 s CPU/min/IP); per-account lock after **10** consecutive failures for **15 minutes**, auto-clearing. A legitimate user needs 2–3 attempts, so ≈ 3× margin |

### Synthesis check

The design is internally consistent with the Discover decisions, with two honest residuals carried into the
approval gate: **T-NL-2** (locked accounts answer faster — an enumeration oracle traded away to avoid
re-arming T-101) and **T-106** (per-IP state is per-process, so N replicas grant N× the allowance — accepted,
re-evaluate at the first multi-replica deployment, which cannot happen before F-017).

## Threat Modeling Triage

- **Trust boundary changes:** yes — modifies the authentication surface itself (throttling, a lock consulted before credential verification, transport-security ordering across all 7 services)
- **Regulated data:** yes — email is PII (§4) and F-021 adds a **new log sink** that could carry it; also handles BCrypt password hashes on the rotation path
- **New attack surface:** yes — no new endpoint, but a new **unauthenticated-write path** (any anonymous caller can cause a write to another user's credential document) plus two flags whose "off" state silently disables a control
- **Triage tier: Full (3/3)** → `docs/pdlc/design/identity-hardening/threat-model.md`, six threats (T-101…T-106), five deprioritized (T-NL-1…T-NL-5)

## Design-Laws Audit Triage

- **Triage tier: Skip (0/3)** — no UI surface, no changed flow, no user-facing copy. Record at
  `docs/pdlc/design/identity-hardening/ux-review.md`, which also carries one client obligation forward to
  F-015 (`429` must respect `Retry-After`; a locked `401` cannot be labelled as "locked").
- **Step 10.7 Variant Convergence:** cannot fire — requires a Full 10.6 triage. Skipped with a record.
