---
feature: platform-remediation
date: 2026-08-18
status: inception-complete
last-updated: 2026-08-18T19:20:00Z
approved-by: ogdevlabs
approved-date: 2026-08-18
prd: docs/pdlc/prds/PRD_F-016_secure-public-endpoints_2026-08-18.md
scope: program-level Discover across F-014, F-015, F-016, F-017
anchor-claim: F-014
---

# Brainstorm Log: Platform Remediation (F-014 – F-017)

**What this is.** A single program-level Discover across four already-planned roadmap
features, run *before* committing to any one PRD. The user chose this over starting Inception
on one feature, so the deliverable is **correct scope and sequencing across all four**, not a
PRD. Precedent: `/brainstorm refactor-minimal-apis` (2026-08-18) established that an oversized
request should be decomposed at Discover rather than forced into one PRD; it produced
F-018/F-019/F-020.

**The four under review**

| ID | Slug | ROADMAP-stated scope |
|---|---|---|
| F-014 | `wire-unreached-services` | Register + route 6 shipped-but-unreachable capabilities |
| F-015 | `api-gateway-and-mobile-contract` | Make the mobile client reach the backend; gateway; `api/v1/` prefixes; refresh flow |
| F-016 | `secure-public-endpoints` | 6 anonymous PII endpoints; Calendar IDOR; `AssertRole`; pagination; central 403 |
| F-017 | `container-and-cd-hardening` | 3 unrunnable Dockerfiles; delete 3 library Dockerfiles; §7 scan gate; image CI |

---

## Divergent Ideation
_Not run — this is a remediation program over an inventoried defect set, not an open idea space.
The catalog already enumerates the problems; widening the idea space would add noise, not signal._

---

## Premise Verification (run before any questioning)

This project has a track record of ROADMAP/Discover premises collapsing on inspection — the
MAUI-workload concern and the OTLP-suppression inference were both **withdrawn as wrong** after
someone actually read the code. So every claim below was checked against
`[context-catalog]` = `docs/pdlc/context/` and against the tree, before asking the user anything.

### F-014 — premise **HOLDS**, verified

- All six capabilities exist as real implementations with interfaces in `Library/Services/`:
  `NotificationService.cs`, `MessageService.cs`, `NoteService.cs`, `PaymentService.cs`,
  `ReportingService.cs` (+ `StripePaymentGateway.cs`), each with an `I*.cs` alongside.
- `grep -riE "map(get|post|put|patch|delete).*(notification|message|note|payment|report)"` across
  all seven `Program.cs` returns **zero matches**.
- Corroborated independently by `01-api-surface.md`'s full endpoint inventory: the complete route
  table across all seven services contains no notification, message, note, payment or reporting
  route. F-006–F-010 are marked `Shipped` on code that has no HTTP surface.

### F-015 — premise **HOLDS, and is worse than the ROADMAP row says**

`01-api-surface.md:140-158` gives the contract-drift table. Two independent defects compound:

- **Path prefix:** every domain call omits `api/v1/` and uses singular collection names no route
  group declares (`GET booking?date=…` → Booking exposes **no GET at all**, `01-api-surface.md:51`).
- **Base address:** one `ApiBaseUrl` cannot address seven processes. `MobileApp/appsettings.json:2`
  is `https://localhost` (:443), `appsettings.Development.json:2` is `https://localhost:5001` —
  **neither matches any service port** (6030–6036), and no service has an HTTPS listener at all.
- **Net effect:** only the 3 Identity routes are reachable, and only after correcting the base URL.
  Every domain read 404s, which is why the ViewModels silently fall back to `SeedDataProvider.cs`.
- ⚠️ **New, not in the ROADMAP row:** ports are now **dynamically assigned by the Aspire AppHost**
  (F-013), so every hardcoded 603x number is wrong under the primary local run path. A gateway is
  no longer a nicety — without one there is no stable address for the client to hold.

### F-016 — premise **HOLDS but is badly under-scoped**

`13-security.md` "Open items, ranked" lists 12 items. F-016's row covers roughly items 3, 4, 8
and 10. The following are real, catalogued, `file:line`-anchored, and belong to **no** feature:

