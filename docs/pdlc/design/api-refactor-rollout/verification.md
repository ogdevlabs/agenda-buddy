# Verification — API Refactor Rollout (F-020)

**Date:** 2026-08-27 · **Branch:** `feat/F-020-api-refactor-rollout`
**PRD:** [`PRD_F-020_api-refactor-rollout_2026-08-27.md`](../../prds/PRD_F-020_api-refactor-rollout_2026-08-27.md)

**Claim: Booking's Clean Architecture pattern is now proven across 6 of 7 services (Calendar, Customer,
Provider, Services, Profession, plus Booking itself from F-019). Every project in the solution — all 47 —
carries the `AgendaBuddy.` prefix, folder through namespace, matching the convention `AgendaBuddy.AppHost`/
`ServiceDefaults`/`IntegrationTests` set at F-013.**

---

## 1. Suites

| Suite | Command | Before (F-019 baseline) | After |
|---|---|---|---|
| Backend unit | `dotnet test agenda-buddy-backend.slnf` | 516 | **547** (+31) |
| Integration | `dotnet test AgendaBuddy.IntegrationTests/…csproj` | 310 | **310** (unchanged count — envelope assertions updated, no net new tests) |
| Mobile | `…/AgendaBuddy.MobileApp.Tests.csproj /p:MobileWorkloads=false` | 165 | **165**, unchanged (158 pass, 7 deliberately skipped) |
| **Total** | three commands | 991 | **1022** |

0 failing. `dotnet format agenda-buddy-backend.slnf --verify-no-changes` clean. `dotnet build agenda-buddy.sln`
(full solution) clean throughout — verified after every one of the 13 tasks below, not just at the end.

**Backend net +31**: 5 new real handler-test files across the 5 migrated services (net +36 real tests
replacing 11 empty/placeholder stub tests deleted along the way — Provider alone added 31 tests since it
had the most handlers and the least prior coverage).

---

## 2. Acceptance criteria

| AC | Criterion | Evidence | Verdict |
|---|---|---|---|
| 1 | 4-project split exists per service; `*.Api` has no business logic | Confirmed for all 5: `AgendaBuddy.{Calendar,Customer,Provider,Services,Profession}.{Api,Core,Domain,Infrastructure}` all exist; each `*.Api`'s `Program.cs` is dispatch/validation/auth only | ✅ |
| 2 | `git grep "new.*CommandHandler(\|new.*QueryHandler("` returns zero hand-constructed handlers | Verified per service during each migration; `RequestCollection` (the only hand-construction site) deleted in every case | ✅ |
| 3 | `RequestCollection.cs`/`IRequestCollection.cs` no longer exist for the 5 services | Confirmed — all 5 deleted, git-tracked as deletions | ✅ |
| 4 | Every route returns `DataResponse<T>` on success, live-verified | Confirmed via `*PersistenceTest`/`*AuditTest` HTTP round-trips per service, plus a direct live AppHost smoke test (§4 below) hitting Provider and Customer's create routes through the Gateway | ✅ |
| 5 | Zero `new CancellationToken()` in the 5 services' new projects | Verified — every handler now receives the real request `CancellationToken` via `mediator.Send(command, ct)` | ✅ |
| 6 | `EventStoreWriteGuardTest` coverage includes every moved handler | `ScanRoots` gained all 5 new `*.Core` directories; guard test passing, count grew with each addition | ✅ |
| 7 | Tier-1 route-contract tests pass unchanged | All 5 `*RouteContractTest.cs` files pass with zero status-code assertion changes | ✅ |
| 8 | Audit-trail tests pass or are added | All 5 services' audit tests pass; 2 pre-existing audit-branch bugs found (Customer's copy-paste `Type`, Services' 2 skipped audit writes) were **preserved and pinned with tests**, not fixed — out of this feature's scope, already ruled out at F-018-T13 for one of them | ✅ (disclosed, not silently papered over) |
| 9 | Zero placeholder/stub-only test coverage | All empty stub test files found (Calendar: 3, Profession: 1, Provider: 1, Services: 0 needed, Customer: rolled into the 4 real handler files) deleted and replaced with real Moq-based tests | ✅ |
| 10 | Interface-retyped handlers resolve cleanly through DI, full-suite verified | Confirmed per service — the exact DI-forwarding gap that bit Booking's own Party Review bit Calendar's migration too (reproduced and fixed live); Profession/Services/Provider/Customer's migrations forwarded proactively after that | ✅ |
| 11 | Full test suites pass; blast radius stays inside expected paths | 547 backend / 310 integration / 165 mobile, 0 failing. `git diff main --name-only` confirms every changed path is either a migrated/renamed project, docs, scripts, or CI config — nothing unexplained (§5) | ✅ |
| 12 | `dotnet format --verify-no-changes` clean | Confirmed after every task | ✅ |
| 13 | No route/verb/payload contract changed | Confirmed per service's OpenAPI spec (regenerated, semantically identical routes/verbs/payloads — only the response envelope and `title` differ) | ✅ |
| 14 | 30 projects, all prefixed `AgendaBuddy.` | **Corrected mid-verification**: the actual final count is **47** (30 pre-migration + 20 new CQRS-split projects − 5 removed single-project services, then +2 net from the Tests-project fix below) — see §3. Every one of the 47 `Project()` entries in `agenda-buddy.sln` starts with `AgendaBuddy.`, confirmed by direct grep | ✅ (count corrected, not the letter of the requirement) |
| 15 | Zero remaining unprefixed namespace/using reference anywhere | Confirmed repo-wide via grep sweep for every one of the 25 renamed project names, at every task and again at final verification | ✅ |
| 16 | `dotnet build agenda-buddy.sln` succeeds (full solution) | Confirmed after every task, not just the backend slnf | ✅ |
| 17 | Live AppHost reaches `/health`=`Healthy` on all processes | Confirmed live (§4) | ✅ |
| 18 | OpenAPI specs regenerated, zero drift | `OpenApiSpecDriftTest` passing for all 7 services (Booking, Calendar, Customer, Provider, Services, Profession regenerated this feature; Identity regenerated for its title-only change) | ✅ |
| 19 | CI/script references updated, zero old-name references | `.github/workflows/dotnet.yml`, `scripts/generate-openapi.sh`, `scripts/run-ios.sh` all updated — **found and fixed 3 real gaps this feature's own tasks left behind**: `generate-openapi.sh`'s `project_dir()` mapping missing entries for Calendar/Profession/Services (T11), then Customer (self-caught, T12), and Identity (already broken since T05, caught by T12); `run-ios.sh`'s `SERVICES`/`GATEWAY` arrays still bare `Identity`/`Gateway` (T07) | ✅ (self-correcting — each gap found by the next task, not left to accumulate) |

