# Episode 002: Secure Public Endpoints

**Episode ID:** 002
**Feature name:** Secure Public Endpoints — close unauthenticated PII exposure, add the verification harness
**Feature slug:** secure-public-endpoints
**Feature ID:** F-016
**Date delivered:** 2026-08-18 (merged as `2134b8d`, tagged `v0.2.0`, PR #38)
**Phase delivered in:** Construction
**Status:** Final — signed off by the maintainer at the Ship/Verify gate, 2026-08-22

> **File location note.** This project keeps episodes in `docs/pdlc/episodes/`, not
> `docs/pdlc/memory/episodes/` — the convention is recorded in that directory's own `index.md`, and episode
> 001 follows it. Deliberately consistent with the project rather than with the template's default path.

---

## What Was Built

F-016 closed the highest-severity item in the Platform Remediation program: `GET /api/v1/providers` and four
sibling routes returned full records **anonymously and unpaginated**, including embedded appointments carrying
customer email addresses and each provider's subscribed-customer list. Alongside that, both Calendar routes
were IDOR-able, `POST /api/v1/professions` let any authenticated caller write global reference data,
`OwnershipGuard.AssertRole` had never been called anywhere in the solution, and `AssertOwner` treated a
missing `sub` claim as *ownership*.

The feature was sequenced first in the program **because of a verification gap, not despite one**:
`docs/pdlc/context/11-testing.md:148` established that no route table in the solution was executed by any
test, so endpoint authorization — exactly what F-016 changes — was the one thing nothing here could verify.
The Calendar IDOR existed *because* of that gap. So F-016 carries the harness as well as the fixes: an
integration suite that hosts real services over HTTP against a MongoDB Testcontainer, with a fail-closed
guard that refuses to run against any endpoint that is not the test session's own container.

Every defect was **demonstrated live as a failing test before being fixed** rather than argued from reading
the code. The suite went from 305 backend tests and no integration suite at all, to 358 backend + 99
integration + 74 mobile.

---

## Links

- **PRD:** [PRD_F-016_secure-public-endpoints_2026-08-18.md](../prds/PRD_F-016_secure-public-endpoints_2026-08-18.md)
- **Plan:** [plan_F-016_secure-public-endpoints_2026-08-18.md](../prds/plans/plan_F-016_secure-public-endpoints_2026-08-18.md)
- **PR:** [#38](https://github.com/ogdevlabs/agenda-buddy/pull/38) — open, mergeable, CI green
- **Review file:** [REVIEW_secure-public-endpoints_2026-08-18.md](../reviews/REVIEW_secure-public-endpoints_2026-08-18.md)
- **Blast radius:** [BLAST-RADIUS_secure-public-endpoints_2026-08-18.md](../reviews/BLAST-RADIUS_secure-public-endpoints_2026-08-18.md)
- **Verification (26-AC attestation):** [verification.md](../design/secure-public-endpoints/verification.md)
- **Design docs:** [ARCHITECTURE.md](../design/secure-public-endpoints/ARCHITECTURE.md) · [api-contracts.md](../design/secure-public-endpoints/api-contracts.md) · [threat-model.md](../design/secure-public-endpoints/threat-model.md) · [data-model.md](../design/secure-public-endpoints/data-model.md) · [ux-review.md](../design/secure-public-endpoints/ux-review.md)
- **MOMs:** wave-3, wave-4, wave-6 standups + [party-review](../mom/party-review_F-016_2026-08-18.md)

---

## Key Decisions & Rationale

Full ADRs are **ADR-022 … ADR-031** in `DECISIONS.md`. The ones that changed the shape of the work:

1. **ADR-022 — a shared `IExceptionHandler`, registered *unconditionally*.** PRD requirement 14 asked for the
   `ForbiddenException` → 403 mapping to be added centrally, and it could not be done where the PRD assumed:
   in all seven services `UseExceptionHandler` is registered *inside* `if (IsDevelopment())`, so a branch
   added there would give 403 in Development and a bare, empty-bodied **500 in Production** — the exact silent
   failure the requirement exists to remove, preserved in the only environment that matters.
2. **ADR-025 — `POST /api/v1/professions` deleted, not role-gated.** Supersedes requirement 13. There is no
   role to check for: Identity's allow-list is exactly `{Provider, Customer}` with no administrative tier, so
   the only implementable check would still let any self-registered provider write global reference data.
3. **ADR-026 — `GET /api/v1/customers` requires the `Provider` role.** A scope addition beyond the approved
   PRD, escalated and accepted. Authenticating it alone was nearly worthless: registration is anonymous and
   unrate-limited, so **pagination bounds the response, not the extraction**. Owner-scoping to the caller's own
   `SubscribedCustomerCollection` is the stronger fix and was deliberately deferred.
4. **ADR-027 — `Event` gains an `actor` field, stamped centrally.** Reducing the audit payload without adding
   attribution would leave the trail *less* useful for incident response than the PII dump was. **The design's
   costing was wrong** — it budgeted "one assignment per handler", but no handler can see the caller. The
   maintainer chose to stamp it in `EventStore.SaveAsync` from `IHttpContextAccessor`: ~8 files instead of ~30,
   cannot be half-done, and it attributes the 11 command handlers for free. Accepted cost: the CQRS kernel is
   now ASP.NET-aware, with `IAuditActorProvider` named as the escape hatch.
5. **ADR-023 — the paginated contract, written before the endpoints.** Clamp, never reject: a 400 would tell an
   attacker the exact boundary and leave an honest client no way to discover the cap. `MaxPageSize = 100` is a
   security control. `204` retired in favour of `200` with `items: []`. F-015 is written against this shape.
6. **ADR-030 — SSH.NET's HIGH advisory accepted as unreachable, and the unreachability *tested*.**
   `GHSA-q939-rpr3-3284` has no patched version (every release through 2025.0.0 is flagged; pinning was
   attempted and cannot fix it). It enters via Testcontainers solely to support Docker-over-SSH, which this
   project does not use. `ContainerRuntimeGuardTest` asserts SSH.NET is never loaded while starting a
   container, so if anyone configures a remote Docker host the risk stops being theoretical and a test fails.
7. **ADR-031 — the integration project excluded from `agenda-buddy-backend.slnf`**, per the MobileApp
   precedent, so the unit gate stays Docker-free. Consequence: it is a **third, separate test command**, which
   is what made the CI path-filter gap (below) possible.
8. **Deviation accepted at the review gate — one pre-existing test deleted.** `AC-19` forbids it absolutely.
   `Profession.Tests` `AddProfessionEvent_ReturnSuccess` was removed because ADR-025 deleted its *subject*.
   Net −1 unit test, +3 integration tests, with the requirement inverted and pinned harder.

---

## What the implementation corrected in its own approved design

The most useful output of the feature, recorded because a design document that survives contact with the code
unchanged usually means nobody checked it.

| Artifact | Correction |
|---|---|
| `15-cqrs-and-messaging.md:161` | Says **"10 queries, 10 handlers"** directly above a **9-row table**. That one line propagated into the PRD's AC-17 note, `ARCHITECTURE.md` §5, the plan's threat table and T18's task body. Real count: 9 handlers, **18** audit call sites. ✅ **Fixed 2026-08-22** at the Ship context refresh, along with the same error in `00-overview.md:107`. |
| `api-contracts.md` §5.1 | Shows `"profession": "Fitness Coach"` and a service `"duration"`. **Neither field exists** on `ProviderEntity` or `ServiceEntity`. F-015 would have bound to fields that are not there. |
| `api-contracts.md` §5.1 | Said an owner receives their full entity from the **list** route too, which would make `items` a mixed array of two shapes — not deserialisable into a typed list. Changed to a homogeneous `ProviderSummary[]`. |
| `api-contracts.md` §3.1 | Said there were **8** hand-written `ForbiddenException` catch sites; there were **7**. A repo-wide grep returns 8 today only because a test doc comment mentions the pattern — very likely how the original 8 arose. |
| `api-contracts.md` §3.1 | Assumed the hand-written `TypedResults.Forbid()` sites returned a *bodyless* 403, so AC-14's "no changed body" meant tolerating two 403 contracts. `app.UseStatusCodePages()` already converts it to ProblemDetails — **the contract is uniform.** My first AC-14 test asserted an empty body and failed, correctly. |
| `ARCHITECTURE.md` §5 | `Event.actor` costed as "one `[BsonElement]` and one assignment per handler" — not achievable, see ADR-027 above. |

---

## Files Created

**Production (8)**
`Library.ServerAuth/AgendaBuddyExceptionHandler.cs` · `Library/Dtos/{PageRequest,PagedResponse,ProviderSummary}.cs` ·
`EventAndCommands/Persistence/{AuditActor,QueryAudit}.cs` · plus `Event.cs` and `EventStore.cs` re-created under
`Persistence/` by the rename.

**Integration harness — the whole project (16 files)**
`AgendaBuddy.IntegrationTests/` — `Harness/{EntryPoints,CryptoSessionFixture,DockerPreflight,MongoEndpointGuard,ServiceHostFixture,TokenFactory,HostileEndpoints}.cs`
plus 11 test classes (`ProfessionHostTest`, `AuthFailurePathTest`, `CentralForbiddenTest`,
`LocalCatchUnaffectedTest`, `RemainingLocalCatchSitesTest`, `NullClaimOwnershipTest`, `CalendarOwnershipTest`,
`AnonymousPiiRoutesTest`, `CustomerListRoleTest`, `ProviderCreationGuardTest`, `ProviderProjectionTest`,
`PaginationTest`, `QueryAuditPayloadTest`, `MongoFailClosedTest`, `MongoEndpointGuardTest`,
`ContainerRuntimeGuardTest`, `InternalsVisibleToTest`).

**Unit tests (6)**
`Library.Tests/Security/{KeyMaterialHygieneTest,AgendaBuddyExceptionHandlerTest}.cs` ·
`Library.Tests/Dtos/PageRequestTest.cs` · `EventsAndCommands.Tests/Persistence/{PersistenceNamespaceTest,QueryAuditTest,AuditActorTest}.cs` ·
`Identity.Tests/Helpers/InMemoryCredentialRepositoryPagingTest.cs`

**Docs** — PRD, plan, 5 design docs, `verification.md`, review + blast-radius, 4 MOMs, this episode.

Totals: **87 added · 71 modified · 2 deleted · 1 renamed** (158 files, +9581 / −481, 28 commits).

## Files Modified (notable)

`Booking|Calendar|Customer|Provider|Services|Profession/Program.cs` (all six: handler registration, guards,
projection, pagination) · `Library.ServerAuth/Tools/OwnershipGuard.cs` (null-claim fix + `IsOwner`) ·
`Library/Repositories/{IRepository,MongoDbRepository}.cs` (`GetPagedAsync`) ·
`Library/Services/{Provider,Customer}Service.cs` · 9 query handlers + 2 query types ·
`{Provider,Customer,Profession}/Requests/*` + `Events/EventsHelper.cs` ·
`EventAndCommands/{ServiceCollectionExtensions.cs,EventAndCommands.csproj}` ·
`.github/workflows/dotnet.yml` (integration job + path-filter fix) · `CLAUDE.md` · `CONSTITUTION.md` §9 ·
`DECISIONS.md` (ADR-022…031).

---

## Test Summary

| Layer | Required (§7) | Command | Result |
|---|---|---|---|
| 1 — Unit | **yes** | `dotnet test agenda-buddy-backend.slnf` | ✅ **358** passing / 0 failing / **0 warnings**, 12 projects (baseline 305) |
| 2 — Integration | no *(deliberately unchecked)* | `dotnet test AgendaBuddy.IntegrationTests/…csproj` | ✅ **99** passing, ~1 m 17 s — run anyway |
| 3 — E2E | no | — | ⊘ no command in project; logged skip |
| 4 — Performance | no | — | ⊘ no command in project; logged skip |
| 5 — Accessibility | no | — | ⊘ no command in project; logged skip |
| 6 — Visual regression | no | — | ⊘ no command in project; logged skip |
| 7a — Dependency audit | **yes** | `dotnet list package --vulnerable --include-transitive` | ⚠️ **1 HIGH** — `SSH.NET 2024.2.0` in `AgendaBuddy.IntegrationTests` only; all 25 pre-existing projects clean. **New on this branch**, disposition already recorded in ADR-030 |
| 7b — Secret scan (diff) | **yes** | 6 patterns over the 161 changed files | ✅ clean; no `.env`; all `appsettings` connection strings still blank |
| 7c — OWASP dependency-check | no | — | ⊘ CLI not installed; INFO |
| Mobile | — | `dotnet test MobileApp.Tests/…  /p:MobileWorkloads=false` | ✅ **74** (67 passing, 7 skipped), untouched |

**Total: 531 tests.** ⚠️ §7's security-scan gate was satisfied **by hand**, as at the F-013 ship — CI still
has only a credential grep, not a scanner. **F-017 owns automating it.** §7's **Integration** checkbox is
deliberately left unchecked: the amendment is gated on 10 consecutive green runs, tracked separately.

---

## Known Tradeoffs & Tech Debt

Accepted at the review gate, each with a named owner:

1. **`GET /api/v1/customers` returns full `CustomerEntity` to any `Provider`-role caller** — including
   `SubscribedProviderCollection`, `AppointmentCollection` and `KafkaTopic`, i.e. the customer↔provider
   relationship graph, with `totalCount` reporting how many pages of it exist. Spec-conformant (requirement 10
   named providers only) and consistent with ADR-026's deferral of owner-scoping. → **F-021 / F-024.**
2. **The providers-list cache holds *unprojected* entities.** `ProviderSummary.From` is applied *after* the
   cache read, so the cache deliberately holds more than the endpoint can return. Correct today; a single line
   returning the cached value directly would leak every appointment book — and the sibling route one file over
   does exactly that. Same trap class as the Calendar guard-before-cache ordering, which *is* documented and
   test-pinned. → **F-019 / F-020**, which rewrite this file.
3. **Authorization failures are entirely unlogged.** The central 403 handler writes no log entry and there is
   no log sink at all, so repeated IDOR probing leaves no trace. `requestId` is returned but exported nowhere.
   → **F-021 / F-024.**
4. **`CacheAside` still has no test** and returns `default!` on a 500 ms lock timeout. F-016 depends on it in
   four routes and *documented* the flake risk — it is why the T-006 assertion is "not 200-with-data" rather
   than "exactly 403" — but did not fix it. Pre-existing.
5. **`MongoDbRepository<T>.GetPagedAsync` has no direct test** — structurally untestable (live DB, and the
   driver's fluent chain ends in an extension method Moq cannot intercept). Covered end-to-end by 9 harness
   cases.
6. **Nine empty `METHOD()` placeholder tests** across `Library.Tests` and `EventsAndCommands.Tests` inflate the
   count. Not deleted: AC-19 forbids removing pre-existing tests and this feature already carries one such
   deviation. → **F-017 / F-019** as a batch.
7. **`EventAndCommands` is now ASP.NET-coupled** (`FrameworkReference` + `IHttpContextAccessor`). Accepted in
   ADR-027 with `IAuditActorProvider` named as the undo. → visible to **F-019 / F-020**.
8. **The Nordstrom standards gate has now failed to run five times** (F-013 ship, F-018 Define, F-016 Define,
   F-016 Plan, F-016 Review). Its six sources do not resolve under this `gh` auth and there is no local cache.
   A gate marked `enforcing` that has never executed is not enforcing anything → recommend folding into
   **F-017** or retiring it explicitly.

**Outside the feature, and still the highest residual risk:** the `agenda_buddy` Atlas credential
(`ISSUE-002`) is **still unrotated** and still recoverable from this public repo's history. It is exactly what
makes T06's fail-closed guard load-bearing rather than pedantic. Human-only.

---

## Agent Team

| Agent | Role in this episode |
|---|---|
| **Neo** (Architect) | Construction lead throughout; wave standups 3/4/6; blast radius; review synthesis. Caught the middleware-ordering trap and the three count errors. |
| **Bolt** (Backend) | Found that the AC-14 catch-site count was 7 not 8, and the ordering fix that keeps AC-13 from failing in Development only. |
| **Echo** (QA) | Coverage discipline; the AC-14 1-of-6 gap (fixed at the gate); the "assert not-200-with-data, not exactly-403" rule that kept T-006 from flaking on `CacheAside`. |
| **Phantom** (Security) | Threat-mitigation verification (7/7 with code *and* linked tests); the unprojected-cache and customer-payload findings; the unlogged-403 advisory. |
| **Jarvis** (Tech Writer) | `CLAUDE.md` staleness (fixed at the gate); CHANGELOG draft; found the CHANGELOG lives at `docs/pdlc/memory/CHANGELOG.md`, not the repo root. |
| **Pulse** (DevOps) | Wave-3 `DockerPreflight` findings; the CI path-filter gap that made a harness-only change run zero jobs. |
| **Muse** (UX) | **Did not participate** — triaged Skip (0/3), no user-facing UI surface. |
| **Atlas** (PM) | Reframed ADR-026 from a control question to a product question: *who is this endpoint for?* |

> ⚠️ **All meetings ran in `solo` mode** — one model roleplaying every agent — because the session carried a
> standing instruction not to spawn agents, which overrides STATE's `Party Mode: agent-teams`. Fidelity is
> lower than independent context windows. Recorded in every MOM and worth weighing when reading the findings.

---

## Deployment Record

| Item | Detail |
|---|---|
| **Deployed to** | `local` (Aspire AppHost) only, at `v0.2.0`. **No remote environment** — the `cloud` target's three blockers are unchanged, making this the **second consecutive release** to skip deploy. Recorded in `DEPLOYMENTS.md` rather than omitted. |
| **CI/CD method** | None exercised. `.github/workflows/deploy.yml` remains written, unit-tested and never run. |
| **Custom deploy artifact** | No — default pipeline. |
| **Deployment Review Party** | Not convened. |
| **Overrides used** | **None.** Four guardrail *warnings* were logged (standards gate unavailable ×2, test layers 3–6 absent, SSH.NET HIGH accepted per ADR-030) — logged warnings, not override ceremonies. |
| **Config changes introduced** | None at deploy level. The three AppHost secret parameters are unchanged. |
| **New tags recorded** | None. |
| **Rollback tested** | No. `v0.1.0` remains tagged and reachable, so a revert target exists, but reverting was not exercised. |
| **DEPLOYMENTS.md updated** | Yes — a `v0.2.0` history row plus the skipped-deploy record with reasons. |

### Ship / Verify evidence (2026-08-22)

Verified against a live AppHost built from `main` at `2134b8d`, because there is no deployed environment to
smoke-test. This is the feature's central claim exercised end to end rather than by inspection:

| Check | Result |
|---|---|
| All 7 services `/health` + `/alive` | ✅ 200 |
| The five formerly-anonymous PII GETs, anonymous | ✅ **401** — providers, providers/{email}, customers, customers/{email}, services/{email} |
| Both Calendar routes, anonymous | ✅ **401** |
| `GET /api/v1/professions` (reference data, must stay open) | ✅ **200** |
| `POST /api/v1/professions` (deleted, ADR-025) | ✅ **405** |
| register → login → authenticated read | ✅ 201 → 200 (RS256, 654-char JWT) → 200 |
| Pagination envelope | ✅ `{items, totalCount, page, pageSize}` |
| Owner reads own provider | ✅ full entity, 9 fields |
| **Non-owner reads the same provider** | ✅ `email, firstName, lastName, services` — no `appointmentEntities`, no `subscribedCustomerCollection`, no `kafkaTopic` |
| Dependency audit + secret scan on `main` | ✅ only the known ADR-030 `SSH.NET` HIGH; no secrets, no `.env` |
| Backend suite re-run on `main` | ✅ 358/358, 12 projects, 0 warnings |

**One new finding, filed as `agenda-buddy-xrw` (P2).** The list route served `totalCount: 0` while the
provider just created was readable by email. Cause: **no cache invalidation exists anywhere in the
solution** — nine `CacheAside.GetOrCreateAsync` read sites, zero `RemoveAsync` — against a 5-minute
absolute TTL. A provider who completes onboarding is absent from discovery for up to five minutes.
Pre-existing and not a regression: F-016 only moved the projection relative to the cache read (review
finding I-1). It is recorded here because Verify *demonstrated* it, where review had only inferred it.

---

## Reflect Notes

### What went well

1. **The harness earned its place immediately.** F-016 absorbed eight tasks from F-018's approved plan to
   build `AgendaBuddy.IntegrationTests` first, on the argument that endpoint authz was the one thing the
   solution could not verify. That argument held: the 99 integration tests are what turn "no endpoint leaks
   PII" from a claim into something checkable, and the Ship smoke test above only confirmed what they
   already asserted. The Calendar IDOR existed *because* 24 unit tests covered `OwnershipGuard` while
   nothing checked whether an endpoint called it.
2. **Three counting errors in approved artifacts were caught by grep, not by review.** 9 query handlers not
   10, 7 catch sites not 8, and two fields (`profession`, service `duration`) that appear in `api-contracts`
   §5.1 and exist on no entity. All three had already passed a PRD gate, a design gate and a plan gate.
3. **Breaking the contract while it had zero consumers was the right sequencing call.** Authn plus
   pagination on five routes is a breaking change; because the mobile client cannot reach those routes at
   all, it cost nothing. Doing it after F-015 would have meant rewriting the mobile client twice.
4. **Deviations were named rather than smoothed over.** The homogeneous-`ProviderSummary[]` decision
   contradicts the approved `api-contracts.md` §5.1 and says so in the code comment, in `verification.md`
   and here — with the reason (a mixed `items` array is not deserialisable into a typed list).

### What broke or slowed us down

1. **The standards gate has now blocked five consecutive gates and has never once executed.** F-013 ship,
   F-018 Define, F-016 Define, F-016 Plan, F-016 Review. A gate marked `enforcing` that has never run is
   governance theatre. It needs a reachable source (SSO/VPN or a vendored `.nordstrom-standards/`) or an
   explicit decision to retire it — recommended folding into F-017, which already owns §7's unimplemented
   scan gate.
2. **§7's security scan was satisfied by hand for the second consecutive release.** Greps are not a
   scanner. F-017 still owns automating it.
3. **The ship gate then sat open for four days.** Merge and tag happened 2026-08-18; Verify closed
   2026-08-22 with the STATE/DEPLOYMENTS edits sitting uncommitted in a working tree the whole time —
   invisible to anyone else on the repo. Episode 002 stayed `Draft` and review finding I-5 stayed open
   across the gap.
4. **All meetings ran in `solo` mode.** One model roleplaying every agent, because a standing instruction
   forbade spawning agents. Fidelity is lower than independent context windows; worth weighing when reading
   any finding above.

### What to improve next time

1. **Close the ship gate in the same session as the merge, or record explicitly that it is open.** The tag
   existing while STATE says `Verify` and the memory edits are uncommitted is the worst of both states.
2. **Resolve the standards gate before the next `enforcing` invocation** rather than logging a sixth skip.
3. **Verify by running, every time.** Both this episode and 001 found real defects only by exercising the
   running system — the cache-staleness finding here, six services unable to start in 001. Any acceptance
   criterion whose evidence is "code review" should be treated as unverified.

### Metrics snapshot

- **Cycle time:** 1 day (roadmap claim 2026-08-18T17:52Z → merged and tagged 2026-08-18). Ship gate closed
  2026-08-22, four days later.
- **Test pass rate:** 100% — 531 run, 0 failing (358 backend + 99 integration + 74 mobile, of which 7 skipped).
- **Tasks completed:** 20 / 20, across 8 waves.
- **Review findings:** 0 Critical · 5 Important (2 fixed, 3 accepted with rationale) · 7 Advisory.
- **Security findings (Phantom, Critical + Important):** 2 — the unprojected providers-list cache (I-1) and
  the full-`CustomerEntity` payload to any Provider-role caller (I-2). Both accepted, both quantified.