| Defect | Anchor | Why it matters |
|---|---|---|
| `RefreshAsync` delete-then-insert **permanently destroys accounts** | `IdentityService.cs:135`→`:155` | Any exception/termination between the two lines loses email + password hash + role, unrecoverably. No audit trail (Identity doesn't use EventStore), no logging. This is data loss, not hardening. |
| No password reset / change / forced-reset flow **at all** | `13-security.md:111` | `MustResetPassword` is written and never read. A user who forgets their password has **no recovery path**. |
| `UseHttpsRedirection` registered **after** `UseAuthentication` | `Booking/Program.cs:83-86` + 5 others | The bearer token is parsed from the plaintext request before the redirect is issued — the credential has already crossed the wire. |
| No rate limiting, no account lockout | `13-security.md:109` | `AddRateLimiter` appears nowhere. `POST /api/v1/auth/login` takes unlimited attempts. |
| No token revocation | `13-security.md:77` | `jti` minted, never recorded. Access token stays valid up to 60 min **after logout**. |
| Read queries write full PII into an unbounded EventStore | `GetProvidersQueryHandler.cs:23` | Every anonymous `GET /api/v1/providers` serialises the entire provider list — every appointment, every customer email — into an unindexed, never-pruned `events` collection with no retention policy and no actor field. **This compounds F-016's exposure rather than being separate from it.** |
| No data-subject-rights capability | `13-security.md:216` | Cancel hard-deletes from `appointments` but the same appointment survives embedded in the provider document *and* in the `events` blobs. Any erasure request is unsatisfiable. |
| `AssertOwner` null-claim asymmetry | `OwnershipGuard.cs:9-10` | `string.Equals(null, null)` is `true`, so the guard **passes** on a null claim. `AssertOwnerAny` handles this; `AssertOwner` doesn't. |
| Double-booking is unprevented | `13-security.md:236` | Nothing checks `Start < End`, that `Start` is future, or slot overlap. `INTENT.md` names double-booking as a **core user frustration** — this is a product defect sitting in the security file. |
| `services.BuildServiceProvider()` during DI registration | `AuthenticationExtensions.cs:54` | ASP0000, in **all seven** services. Builds a throwaway container, double-instantiates every prior singleton, leaks them for process lifetime — to emit one log line. |

### F-017 — premise **HOLDS**

`00-overview.md:80` confirms `Library/Dockerfile:13`, `Kafka/Dockerfile:13` and
`EventAndCommands/Dockerfile:12` publish `net10.0` output onto a `dotnet/runtime:8.0` base — a
leftover the F-011 .NET 10 upgrade missed. Those three images cannot run. The §7 dependency-audit +
secret-scan gate remains mandated-but-unimplemented (`13-security.md:269`); it was run **by hand**
at the v0.1.0 ship, which does not discharge it.

---

## Socratic Discovery
_In progress — Round 1 below._

### The reframing this verification forces

Two structural findings change the shape of the program, and both cut against roadmap order:

**1. F-016 is substrate, not a peer feature.** F-014's job is to add six new route families —
including `NoteService` (therapy/coaching session notes, which `13-security.md:208` identifies as
*the most sensitive data in the product*) and `PaymentService` (financial). The substrate those new
routes would be built on is, verifiably:

- `ForbiddenException.StatusCode => 403` is **never read** — correct 403s depend on each endpoint
  hand-writing `try/catch`, repeated at 8 call sites, with **no compile-time protection**. A new
  guarded endpoint that forgets the `try/catch` returns **500 instead of 403**
  (`13-security.md:139`).
- `AssertRole` is **never called anywhere** (`13-security.md:137`), so nothing role-gates anything.
- Every command *and query* copies its full payload into the unbounded `events` collection.

Building six new capabilities — two of them handling the most sensitive data in the system — on
that substrate means **shipping new exposure**, then retrofitting. F-016's "central 403 mapping" and
"actually call `AssertRole`" are exactly the primitives F-014's six new route families would consume.

**2. F-015 is partly blocked by an F-013 side-effect nobody has re-scoped for.** The AppHost now
assigns ports dynamically, so "fix the three wrong base URLs" is no longer a coherent task — there
are no longer three fixed URLs to be right about. The gateway moves from *nice-to-have* to
*prerequisite*.

### Round 1 decisions (user, 2026-08-18)

1. **Sequencing — F-016 whole first**, then F-014 → F-015 → F-017. (Options to split F-016 or keep
   roadmap order were both declined.)
2. **Orphan defects — absorb the critical ones** into the right feature; file the rest.

**Unprompted supporting argument for the chosen order:** authn and pagination on the list endpoints
are **breaking contract changes**, and right now they have **no consumer** — the mobile client cannot
reach those routes at all (`01-api-surface.md:158`). Doing F-016 before F-015 changes the contract
while nothing depends on it. Ordering F-015 first would mean writing the mobile client against a
contract F-016 then breaks.

### Orphan-defect triage (per decision 2)

**Absorbed — critical:**

| Defect | Home | Why critical |
|---|---|---|
| `RefreshAsync` delete-then-insert account destruction | F-016 | Unrecoverable data loss, unlogged, untestable by the current suite (`11-testing.md:65`) |
| `UseHttpsRedirection` after `UseAuthentication` | F-016 | Bearer token crosses plaintext before the redirect. 6-file ordering swap — near-zero cost |
| `AssertOwner` null-claim passes the guard | F-016 | One-line fix in the exact file F-016 already opens; latent auth bypass |
| Read queries serialise full PII into the unbounded `events` collection | F-016 | Compounds the very exposure F-016 closes; fixing the endpoint without this leaves the copy |
| No rate limiting / account lockout on login | F-016 | Anonymous endpoint, unlimited attempts, cheap to fix (`AddRateLimiter`) |
| Double-booking unprevented | F-014 | `INTENT.md` names it a **core user frustration**. F-004 is marked Shipped while permitting it — the same "shipped but doesn't work" class F-014 exists to fix. **Debatable placement — flagged for the user.** |

**Filed, not absorbed:**

| Defect | Why not now |
|---|---|
| No password-reset / change flow | This is a **new capability**, not a defect fix — needs endpoints, tokens, and delivery. Delivery means `NotificationService`, which **F-014 wires**. So it is genuinely downstream of F-014. → new roadmap feature. |
| No token revocation (`jti` denylist) | Needs a design decision (denylist store, per-request check cost). Not a one-task fix. → new roadmap feature. |
| No data-subject-rights / erasure | Large product capability; cluster is confirmed synthetic-only, so no live obligation. → new roadmap feature. |
| `services.BuildServiceProvider()` (ASP0000, all 7) | A leak and a smell, not exposure. **F-019/F-020 rewrite these `Program.cs` files anyway** — fixing it there avoids doing it twice. → note on F-019. |

---

## Progressive Thinking (Agent Team Meeting)

Run in **solo** mode — the session carries a standing "do not call the Agent tool unless requested"
instruction, which overrides STATE.md's `Party Mode: agent-teams`. Recorded, not silently
substituted. Six progressive rounds, compressed to findings that would actually change the plan.

**Concrete (Bolt).** F-016's mechanical change set: add `.RequireAuthorization()` to 4 routes
(`Provider/Program.cs:132,150`, `Customer/Program.cs:146,160`), add `OwnershipGuard` to
`Calendar/Program.cs:93,121`, wire `AssertRole` on `Provider/Program.cs:100` and
`Profession/Program.cs:93`, add pagination to 2 list endpoints, register a central
`ForbiddenException`→403 mapping in 7 pipelines, and stop `GetProvidersQueryHandler.cs:23`
serialising the dataset into `events`. `GET /api/v1/professions*` stays anonymous — reference data,
defensible (`01-api-surface.md:110`).

**Inferential (Pulse).** The harness has a hardware constraint F-018's plan measured but this program
inherits: container startup is **4.45 s warm** (the spike figure that reversed container-per-test to
container-per-class, ADR-017), and the Rancher VM is **2 CPUs / 4.1 GB already running a k8s
cluster**. F-016 will add test classes across 5 services. Container-per-class × N classes on that VM
is the practical ceiling, not the 4.45 s itself. Also: `docker` is not on `PATH` under Rancher
(`~/.rd/bin`), which Testcontainers shells out to.

**Consequential (Atlas).** **Pagination is a contract change, and F-015 must be written against the
new contract.** F-016 therefore has to *record* the paginated response shape as a decision, not just
implement it — otherwise F-015 designs the mobile client against a shape F-016 already replaced. This
is a hard hand-off artifact, not a nicety.

**Speculative (Echo).** The harness sits on the **critical path of a live security fix**. If it slips,
the exposure stays open. Mitigating fact: F-018 already spiked both gating risks and **both passed** —
Testcontainers works on Rancher with zero configuration, and spec generation needs no container. So
the residual risk is execution, not feasibility. That is a meaningfully different risk profile from
"build a harness and hope."

**Conflicting (Muse vs Phantom) — unresolved, escalated to Define.** Phantom wants
`GET /api/v1/providers` authenticated. Muse asks what happens to **provider discovery**: a customer
choosing a coach has to list providers, and if that route requires a token, browse-before-signup
dies. `INTENT.md` frames the product around providers managing *their own* clients, which suggests
public browse was never a use case — but nothing states it. **This is the top open question for the
F-016 PRD and must not be silently resolved by the implementer.** Options: authenticate it outright ·
authenticate + add a slim anonymous public-profile projection · keep anonymous but strip the embedded
appointment book and customer list (which alone removes most of the exposure).

**Strategic (Neo).** This program **changes F-018's shape**. F-016 consumes 6 of F-018's 20 tasks
(T01, T05, T06, T08, T09, T14). On resume, F-018 is ~14 tasks — CI wiring, OpenAPI + spec drift,
`.editorconfig`, constitution amendments, mobile CI, the tier tests — and its plan, dependency graph,
and AC set all need amending. This must be written into `.paused-feature.json`, or whoever resumes
F-018 rebuilds a harness that already exists.

---

## Edge Case Analysis

| Edge case | Assessment |
|---|---|
| Existing anonymous consumers break when the 4 GETs require auth | **None exist.** The mobile client cannot reach any domain route (`01-api-surface.md:158`), and there is no web client. Zero-consumer breaking change — the best possible moment. |
| Wiring `AssertRole` breaks dev/seed flows | `DevelopmentSeedData` creates `Provider` and `Customer` accounts with roles, so role checks should hold. But `SeedAuthCredentials` is **dead code** (`13-security.md:113`) — any pre-auth provider/customer record cannot authenticate at all, so a role check will surface that latent breakage rather than cause it. Worth expecting, not fearing. |
| Pagination default page size unstated | Must be decided in the F-016 PRD, not left to the implementer — it becomes the contract F-015 consumes. |
| `AssertOwner` null-claim fix changes behaviour for callers relying on the bug | Nothing can rely on it: reachable only via a null route value, which ASP.NET generally prevents (`13-security.md:135`). Pure hardening. |
| Central 403 mapping changes existing 500s to 403s | That is the fix. But note 8 call sites currently hand-write the `try/catch`; the central mapping must not double-handle. |
| Rate limiting locks out the test harness | The harness authenticates repeatedly against `POST /api/v1/auth/login`. Limits must be configurable and relaxed in the test environment, or F-016b's rate limiter breaks F-016a's harness. **Ordering dependency: F-016b must not tighten limits without a test-environment escape.** |
| Harness cannot run in CI on the maintainer's machine constraints | F-018's plan already flags that CI-dependent tasks need a **maintainer-pushed throwaway branch** — the dependency graph cannot express "waits on a human." Inherited by F-016 with the harness slice. |

---

## Adversarial Review

Run against the Round 1 decisions before writing the summary. Three findings; the first is
load-bearing and changes the plan.

### 🔴 A-1 — F-016 is the one feature this codebase **cannot verify**, and we just deprioritized its instrument

`11-testing.md:148`, verbatim: *"**`Program.cs` is not coverable.** With top-level statements and no
`WebApplicationFactory`, none of the seven route tables is executed by any test. There is **no
integration test in the solution** — no `Microsoft.AspNetCore.Mvc.Testing` reference anywhere, no
in-memory `TestServer`. **Every endpoint's auth attribute, validation call, ownership guard, and
status-code mapping is unverified end-to-end.**"*

And `13-security.md:156`: *"The `OwnershipGuardIdorTest` suite tests the guard, not the endpoints
that forgot it."*

F-016's entire job is adding auth attributes, calling ownership guards, and mapping status codes on
6+ endpoints across 5 services. That is **precisely and exclusively** the class of change no test in
this solution can observe. The Calendar IDOR exists *because* there is no endpoint-level test —
24 tests cover the 26-line `OwnershipGuard` class (`11-testing.md:72`) while nothing checks whether
an endpoint calls it.

**Doing F-016 with unit tests only reproduces the exact conditions that produced the bug it fixes.**

The instrument already exists on paper: F-018's harness. And critically, **its design work is already
done and merged** — PRD, architecture, threat model, 20 tasks, and *both gating spikes passed*
(Testcontainers on Rancher works with zero config; `ISwaggerProvider` needs no container). The
minimal slice F-016 needs is 5 of those 20 tasks:

| F-018 task | Gives F-016 |
|---|---|
| T05 | The `AgendaBuddy.IntegrationTests` project + `InternalsVisibleTo` × 7 |
| T06 | `CryptoSessionFixture` — a session RSA keypair, so tests can mint real RS256 tokens |
| T08 | `ServiceHostFixture` — a real service over HTTP against a Mongo container |
| T09 | `TokenFactory` — valid / expired / foreign-subject tokens |
| T14 | The 401/403 auth-failure tests — **the exact shape every F-016 change needs** |

Note T01 (the `Persistence` rename) gates T05 in the current graph, and F-018's AC-16 requires it to
land before any integration test is authored. So the slice is really T01 → T05 → T06 → T08 → T09 →
T14 — which is *F-018's own critical path minus the OpenAPI and CI-wiring work*.

**This does not contradict the Round 1 decision.** F-016 still goes first; the harness becomes its
first wave rather than a separate feature.

### 🟡 A-2 — F-016 + absorbed defects is now too large for one PRD

Original F-016 was 5 workstreams (auth 6 endpoints · Calendar guard · `AssertRole` · pagination ·
central 403). Absorption adds 5 more, and A-1 adds a harness. Ten-plus workstreams is exactly the
condition Discover guideline #2 exists to catch — and the condition that decomposed
`refactor-minimal-apis` earlier today.

A clean seam exists:

- **Exposure closure** — the 6 anonymous endpoints, Calendar ownership guard, central
  `ForbiddenException`→403, `AssertRole` wired, pagination, EventStore PII amplification, + the
  harness that proves it. One coherent claim: *no endpoint leaks PII, and we can demonstrate it.*
- **Identity hardening** — `RefreshAsync` account destruction, HTTPS-before-auth ordering, rate
  limiting / lockout, `AssertOwner` null claim. One coherent claim: *the auth system itself is safe.*

Both still precede F-014. The user declined a split at Round 1, but that was **before** A-1 added the
harness — so the size question is materially different now and is re-raised rather than assumed.

### 🟢 A-3 — central 403 mapping will be written twice

Central `ForbiddenException`→403 touches the error pipeline in all seven `Program.cs`, and F-019/F-020
rewrite exactly those files. Accepted rework: the change is small, and leaving 403s hand-written at 8
call sites until after a three-stage refactor is the worse trade.

---

## Edge Case Analysis
_Not run._

---

## External Context
_None ingested._

---

## Round 2 decisions (user, 2026-08-18)

3. **Harness as F-016's first wave** — pull T01, T05, T06, T08, T09, T14 from F-018's approved plan
   into F-016 wave 1, so every authz change gets a real 401/403 test against a live endpoint.
4. **Split F-016** into exposure closure (keeps `F-016`) and identity hardening (new `F-021`).

---

## Discovery Summary

**Program: Platform Remediation.** Four planned features, verified against the code, re-scoped and
re-sequenced into **six**. Every premise held; two were materially under-scoped; ten catalogued
defects belonged to no feature at all.

### Resulting sequence

| # | ID | Slug | Scope | Ships when |
|---|---|---|---|---|
| 1 | **F-016** | `secure-public-endpoints` | **Harness first** (F-018 T01/T05/T06/T08/T09/T14), then: auth the **5** anonymous PII GETs *(corrected from "4" at Define — `services/{email}` was omitted; `professions*` stays anonymous)* · project the embedded appointment book out of provider reads for non-owners · `OwnershipGuard` on both Calendar routes · central `ForbiddenException`→403 · `AssertRole` wired on provider + profession creation · pagination on both list endpoints · stop read queries writing full PII to `events`. **Claim: no endpoint leaks PII, and we can demonstrate it.** | Closes the live exposure |
| 2 | **F-021** | `identity-hardening` *(new)* | `RefreshAsync` account-destruction fix · `UseHttpsRedirection` before `UseAuthentication` (6 files) · rate limiting + account lockout · `AssertOwner` null-claim. **Claim: the auth system itself is safe.** | |
| 3 | **F-014** | `wire-unreached-services` | Register + route the 6 verified-unreachable capabilities · **+ absorbed: prevent double-booking** (`Start < End`, future-dated, overlap) | Makes F-006–F-010 real |
| 4 | **F-015** | `api-gateway-and-mobile-contract` | Gateway (now a **prerequisite**, not a nicety — AppHost assigns ports dynamically) · `api/v1/` prefixes · wire the refresh-token flow · `LogoutAsync` calls the server. Consumes F-016's paginated contract. | Mobile client works |
| 5 | **F-017** | `container-and-cd-hardening` | 3 unrunnable Dockerfiles · delete 3 library Dockerfiles + Compose services · **§7 dependency-audit + secret-scan gate** · image build/scan/push | Discharges the §7 gate |
| 6 | **F-018–F-020** | api refactor program | Resumes ~14 tasks (6 consumed by F-016) | |

**Filed for later, not in this program:** `F-022` password-reset flow (downstream of F-014 —
needs `NotificationService` for delivery) · `F-023` token revocation / `jti` denylist ·
`F-024` data-subject-rights. `services.BuildServiceProvider()` → noted on F-019, which rewrites
those files anyway.

### The three things that must not get lost

1. **🟡 Open question for the F-016 PRD — provider discovery. Largely resolved by evidence found at
   approval time.** `ROADMAP.md` F-003 `customer-onboarding-flow` — status **Shipped** — describes the
   flow as *"a customer **signs up**, **discovers providers**, and subscribes to one."* Discovery is
   therefore explicitly a **post-signup** step in the product's own shipped definition, so
   authenticating `GET /api/v1/providers` is consistent with intent rather than a regression. Combined
   with `INTENT.md` framing the product around providers managing their own clients, and with the fact
   that no client can reach the route today, the recommendation is **authenticate it outright**.
   Still to be confirmed at the Define gate rather than assumed, because it is a product call — but it
   is now a confirmation, not an open design question. The embedded-appointment-book exposure
   (`ProviderEntity` embeds `AppointmentEntities` + `SubscribedCustomerCollection`) should be fixed
   **regardless** of the auth decision: an authenticated customer browsing providers still has no
   business receiving every provider's appointment book.
2. **F-016 must record the paginated response shape as a decision**, because F-015 is written against
   it. A contract, not an implementation detail.
3. **F-018's paused state is now stale.** It loses 6 tasks to F-016; its plan, dependency graph and
   ACs need amending on resume. Recorded in `.paused-feature.json`.

### Why this ordering is defensible

- The exposure closes **first**, and the breaking contract changes (auth + pagination) land while
  they have **zero consumers** — the mobile client cannot reach those routes today.
- F-016's authz changes become **verifiable**, using a harness whose feasibility is already proven by
  two passed spikes rather than assumed.
- F-014 stops adding six new route families — including therapy notes and payments — on a substrate
  where `AssertRole` is dead and a forgotten `try/catch` silently returns 500 instead of 403.
- The aborted F-018 Inception is **not wasted**: its plan becomes F-016's wave 1.

### ✅ Approved by the maintainer 2026-08-18

Discover closed. Roadmap reordered (Priority column reflects the new sequence; feature IDs unchanged),
F-021–F-024 created as feature records, claim moved F-014 → **F-016**.

**Define runs on F-016 `secure-public-endpoints`.** Its PRD must resolve, not inherit:
- the provider-discovery auth decision (recommendation: authenticate, per F-003's shipped definition);
- the **paginated response shape**, as a recorded contract F-015 consumes;
- how the six absorbed F-018 harness tasks map onto F-016's own acceptance criteria, given F-018's
  AC-16 requires the `Persistence` rename to land as its own commit before any integration test.

---

## Design Discovery (Bloom's Taxonomy) — F-016

**Mode: self-answered.** The maintainer's standing instruction for this run is *"keep going, only stop for
required human input, move autonomous as possible."* Sketch mode already pre-drafts every answer with a
cited source; here Neo drafted and **accepted** them rather than batching them for confirmation, and
carried genuine ambiguity into the design docs as flagged items — which is what Sketch mode prescribes
for residual ambiguity anyway. The Step 12 design approval gate is where the human re-enters, because
that gate is a hard rule.

### Round 1 — Mechanics

**Q1. For a `GET /api/v1/providers` request, what actually happens, and where does the authorization
decision have to sit?**
Endpoint → `EventsHelper.GetAllProvidersEvent` (a pure pass-through, zero logic) →
`IRequestCollection.GetProvidersRequest` → hand-constructed `GetProvidersQueryHandler.Handle` →
`mediator.Publish` (**no-op — zero `INotificationHandler` implementations exist**) → `ProviderService` →
`MongoDbRepository<ProviderEntity>.GetAllAsync()` → **and then an audit write that serialises the whole
result** (`GetProvidersQueryHandler.cs:23`). *Source: `15-cqrs-and-messaging.md:95-113,213`.*
**Consequence for this design:** authorization must sit at the **endpoint**, because everything below it
is a pass-through chain with no interception seam — MediatR never dispatches, so there is no
`IPipelineBehavior` to hang an authorization behaviour on. *Source: `15-cqrs-and-messaging.md:51`.*

**Q2. Where does the PII projection have to happen?**
`GetAllAsync()` returns whole `ProviderEntity` documents including embedded `AppointmentEntities` and
`SubscribedCustomerCollection`; the endpoint returns the entity object directly. So the projection is a
**response-shaping concern at the endpoint boundary**, not a query change — changing what Mongo returns
would be a data-model migration, explicitly out of scope per the PRD's last Assumption.

**Q3. What happens to the audit write once the endpoint is authenticated?**
Requirement 9 removes the *unauthenticated* write-amplification vector, but not the amplification. An
authenticated caller can still force an unbounded PII copy into `events` on every read. Both fixes are
needed; neither subsumes the other.

### Round 2 — Apply (map to the actual stack)

**Q1. Which mechanism for the central `ForbiddenException` → 403?**
⚠️ **This round surfaced the single most important design fact, and it contradicts the naive plan.**
`10-error-handling.md:9-34`: in **all seven services** `UseExceptionHandler` is registered *inside*
`if (app.Environment.IsDevelopment())`, alongside Swagger. **A `ForbiddenException` → 403 mapping placed
in the existing exception handler would only work in Development.** In Production there is no handler at
all — Kestrel returns a bare 500 with an empty body.
**Decision:** implement `IExceptionHandler` (the .NET 8+ idiomatic form, which this codebase does not yet
use anywhere) in a shared library, register it with `AddExceptionHandler<T>()` + `UseExceptionHandler()`
**unconditionally, outside the Development guard.** This is strictly larger than "add a mapping" and is an
ADR candidate — it changes production error behaviour for all seven services. It also *fixes* a latent
production defect rather than introducing risk.

**Q2. Reuse or build for pagination?**
Build — minimally. `IRepository<T>` (verified by reading `Library/Repositories/IRepository.cs`) exposes
`GetAllAsync()`, `FindAllAsync(BsonDocument)`, and no skip/limit/count anywhere. A paged primitive must be
added to the interface, which means every implementer changes — `MongoDbRepository<T>` and
`Identity.Tests/Helpers/InMemoryRepository.cs`. Per the `yagni` ladder: add **one** paged method, not a
query-object abstraction.

**Q3. Which existing patterns must the harness follow?**
`Identity.Tests` is the only project in the solution with purpose-built test infrastructure —
`InMemoryRepository`, `FakeDateTimeProvider`, `RsaKeyHelper`, and a `TestCollectionDefinition` xUnit
collection that serialises tests mutating `JWT_*` environment variables (*`11-testing.md:37`*). The new
harness should follow that collection-serialisation pattern, because `AuthenticationExtensions` reads
`JWT_PUBLIC_KEY` from the environment at startup and parallel test classes would race on it.

### Round 3 — Trade-offs and Judgments

**J1 — 403 vs 404 for a non-owned resource.** Chosen: **403**, matching the eight existing hand-written
call sites. A 404 would hide existence but would diverge from established behaviour and make the central
mapping inconsistent with the endpoints it replaces. Recorded rather than assumed because
`GET /api/v1/customers/{email}` is currently an enumeration oracle and this is the deliberate decision not
to close that particular door in this feature.

**J2 — Scope of the audit fix: one handler or all ten?** The PRD's requirement 16 names
`GetProvidersQueryHandler.cs:23` as "the specific offender", but **all ten query handlers follow the same
publish → query → audit shape** (*`15-cqrs-and-messaging.md:160`*), and `GetCustomersQuery` serialises every
customer record. Fixing only the named one leaves an equivalent hole.
**Judgment: design for all ten; flag that PRD AC-17 tests only the provider path and should be broadened at
the Plan gate.** Surfaced rather than silently expanded, because the PRD is approved.

**J3 — Correctness over speed.** This is a security fix whose entire premise is that the codebase cannot
currently verify its own authorization. Shipping it faster by skipping the harness would defeat the
feature. Where the two conflict, correctness wins — already settled at Discover Round 2 and restated here
because it governs the task ordering in Plan.

**J4 — Do not touch Identity's error scheme.** Identity uses an incompatible ad-hoc `{ error, message }`
envelope and is the only service without `ProblemDetailsServiceEndpointFilter`
(*`10-error-handling.md:146,208`*). The central handler must not be registered in Identity in this feature —
unifying two error envelopes is its own piece of work, and F-021 touches Identity next.

### Synthesis

The architecture is: **authorization and projection at the endpoint boundary** (the only interception seam
that exists, since MediatR never dispatches); **one shared `IExceptionHandler`** registered unconditionally
in the six domain services to make 403 structural rather than copy-pasted; **one new paged repository
primitive** plus response DTOs for the two list endpoints; **audit writes reduced to metadata** across all
ten query handlers; and **an integration harness** that asserts each of those over real HTTP against a
throwaway Mongo container, refusing to start if pointed anywhere else.

Three items carried forward as flagged rather than resolved: broadening AC-17 to all ten query handlers
(J2), whether to add an `actor` field to `Event` now that these endpoints finally have an authenticated
caller to record, and the 403-vs-404 enumeration-oracle decision (J1, decided but worth the human's eyes).

---

## Threat Modeling Triage — F-016

- **Trust boundary changes:** yes — creates an authorization boundary at 5 routes that had none, adds
  ownership scoping to 2, adds the solution's first two `AssertRole` call sites, moves exception
  handling into Production (AD-1).
- **Regulated data:** yes — provider/customer names + emails; appointment records linking a named
  customer to a named provider. Cluster is synthetic, but the data classes are PII.
- **New attack surface:** yes — `page`/`pageSize` client input; new response type; a new
  `IExceptionHandler` running in Production; a new harness whose connection-string resolution can
  reach a live cluster.
- **Triage tier: Full (3/3).** Party convened (solo spawn mode — standing no-Agent-tool instruction).

**Outcome: 8 threats across 6 trust boundaries — 1 CRITICAL, 2 HIGH, 5 MEDIUM, plus 6 LOW noted.**
**Five of the eight are introduced or made newly reachable by this feature**, which was the point of
Phantom's framing. Two changed the design rather than annotating it:

- **T-001** — the response-shape projection reuses `OwnershipGuard.AssertOwner`, which **passes on a
  null claim** (`string.Equals(null, null)` is `true`). The hole exists today but is *unreachable* at
  these routes; this feature makes it reachable, and the bypass lands on the **owner** branch that
  returns the unprojected entity. **Reassigns PRD requirement 18 from F-021 into F-016.**
- **T-003** — authenticating `GET /api/v1/customers` does not authorize it. Registration is anonymous,
  unverified and unthrottled, so an attacker self-registers and pages through the entire customer
  table. **Pagination bounds the response, not the extraction.** Proposed fix is a scope addition
  beyond the approved PRD → escalated to the human.

Full detail: [`threat-model.md`](../archive/design/secure-public-endpoints/threat-model.md) ·
MOM: [`MOM_threat-model_secure-public-endpoints_2026-08-18.md`](../archive/mom/MOM_threat-model_secure-public-endpoints_2026-08-18.md)

---

## Design-Laws Audit Triage — F-016

- **UI surface:** no — no screen, modal, form, nav element or user-visible template. `MobileApp` untouched.
- **New flow / pattern:** no — and the API contract changes have no consumer today.
- **First-experience pathway:** no — no onboarding/signup/install surface is modified.
- **Triage tier: Skip (0/3).**

Muse recorded **two downstream UX consequences as inputs to F-015** rather than leaving a bare skip:
the provider-browse screen must be designed as an *authenticated* screen with real 401/403 paths (the
60-minute token expiry is currently a hard logout because the refresh flow is stored but never
called), and a browsing customer receives `ProviderSummary` with **no availability** — so a
"browse providers with open slots" design would need the Calendar route, which is now
ownership-guarded and returns 403 to a customer. Detail: [`ux-review.md`](../archive/design/secure-public-endpoints/ux-review.md)

---

## Variant Convergence Gate — F-016

**Skipped.** The gate fires only when Step 10.6 ran with **Full** triage; it ran **Skip**. No visual
exploration is possible or needed for a feature with no UI surface. One-line record per the skill.

---

## Readiness Party Triage — F-016

- **Task count:** 19
- **Waves:** 7
- **Domains:** `backend`, `devops`, `security`
- **Unresolved MUST requirements:** no
- **Triage tier: Full** (all three Full conditions met — multi-wave AND multi-domain AND ≥6 tasks)

**Outcome: Fair — 4 open gaps, one a real defect corrected in-party.**

The finding that justified convening: **AC-12 contradicted ADR-025.** It required a 403 from
`POST /api/v1/professions` on a route ADR-025 **deletes**. Left in place, Build would have
implemented a role check on a non-existent route and read the correct 404/405 as a test failure.
Echo found it by cross-checking ACs against the **ADR registry** rather than against the
requirements — a requirements-only pass showed AC-12 as fully covered, because every link was
present and the link was to a superseded decision. Struck, replaced by AC-26.

Carried into Construction: **no CI job runs the integration suite** (F-018's T18 was not absorbed, so
the feature's central claim is unenforced — escalated to the human rather than decided);
`tasks.cjs ready` is not feature-scoped and returns paused-F-018 tasks; requirements 8 and 20 have
no dedicated AC (distributed coverage).

Phantom's issue-#55 check **passes** — all 7 mitigate-now threats are `[security]`-tagged ACs on
tasks, verified against the raw task records after `ac list --json` produced a false
`security-ac-unmaterialized` reading (a projection artifact, not a data gap — recorded in the MOM
because the next person to run that command will draw the same wrong conclusion).

MOM: [`MOM_readiness-party_secure-public-endpoints_2026-08-18.md`](../archive/mom/MOM_readiness-party_secure-public-endpoints_2026-08-18.md)

---

## Standards Readiness — F-016

| Gate | Mode | Tier | Outcome |
|---|---|---|---|
| Define Step 6.5 | `--ideate` | advisory | Light skip |
| Plan Step 17.5 | `--design` | **enforcing** | **Skip-with-notice — skill unavailable, not user-elected** |

The plugin **is installed**, but its six source standards repos do not resolve under the current
`gh` auth and no local `.nordstrom-standards/` exists; re-verified at Plan. Per the gate's own
instruction, an unavailable skill skips-with-notice rather than requiring an `/override` ADR —
overrides are for a human electing to bypass a *working* gate. **No MUST findings were surfaced
because none could be computed; absence of findings is not evidence of compliance,** and the PRD's
Standards Alignment section now says so explicitly. Same condition at F-013 and F-018.