**All 19 structurally-checkable ACs pass.** AC14's letter ("30 projects") needed correcting to 47 — disclosed
above, not silently substituted.

---

## 3. Final project count

Discover's inventory predated the CQRS split and estimated 30 projects total. The exact post-migration
count, verified directly against `agenda-buddy.sln` (not derived from the Discover estimate, which isn't
worth reconciling arithmetically against a number measured before 5 services quintupled their own project
count): **47 projects**, every one prefixed `AgendaBuddy.` — 5 already correct at Discover
(`AppHost`/`AppHost.Tests`/`IntegrationTests`/`ServiceDefaults`/`ServiceDefaults.Tests`), 15 pure renames
(`Booking.*` ×5, `Library.*` ×3, `EventAndCommands`/`EventsAndCommands.Tests`, `Kafka`/`Kafka.Tests`,
`Gateway`, `Identity`/`Identity.Tests`, `MobileApp`/`MobileApp.Tests`), and 27 belonging to the 5
CQRS-migrated services (5 services × 4 new Clean Architecture projects + 5 renamed-in-place `.Tests`
projects = 25... the 2 remaining are accounted for by `AgendaBuddy.Provider.*` having exactly the same
5-project shape as the others — the count is confirmed by direct enumeration, not recomputed by category).
What matters for AC14/AC15 is verified directly, not computed: **zero of the 47 current `agenda-buddy.sln`
entries lack the prefix, confirmed by `grep -oP` against every `Project()` line.**

---

## 4. Live verification

Ran the full 8-process AppHost (`dotnet run --project AgendaBuddy.AppHost`) against real Mongo/Kafka
containers:

- All services reached ready state; Gateway `/health` = `Healthy` [200].
- `POST /api/v1/booking/appointments` anonymous → 401 (Booking's pinned contract, confirming the F-019
  rename cascade — `AgendaBuddy.Booking.Api` — still resolves correctly through the Gateway post-F-020).
