# Verification — Secure Public Endpoints (F-016)

**Feature:** `secure-public-endpoints` (F-016) · **Task:** `F-016-T19` · **Date:** 2026-08-18
**Branch:** `feat/F-016-secure-public-endpoints` · **Not pushed, not merged.**

AC-19's attestation: what is verified, what is verified *differently from how the criterion was worded*,
and what is not verified at all. Written so a reviewer can disagree with a specific line rather than with a
summary.

---

## 1. Test gate

| Suite | Command | Result |
|---|---|---|
| Backend unit | `dotnet test agenda-buddy-backend.slnf` | **358 passing / 0 failing / 0 warnings**, 12 projects |
| Integration | `dotnet test AgendaBuddy.IntegrationTests/AgendaBuddy.IntegrationTests.csproj` | **92 passing / 0 failing**, 1 m 13 s |
| Mobile | `dotnet test MobileApp.Tests/MobileApp.Tests.csproj /p:MobileWorkloads=false` | **74 (67 passing, 7 skipped)** — untouched by F-016 |

Baseline at feature start: **305 backend**. AC-19 requires "305 or more". Backend grew 305 → 358 (**+53**),
and the integration suite went from **not existing** to 92 tests.

⚠️ **The integration suite is a separate command** and is *not* in `agenda-buddy-backend.slnf` (ADR-031), so
`dotnet test agenda-buddy-backend.slnf` does **not** run it. It has no CI job until `F-016-T20`.

---

## 2. The one AC-19 deviation

**AC-19 says: "No pre-existing test was deleted or skipped to achieve this." One was deleted.**

`Profession.Tests/Events/EventsHelperTest.AddProfessionEvent_ReturnSuccess`, removed by `F-016-T17`.

The claim being made, which is the maintainer's to accept or reject:

- AC-19 exists to stop a test being deleted **because it failed** — deleting evidence to make a change look
  green.
- This test's **subject** was deliberately removed. ADR-025 deletes `POST /api/v1/professions` and its
  write path, so `EventsHelper.AddProfessionEvent` does not exist and the test cannot compile. Keeping it
  would mean keeping the write path the task exists to remove.
- The requirement is **inverted and pinned harder**: `ProfessionWriteRouteRemovedTest` asserts over real
  HTTP that the route returns 404/405 for **both** roles, that no profession is written, and that the two
  read routes still return 200 anonymously.

**Net: −1 unit test, +3 integration tests.** A comment at the deletion site records the same reasoning.

One test was **updated** rather than deleted — `Customer.Tests EventsHelperTest`'s `GetCustomersEvent` case,
whose subject still exists with a new `PageRequest` parameter (T15). That is not an AC-19 event.

---

## 3. Acceptance criteria — 26

### Harness (AC-1 … AC-7)

| AC | Verified by | Status |
|---|---|---|
| 1 — `Persistence` rename, own commit, before any integration test | `EventsAndCommands.Tests/Persistence/PersistenceNamespaceTest.cs`; commit `9c64a0a` precedes `75e7e3d` | ✅ |
| 2 — suite discovers the project; 7 × `InternalsVisibleTo` | `Harness/InternalsVisibleToTest.cs` (8 cases) | ✅ ⚠️ *see note A* |
| 3 — no PEM/private-key material tracked; no production `.csproj` reference | `Library.Tests/Security/KeyMaterialHygieneTest.cs` (4 cases) | ✅ ⚠️ *see note B* |
| 4 — real service, real HTTP, real route, Mongo Testcontainer, container/class, DB/test | `Harness/ProfessionHostTest.cs` (3 cases) | ✅ ⚠️ *see note C* |
| 5 — fail closed on a non-container connection string, naming the host | `Harness/MongoEndpointGuardTest.cs` (8) + `MongoFailClosedTest.cs` (3) | ✅ |
| 6 — expired → 401; foreign subject → 403, on a real route | `Harness/AuthFailurePathTest.cs` (4) + `TokenFactoryTest.cs` (4) | ✅ |
| 7 — actionable runtime diagnostics, not an opaque timeout | `Harness/DockerPreflightTest.cs` (10); verified live with a bogus `DOCKER_HOST` | ✅ |

**Note A.** `InternalsVisibleTo` is **not** what enables hosting, contrary to AC-2's stated rationale. Seven
assemblies each emit an internal `Program` in the global namespace, so `WebApplicationFactory<Program>` is
ambiguous; `Harness/EntryPoints.cs` anchors each service to a distinct public type instead. AC-2 is
implemented as specified and its reason is recorded as wrong.

**Note B.** AC-3's second clause reads "no production `.csproj` **references**" the harness. Tested as
*no `ProjectReference`*, because seven production csprojs legitimately name it in `<InternalsVisibleTo>` for
AC-2. A string match would be red forever and the tempting fix would break AC-2 — a test pins that
distinction. AC-3 was also made *literally* true by deleting a dead hardcoded public-key PEM constant that
was the only committed PEM payload in the tree.

