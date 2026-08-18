# Review — secure-public-endpoints (F-016)

**Date:** 2026-08-18 · **Feature:** F-016 · **Branch:** `feat/F-016-secure-public-endpoints` · **PR:** [#38](https://github.com/ogdevlabs/agenda-buddy/pull/38)
**Diff reviewed:** `git diff main..HEAD` — 158 files, +9581 / −481, 28 commits
**Blast radius:** [`BLAST-RADIUS_secure-public-endpoints_2026-08-18.md`](BLAST-RADIUS_secure-public-endpoints_2026-08-18.md)
**Reviewers:** Neo (lead), Echo, Phantom, Jarvis

> **Spawn mode: solo.** STATE records `Party Mode: agent-teams`, but this session carries a standing
> instruction not to spawn agents. All four mandates were executed by one model in sequence, with
> cross-talk performed explicitly. Fidelity is lower than four independent context windows — weigh the
> findings accordingly. Same condition as every prior F-016 meeting.
>
> **Muse did not participate.** `ux-review.md` records **Triage outcome: Skip (0/3)** — no user-facing UI
> surface — so the Step 12 conditional does not fire and there is no design-time scorecard to delta
> against. Step 12.5 skips for the same reason (see its skip record below).

**Tally: 0 Critical · 5 Important · 7 Advisory · 1 pre-existing deviation requiring human acceptance.**

---

## Verdict summary

| Reviewer | Verdict |
|---|---|
| **Neo** — architecture & PRD conformance | **PASS with one deviation.** No architectural drift from `ARCHITECTURE.md`; all 26 ACs walked individually in `verification.md`. The one deviation (AC-19) needs human acceptance, not a fix. |
| **Phantom** — security | **PASS.** All 7 "mitigate now" threats have implementing code *and* a linked asserting test; `tasks.cjs check` reports **0** `security-ac-untested`. Two Important findings are *newly-visible adjacent exposure*, not regressions. |
| **Echo** — coverage | **PASS with one gap.** Every AC maps to a test. One Important gap: AC-14 is verified on 1 of 6 remaining catch sites. |
| **Jarvis** — docs & contracts | **PASS with two Important doc staleness items.** `api-contracts.md` now matches the implementation (it was corrected three times during build). `CLAUDE.md` does not. |

---

## Important findings

### I-1 [Important] — Phantom · The providers list cache holds **unprojected** entities

`Provider/Program.cs` (list route): the cache stores `PagedResponse<ProviderEntity>` — full records including
every embedded appointment and customer email — and `ProviderSummary.From` is applied **after** the cache
read, per request.

That is correct **today**: the projection is a property of the response, not of the cached value, and every
response path goes through it. But it means **the cache deliberately holds more than the endpoint can ever
return**, and a single line returning `providerCollection` directly would leak every provider's appointment
book to any authenticated caller.

This is the *same trap class* as the Calendar guard-before-cache ordering — which F-016 recognised, recorded
as a design invariant in code comments, and pinned with a regression test. **This one is undocumented and
unpinned.** And the sibling route one file over (`Customer/Program.cs`) does exactly the thing that would
break it: returns the cached collection directly.

**Recommendation:** either document it as an invariant with a regression test, mirroring the Calendar
treatment, or project *before* caching so the cache cannot hold what the endpoint won't return. The second
is strictly safer and changes the cached type, not the contract.

**Linked with Neo's N-1 — same root cause.** See cross-talk.

### I-2 [Important] — Phantom · `GET /api/v1/customers` returns full `CustomerEntity` to any Provider-role caller

`Customer/Program.cs` (list route) returns unprojected `CustomerEntity`, so a caller holding the `Provider`
role receives, for **every customer in the system**:

- `SubscribedProviderCollection` — which providers that customer uses
- `AppointmentCollection` — their appointment identifiers
- `KafkaTopic`

Requirement 10 named the *provider* routes only, so **this is spec-conformant** and ADR-026 explicitly
deferred owner-scoping as "the stronger fix… more work". The finding is not that the spec was violated — it
is that the exposure class F-016 closed for providers is still open for customers, and `totalCount` now tells
a caller exactly how many pages of it there are.

Registration is anonymous, unverified and unrate-limited (F-021 owns the limiter), so the attack path ADR-026
describes is still: self-register as `Provider` → page the customer table → obtain the customer↔provider
relationship graph. The role check raises the bar from "anyone" to "anyone who picked Provider at signup".

**Recommendation:** log as tech debt against F-021/F-024 with this concrete payload list, so the deferral is
recorded against *what is actually exposed* rather than against "customer data". Not a merge blocker —
accepting it is consistent with ADR-026's recorded decision.

**Linked with Neo's N-1.**

### I-3 [Important] — Echo · AC-14 is verified on **1 of 6** remaining local catch sites

AC-14 requires that the hand-written `ForbiddenException` catch sites "still return exactly one 403, no
double-handling, no changed body". T08 removed one of the seven (Customer's, for AC-13), leaving six.
`LocalCatchUnaffectedTest` verifies **Provider `:273`** in both environments.

Unverified end-to-end:

| Site | Integration coverage |
|---|---|
| `Booking/Program.cs:125` | **none** — no integration test touches any `api/v1/booking` route |
| `Booking/Program.cs:149` | **none** |
| `Booking/Program.cs:174` | **none** |
| `Services/Program.cs:143` | 401 path only (`AnonymousPiiRoutesTest`); the 403-through-local-catch path is not exercised |
| `Services/Program.cs:177` | **none** |

These are the sites most exposed to the change, because T08 inserted a new exception-handler middleware into
all six services' pipelines. The unit-level `AgendaBuddyExceptionHandlerTest` proves the handler *declines*
non-`ForbiddenException` and the blast radius shows no signature change reached them — so the risk is low,
but "low" is a judgement, not a test.

**Recommendation:** one parameterised integration test over the five remaining sites would close it. Roughly
40 lines, reusing `ServiceHostFixture<BookingAnchor>` / `<ServicesAnchor>`. Worth doing before ship given
that verifying authorization end-to-end is this feature's entire premise.

### I-4 [Important] — Jarvis · `CLAUDE.md` is stale in the two places agents read first

`CLAUDE.md:14` — *"xUnit — 379 tests total: 305 across 12 backend projects + 74 in `MobileApp.Tests`"*.
Actual: **358** backend + **93** integration + **74** mobile = **525**.

`CLAUDE.md:38` — the Development section lists the backend and mobile test commands but **not the
integration suite**, which is a third, separate command (`AgendaBuddy.IntegrationTests` is excluded from
`agenda-buddy-backend.slnf` by ADR-031, so the documented commands do not run it).

`README.md` likewise never mentions the integration suite or Testcontainers.

This matters more than ordinary doc drift: `CLAUDE.md` is the file loaded into every agent's context, and it
currently tells the next contributor that a suite they must run does not exist. The `## Key Files` section
also has no entry for `AgendaBuddy.IntegrationTests/`.

**Recommendation:** fix before ship — it is a five-line edit and it is the highest-leverage documentation in
the repo.

### I-5 [Important] — Jarvis · The context catalog still carries the error that propagated into four artifacts

`docs/pdlc/context/15-cqrs-and-messaging.md:161` still states *"10 queries, 10 handlers"* directly above a
table listing **nine**. That single line is the origin of a count that reached the PRD's AC-17 note,
`ARCHITECTURE.md` §5, the plan's threat table, and `F-016-T18`'s task body — all of which are now corrected.

It is scheduled for the `/ship` Reflect context refresh (16c-bis), which is the right mechanism. Flagged here
so it cannot be lost between Construction and Ship: if the refresh regenerates the file from code, it
self-corrects; if it is hand-edited, this line needs naming explicitly.

---

## Advisory findings

| # | Reviewer | Finding |
|---|---|---|
| A-1 | Phantom | **Authorization failures are unlogged.** `AgendaBuddyExceptionHandler` writes no log entry, and there is no log sink at all (`10-error-handling.md:138`), so repeated IDOR probing against the Calendar or provider routes leaves no trace. `requestId` is returned but not exported anywhere. F-021/F-024 territory; noted because F-016 is the feature that makes those 403s meaningful. |
| A-2 | Phantom | **ADR-030 stands accepted.** SSH.NET `GHSA-q939-rpr3-3284` (HIGH) has no patched version; it enters via Testcontainers and is reachable only through Docker-over-SSH, which this project does not use. The unreachability is *tested* (`ContainerRuntimeGuardTest`), and `NU1903` is suppressed in that one project only, so F-017's future audit gate still sees it. No action; re-confirmed against the as-built code. |
| A-3 | Phantom | `totalCount` on `/customers` is an enumeration aid — it converts "page until empty" into "fetch N pages". Inherent to the published contract (ADR-023) and immaterial while I-2 stands; would matter if I-2 is closed by owner-scoping. |
| A-4 | Echo | `MongoDbRepository<T>.GetPagedAsync` has **no direct test** — structurally untestable (live DB + the driver's fluent chain ends in an extension method Moq cannot intercept). Covered end-to-end by `PaginationTest`'s 9 cases. Already disclosed in `verification.md` §4; Echo concurs with the disclosure rather than objecting to the gap. |
| A-5 | Echo | **`CacheAside` still has no test at all** and returns `default!` on a 500 ms lock timeout. F-016 depends on it in four routes and *documented* the flake risk (it is why the T-006 assertion is "not 200-with-data" rather than "exactly 403") but did not fix it. Pre-existing; F-016 correctly declined the scope. |
| A-6 | Neo | **`EventAndCommands` is now ASP.NET-coupled** — a `FrameworkReference` on `Microsoft.AspNetCore.App` plus `IHttpContextAccessor` in `EventStore`. Accepted in the ADR-027 amendment with a named escape hatch (`IAuditActorProvider`). Flagged for F-019/F-020 visibility, since they will move the web layer around. |
| A-7 | Jarvis | `CONSTITUTION.md` §1 still says **C# 12 / .NET 8 / ASP.NET Core 8**; the solution targets `net10.0`. Pre-existing, not introduced by F-016, and out of its scope — but §1 is the stack table every phase reads. Also: `CHANGELOG.md` lives at `docs/pdlc/memory/CHANGELOG.md`, not the repo root, which is worth knowing before someone creates a second one. |

---

## Over-Engineering (Neo · `yagni-review` lens, level `full`)

Little to cut — the diff is unusually lean for its size because most of it is tests and documentation. Three
one-liners, all deletion *opportunities*, none blockers:

- `shrink:` `PagedResponse<T>.From(items, count, page)` is a thin wrapper over the primary constructor; the
  three call sites could use `new PagedResponse<T>(items, count, page.Page, page.PageSize)`. The wrapper buys
  one thing — it makes "page and pageSize come from the *clamped* request" structural rather than a
  convention — so this is a genuine trade, not dead weight. Keep or cut on taste.
- `yagni:` `HostileEndpoints.Srv()` (no credentials) is used only by the theory case; `WithCredentials()` and
  `SrvWithCredentials()` carry the load. Retained deliberately: srv-without-credentials is a distinct
  rejection reason and the guard tests both branches.
- `delete:` `Library.Tests/Repositories/MongoDbRepositoryTest.METHOD()` and the eight sibling empty
  `METHOD()` stubs across `EventsAndCommands.Tests` are worthless placeholders inflating the test count. **Not
  deleted here on purpose** — AC-19 forbids removing pre-existing tests, and F-016 already carries one such
  deviation. Correct target for F-017/F-019 as a batch.

**Nothing flagged that the safety carve-outs protect** (validation, error handling, security, explicitly
requested work) — which is most of this diff.

---

## Threat-model mitigation check (Phantom · issue #55)

Every "mitigate now" threat in `threat-model.md`, checked for *implementing code* **and** a *linked asserting
test* — not a task citation.

| Threat | Sev | Implementing code | Asserting test | Linked? |
|---|---|---|---|---|
| **T-001** | HIGH | `Library.ServerAuth/Tools/OwnershipGuard.cs:35-41` (+ `IsOwner:55-62`) | `T001_AssertOwner_WhenNeitherSubNorEntityEmailIsKnown_Throws` + `ProviderProjectionTest.T001_*` | ✅ |
| **T-002** | CRITICAL | `Harness/MongoEndpointGuard.cs`, called from `ServiceHostFixture.InitializeAsync` | `T002_AbortsDuringFixtureConstruction_AndCreatesNoDatabase` | ✅ |
| **T-003** | HIGH | `Customer/Program.cs` — `AssertRole(user, "Provider")` before the cache read | `T003_ACustomerRoleTokenGets403AndNoCustomerRecord` | ✅ |
| **T-004** | MEDIUM | `Library.ServerAuth/AgendaBuddyExceptionHandler.cs` | `T004_TheProductionForbiddenBody_CarriesOnlyStatusTitleAndRequestId` | ✅ |
| **T-005** | MEDIUM | `EventAndCommands/Persistence/{QueryAudit,AuditActor,EventStore}.cs` | `T005_AnAuthenticatedReadIsAttributedToItsCallerAndRecordsNoPersonalData` (+ literal-route case) | ✅ |
| **T-006** | MEDIUM | `Calendar/Program.cs:149,195` — guard **above** the cache read | `T006_AWarmCacheIsNotServedToADifferentPrincipal` | ✅ |
| **T-007** | MEDIUM | `Profession/Program.cs` — route + write path deleted | `T007_TheRouteIsGone_AndNoProfessionIsCreatedByAnyRole` | ✅ |

`node scripts/tasks.cjs check --json` → **0** `security-ac-untested` findings for F-016.

**Phantom's verdict: no "citation over code" gap. All seven mitigations exist in code and are asserted by a
test named after the threat.**

---

## Cross-talk

**Round 1 — Phantom I-1 ↔ Phantom I-2 ↔ Neo N-1: one root cause.**

Neo raised the projection asymmetry (providers projected, customers not). Phantom raised the unprojected
cache (I-1) and the customer payload (I-2) independently. All three are the same underlying decision:
**the projection is applied at the response boundary of one route family only.**

Consensus reached in round 1. Primary finding: **I-2** (concrete, quantified exposure). **I-1** is filed
separately because its fix is different in kind — I-1 is an ordering/containment invariant, I-2 is a scope
decision already recorded in ADR-026. **N-1** is folded in as the architectural framing of both rather than a
third finding.

**Round 1 — Phantom I-1 ↔ Echo A-5: independent, despite both being "the cache".**

I-1 is *what the cache holds* relative to what the endpoint returns. A-5 is that `CacheAside` has no test and
fails open to `default!`. Different root causes; neither fix resolves the other. Filed independently, with a
note that A-5 is why I-1's consequences would be hard to observe if it ever regressed.

**Critical routing to Neo:** none — no Critical findings were raised by Phantom or Echo, so the escalation
path in the protocol did not trigger.

---

## AC conformance walk (Neo)

Neo does not restate the 26-row table; `verification.md` §3 walks each AC individually and names the code path
or test satisfying it. Neo's independent checks on that document:

- **Spot-checked 6 rows against the code** (AC-3, AC-8, AC-13, AC-17, AC-21, AC-26) — each names a real test
  that exists and passes.
- **AC-12** is correctly recorded as struck at Plan, not silently dropped.
- **AC-19** is the one deviation, and `verification.md` §2 states it plainly rather than burying it.
- **The three count corrections** (9 handlers not 10; 7 catch sites not 8; no `profession`/`duration` fields)
  are each traced to their origin and fixed in the owning artifact. Neo's view: **this is the review's most
  valuable output** — a design doc that survives implementation unchanged usually means nobody checked it.

**Conformance status: PASS.** No drift from `ARCHITECTURE.md`; the two documented deviations from
`api-contracts.md` are corrections *to* the doc, made because the doc described fields that do not exist.

---

## ⚠️ Deviation requiring human acceptance (not a finding — a decision)

**AC-19: "No pre-existing test was deleted or skipped to achieve this." One was deleted.**

`Profession.Tests/Events/EventsHelperTest.AddProfessionEvent_ReturnSuccess`, removed by T17 because ADR-025
deleted its subject (`EventsHelper.AddProfessionEvent`).

The argument, restated so the decision is on the record: AC-19 exists to stop a test being deleted **because
it failed**. This test's subject was deliberately removed by an approved ADR, so it cannot compile, and
keeping it would mean keeping the write path the task exists to delete. The requirement is inverted and
pinned harder — `ProfessionWriteRouteRemovedTest` asserts over real HTTP that the route is gone for **both**
roles, that nothing is written, and that the two read routes still return 200 anonymously.

**Net: −1 unit test, +3 integration tests.** All four reviewers concur the trade is sound. It is recorded as a
deviation rather than resolved silently because AC-19 is written absolutely, and only the maintainer can
accept a deviation from an approved acceptance criterion.

---

## Draft CHANGELOG entry (Jarvis)

For `docs/pdlc/memory/CHANGELOG.md` — note the file is **there**, not at the repo root.

```markdown
## [Unreleased]

### Security
- **BREAKING** — `GET /api/v1/providers`, `/providers/{email}`, `/customers`, `/customers/{email}` and
  `/services/{email}` now require authentication. They previously returned full records anonymously,
  including embedded appointments carrying customer email addresses (F-016, #38).
- **BREAKING** — `POST /api/v1/professions` has been **removed**. Professions are seeded reference data and
  no shipped flow creates one; there is no administrative role to gate the route on (ADR-025).
- `GET /api/v1/calendar/availability/{email}` and `/calendar/appointments/{email}` now enforce ownership.
  Any authenticated user could previously read any provider's full appointment list (F-016).
- `GET /api/v1/customers` now requires the `Provider` role (ADR-026).
- `POST /api/v1/providers` now requires the `Provider` role **and** that the record is the caller's own.
- `GET /api/v1/providers*` returns `ProviderSummary` to non-owners — no appointments, no subscribed
  customers, no Kafka topic.
- `OwnershipGuard.AssertOwner` no longer treats a missing `sub` claim as ownership. `string.Equals(null,
  null)` is `true`, so a token with no subject previously passed the guard.
- `ForbiddenException` now maps to **403 in every environment**. It previously reached the client as 403 only
  where an endpoint hand-wrote a `try/catch`; elsewhere it was a 500, and in `Production` a bare empty-bodied
  one (ADR-022).
- Query audit records no longer serialise their full result payload into the `events` collection. A single
  anonymous list call previously wrote every provider — with embedded appointments and customer emails — into
  an unbounded, unindexed, never-pruned collection.

### Added
- **BREAKING** — both list endpoints are paginated: `?page=&pageSize=`, returning
  `{items, totalCount, page, pageSize}`. `pageSize` is capped at 100 and **clamped, not rejected**; the
  response echoes the effective value. The previous `204` for an empty collection is retired in favour of
  `200` with `items: []` (ADR-023).
- `Event.actor` — audit records now attribute reads to the calling `sub` claim (ADR-027).
- `AgendaBuddy.IntegrationTests` — the first integration suite in the solution: real services over HTTP
  against a MongoDB Testcontainer, with a fail-closed guard that refuses to run against any endpoint that is
  not the test session's own container. 93 tests.
- A separate, duration-enforced integration CI job (600 s budget).

### Fixed
- `AgendaBuddy.IntegrationTests` was absent from every CI path filter, so a change to it alone ran **zero**
  jobs.
- Renamed `EventAndCommands/Persitency` → `Persistence`.
```

---

## Post-approval actions (Step 14 — pending human decision)

- PR comments for non-accepted findings → PR #38.
- Accepted Phantom warnings (A-1, A-3) and Echo gaps (A-4, A-5) → STATE Guardrail Log.
- Deferred findings → Decision Registry via `/decide`, batched into one Decision Review Party.