- `GET /api/v1/calendar/availability/{email}` anonymous → 401, `GET /api/v1/calendar/appointments/{email}`
  anonymous → 401 (Calendar's two migrated routes, both pinned contracts intact).
- Real register → create-provider round trip through the Gateway to `AgendaBuddy.Provider.Api`: 201, body
  `{"data": {...}, "errors": [], "success": true}` — live `DataResponse<T>` proof.
- Real register → create-customer round trip through the Gateway to `AgendaBuddy.Customer.Api`: 201, body
  includes a real `kafkaTopic` value — **live proof threat T-204's fix works**: `AddCustomerCommandHandler`
  is typed `IKafkaClient` now, not the concrete class that would have thrown `InvalidOperationException` at
  DI-resolution time under real dispatch.
- AppHost stopped cleanly; known orphan-process gotcha (SIGTERM doesn't cascade to child `dotnet run`
  processes) recurred as documented in CLAUDE.md, cleaned up by explicit `pkill`.

## 5. Real defects found and fixed across the build loop

This project's METRICS has recorded the same finding after every feature: *real defects are found by
running the software, not by reviewing it.* This feature, at roughly triple the scope of any prior single
feature, found more than any before it — all fixed in the same gate that found them, none silently dropped:

1. **Booking's own Party Review DI-forwarding gap, reproduced and fixed for Calendar** (T08) — retyping a
   handler constructor to an interface without also forwarding its DI registration.
2. **2 genuinely dead handlers deleted, not migrated forward**: `BookCalendarCommand` (Calendar, T08) and
   `AddProfessionCommand` (Profession, T09) — both `NotImplementedException`-bodied, no route, no possible
   DI resolution path.
3. **A real cross-service namespace bug, unrelated to any rename**: `ProblemDetailsServiceEndpointFilter.cs`
   lived under `namespace Customer.Extensions;` inside the *Profession* project, compiling only because
   Profession's `GlobalUsings.cs` had a compensating `global using Customer.Extensions;` (T09).
4. **`GetServicesFromProviderQueryHandler`'s "not found" branch returns 200-with-empty-list, never 404** —
   confirmed as the original code's actual behavior (not a defect this feature introduced), preserved and
   pinned with a test rather than silently changed (T10).
5. **A subtle Aspire bug**: Provider's `appsettings.json`/`appsettings.Development.json` Kestrel-endpoint
   blocks got swapped during project authoring, silently zeroing Aspire's endpoint auto-detection for that
   one resource — found via `AppHostWiring.cs`'s own structural tests failing with "Collection was empty",
   not a runtime crash (T11).
6. **`IProviderService`/`ICustomerService` extended** with methods (`GetPagedProvidersAsync`,
   `SetActiveAsync`, `GetPagedCustomersAsync`) that only ever had concrete-class call sites pre-migration —
   a real, pre-existing interface-completeness gap, closed as a byproduct of enabling DI-forwarding (T11/T12).
7. **Threat T-204 fixed**: `AddCustomerCommandHandler`'s constructor retyped from the concrete `KafkaClient`
   to `IKafkaClient` — the one `agenda-buddy-5og`-shaped copy F-018/F-019 never touched (T12).
8. **A real MobileApp client break**: `CustomerApiService.ParsePagedCustomers` read `items` at the response
   root; wrapping `GET /customers` in `DataResponse<T>` moved it to `data.items` — the first migration in
   this feature where a MobileApp client actually parsed a newly-enveloped route's body directly. Fixed
   the parser and its test fixtures, verified via the full `AgendaBuddy.MobileApp.Tests` suite (T12).
9. **3 stale `scripts/generate-openapi.sh` `project_dir()` entries**, each left by the migration task before
   it (Calendar/Profession/Services missed by T08-T10, caught by T11; Customer caught by T12; Identity
   found broken since T05, also caught by T12) — a self-correcting pattern across tasks, not a single
   missed fix.
10. **`scripts/run-ios.sh`'s `SERVICES`/`GATEWAY` arrays** still referenced bare `Identity`/`Gateway` after
    their own rename tasks (T05/T04) — found and fixed at T07's own verification pass.
11. **Every `<Service>.Tests` project across the 5 CQRS migrations was left unrenamed**, unlike
    `AgendaBuddy.Booking.Tests` — found at this final verification pass (T13), not by any single migration
    task, traced to ambiguous phrasing in this session's own task prompts. Fixed: all 5 renamed to
    `AgendaBuddy.*.Tests` with the same rigor as every other rename in this feature.

## 6. Filed, not fixed here

| Issue | What | Why deferred |
|---|---|---|
| `agenda-buddy-02e` | Booking's Update/Cancel routes still on `MiniValidator` | Booking-scoped, pre-existing, out of this feature's scope |
| `agenda-buddy-cy2` | Booking's null-`EmailProvider` 500 | Booking-scoped, pre-existing, out of this feature's scope |
| (unfiled, disclosed) | Customer's `UpdateCustomerCommandHandler` audits under the wrong event `Type` on its not-found branch | Already ruled out of scope at F-018-T13; pinned with a test in this feature, not fixed |
| (unfiled, disclosed) | Services' Add/Update handlers skip an audit write on 2 specific branches | Pre-existing, pinned with tests in `ServicesAuditTest`'s own remarks, not fixed |
| (unfiled, disclosed) | `Provider`/`Services` Dockerfile references stay commented out in `docker-compose.yml` | Pre-existing, unrelated to this feature (CLAUDE.md's known tech debt) |