**Note C.** "Real HTTP" means a real `HttpClient` through the service's entire pipeline — routing,
authentication, authorization, model binding, the exception handler — against real MongoDB. The transport
is `TestServer`'s in-memory one, not TCP. That is what `Microsoft.AspNetCore.Mvc.Testing` provides and what
the T02 design selected.

### Exposure closure (AC-8 … AC-18)

| AC | Verified by | Status |
|---|---|---|
| 8 — the five PII GETs return 401 anonymously | `Harness/AnonymousPiiRoutesTest.cs` (10 cases: 5 × 401 + 5 authenticated controls) | ✅ |
| 9 — provider reads carry no appointments / subscribed customers for non-owners | `Harness/ProviderProjectionTest.cs` (4) | ✅ |
| 10 — both Calendar routes 403 for a non-owner, 200 for the owner | `Harness/CalendarOwnershipTest.cs` (5) | ✅ |
| 11 — `POST /providers` 403 for Customer role, and for a foreign email | `Harness/ProviderCreationGuardTest.cs` (3) | ✅ |
| 12 | — | 🚫 **struck at the Plan readiness party**; contradicted ADR-025. Replaced by AC-26 |
| 13 — a route with no local `try/catch` returns 403, not 500 | `Harness/CentralForbiddenTest.cs`, theory over Development **and** Production | ✅ |
| 14 — the hand-written catch sites still return exactly one 403, body unchanged | `Harness/LocalCatchUnaffectedTest.cs` (2 cases) | ✅ ⚠️ *see note D* |
| 15 — pagination, bounded page, total count, capped page size | `Harness/PaginationTest.cs` (9) + `Library.Tests/Dtos/PageRequestTest.cs` (15) | ✅ |
| 16 — the paginated shape recorded as an ADR before the endpoint work closes | **ADR-023**, written at Design and annotated with T15's findings | ✅ |
| 17 — the `events` document holds no provider/customer email or appointment record | `Harness/QueryAuditPayloadTest.cs` (3) + `EventsAndCommands.Tests` `QueryAuditTest` (6) | ✅ ⚠️ *see note E* |
| 18 — both profession read routes still 200 anonymously | `Harness/ProfessionWriteRouteRemovedTest.cs` + `ProfessionHostTest.cs` | ✅ |

**Note D — two count corrections, both verified by grep.**
There were **7** hand-written `ForbiddenException` catch sites, not the **8** stated by AC-14 and
`api-contracts.md` §3.1: `Booking:125,:149,:174`, `Customer:154`, `Provider:203`, `Services:143,:167`. T08
removed exactly one (Customer's, for AC-13), leaving **6**. A repo-wide grep returns 8 *today* only because a
test doc comment mentions the pattern — very likely how the original 8 arose.
Separately, **both 403 paths already return the same body.** The design assumed the hand-written
`TypedResults.Forbid()` sites returned a *bodyless* 403, so AC-14's "no changed body" meant tolerating two
contracts. `app.UseStatusCodePages()` converts a bodyless 403 into ProblemDetails, so the contract is
uniform. Both tests now assert the identical property set `{type, title, status, traceId, requestId}`. My
first AC-14 test asserted an empty body and failed, correctly. `api-contracts.md` §3.1 is corrected.

**Note E — there are NINE query handlers, not ten.** AC-17's broadening note, `ARCHITECTURE.md` §5, the plan
and T18's task body all say ten, inherited from `docs/pdlc/context/15-cqrs-and-messaging.md:161` — which
states *"10 queries, 10 handlers"* directly above a table listing **9**. Verified by grep: 9 query types, 9
handlers, **18** audit call sites. All 18 were changed. `ARCHITECTURE.md` is corrected; **the catalog line
still needs fixing at the next `/ship` context refresh.**

### Non-regression (AC-19, AC-20)

| AC | Verified by | Status |
|---|---|---|
| 19 — suite green, no test deleted | §1 | ⚠️ **verified with one deviation** — §2 |
| 20 — no success-path response semantics changed except by req. 10 and 15 | Reviewed per route | ⚠️ *see note F* |

**Note F.** Changed deliberately and within the carve-out: both list endpoints' envelope and the retirement
of their `204` (req. 15 / ADR-023), and the provider projection (req. 10). Changed **outside** it, and
flagged: `PUT /api/v1/customers/{email}`'s 403 **body** gains ProblemDetails, because T08 removed its local
`try/catch` to satisfy AC-13 — AC-13 explicitly authorizes that and speaks of *status* being unchanged, which
it is. `POST /api/v1/professions` is gone entirely (ADR-025, superseding req. 13).

### Threat-derived `[security]` criteria (AC-20 … AC-26)

All seven are materialized as structured ACs with **linked tests**. `tasks.cjs check` reports **zero**
`security-ac-untested` findings for F-016 — so `tasks.cjs done` could not have closed any of these tasks on a
citation alone.

| AC | Threat | Sev | Linked test | Status |
|---|---|---|---|---|
| 20 | T-002 | CRITICAL | `T002_AbortsDuringFixtureConstruction_AndCreatesNoDatabase` | ✅ |
| 21 | T-001 | HIGH | `T001_AssertOwner_WhenNeitherSubNorEntityEmailIsKnown_Throws` | ✅ **both halves** — *note G* |
| 22 | T-003 | HIGH | `T003_ACustomerRoleTokenGets403AndNoCustomerRecord` | ✅ |
| 23 | T-004 | MEDIUM | `T004_TheProductionForbiddenBody_CarriesOnlyStatusTitleAndRequestId` | ✅ |
| 24 | T-005 | MEDIUM | `T005_AnAuthenticatedReadIsAttributedToItsCallerAndRecordsNoPersonalData` | ✅ **literal route too** — *note H* |
| 25 | T-006 | MEDIUM | `T006_AWarmCacheIsNotServedToADifferentPrincipal` | ✅ |
| 26 | T-007 | MEDIUM | `T007_TheRouteIsGone_AndNoProfessionIsCreatedByAnyRole` | ✅ |

**Note G.** AC-21 has two halves and they landed in different tasks, deliberately. `AssertOwner(user, null)`
throws — T09, unit + `NullClaimOwnershipTest` over HTTP. And `GET /api/v1/providers/{email}` never returns
the full entity for a no-`sub` token — **T11**, because at T09 that route was neither authenticated nor
projected, so the test would have sat red for several tasks. The obligation was written into T11's task body
rather than left implicit; `ProviderProjectionTest.T001_*` closes it.

**Note H.** AC-24 is worded against `GET /api/v1/providers`, which was still anonymous when T18 landed — so
an *authenticated* read of it was impossible and T18 attested the criterion on the authenticated Calendar
route instead. T12 authenticated it, and T19 added
`T005_TheLiteralCriterion_AnAuthenticatedGetProvidersIsAttributedAndCarriesNoPii`. Both are kept; they
exercise different handlers of the nine.

---

## 4. Not verified, and why

| Item | Why |
|---|---|
| **`F-016-T20`** — the integration CI job | **Cannot be verified locally.** `main` is PR-protected and the pipeline is path-filtered, so it needs a real CI run on a throwaway branch **pushed by the maintainer**. The task graph cannot express that. |
| `MongoDbRepository<T>.GetPagedAsync` — Mongo's own `Skip`/`Limit`/`CountDocumentsAsync` semantics | Not unit-testable: both constructors need a live database and the driver's fluent chain ends in an extension method Moq cannot intercept. Exercised end-to-end by `PaginationTest`'s 9 cases against a real container. |
| CONSTITUTION §7 **Integration** checkbox | Deliberately left unchecked. The amendment is gated on **10 consecutive green integration runs**, tracked separately (inherited from F-018's T04, *not* absorbed). Do not tick it as a tidy-up. |
| CONSTITUTION §7 **Security scan** gate | Still unimplemented project-wide; owned by F-017. ADR-030 (SSH.NET) will be its first finding, expected disposition "accepted". |
| Atlas credential rotation (`ISSUE-002`) | Human-only, outside this feature — and it is exactly what makes T06's fail-closed guard load-bearing rather than pedantic. |
| Anything under the AppHost / a running cluster | F-016 changes no Aspire wiring. The 47 AppHost tests still pass. |

---

## 5. Design documents corrected by implementation

Recorded because a design doc that survives contact with the code unchanged usually means nobody checked.

| Document | Correction |
|---|---|
| `api-contracts.md` §3.1 | 7 catch sites, not 8; both 403 paths already share one body shape |
| `api-contracts.md` §5.1 | **No `profession` field and no service `duration`** — neither exists on `ProviderEntity`/`ServiceEntity`. F-015 would have bound to fields that are not there. Also: the list is **homogeneous** `ProviderSummary[]`; owner-gets-full applies to §5.2 only, because a mixed `items` array is not deserialisable into a typed list |
| `ARCHITECTURE.md` §5 | 9 query handlers, not 10; and `Event.actor` is **not** "one assignment per handler" — no handler can see the caller |
| `ADR-023` | Annotated: DB-level paging, the cache key must carry the page, `skip` overflow guard |
| `ADR-027` | Amended with the chosen mechanism (central stamp in `EventStore`) and the rejected alternative |
| `docs/pdlc/context/15-cqrs-and-messaging.md:161` | ⚠️ **Not yet corrected** — "10 queries, 10 handlers" above a 9-row table. Needs the `/ship` context refresh |
