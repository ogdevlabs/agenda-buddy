# Verification — API Gateway and Mobile Contract (F-015)

**Date:** 2026-08-24 · **Branch:** `feat/F-015-api-gateway-and-mobile-contract`
**PRD:** [`PRD_F-015_api-gateway-and-mobile-contract_2026-08-23.md`](../../prds/PRD_F-015_api-gateway-and-mobile-contract_2026-08-23.md)

**Claim: a provider or customer using `MobileApp` against a live AppHost sees their real data, on every
screen, with zero fabricated fallback — ever.**

**This claim is now true for every screen, including Messaging and Notifications.** §3 records a real defect
this gate found by running the software — the Gateway's route allowlist had no entry for either — and the
fix that closed it before Construction ended, with a regression test added at the exact layer that missed it
the first time (the Gateway's own routing table, not the client or the backend).

---

## 1. Suites

| Suite | Command | Before (F-014 baseline) | After |
|---|---|---|---|
| Backend unit | `dotnet test agenda-buddy-backend.slnf` | 452 | **468** (+16) |
| Integration | `dotnet test AgendaBuddy.IntegrationTests/…csproj` | 175 | **234** (+59) |
| Mobile | `…/MobileApp.Tests.csproj /p:MobileWorkloads=false` | 74 (67 passing, 7 skipped) | **165** (158 passing, 7 skipped) (+91) |
| **Total** | three commands | 701 | **867** |

0 failing across all three suites, re-run in full after the §3.1 fix (not taken on faith from the task
closing notes). Integration duration **2 m 37 s** against the 600 s CI budget, against a real MongoDB
Testcontainer (Rancher Desktop). The backend `slnf` now also carries `Gateway` itself as a 13th, non-test
project — it builds under the same command but contributes no test count of its own; its behavior is proved
by `AgendaBuddy.IntegrationTests`' Gateway-hosted tests instead. The final +4 (863→867) are the regression
tests §3.1's fix added.

**The +55 integration tests are where the gateway's own claims live** (routing allowlist, JWT passthrough,
failure translation, transport-security parity, logout/refresh) — the same shape of observation F-014's
verification made about its own +57: a unit test on a class proves nothing about whether a client can reach
it.

---

## 2. Acceptance criteria

| AC | Criterion | Test | Live evidence (this gate) | Verdict |
|---|---|---|---|---|
| 1 | Real dashboard/calendar/customers/messages/notifications data, zero `SeedDataProvider`, whether the cause was failure or emptiness | `DashboardViewModelTests.LoadAsync_Success_SetsAppointmentsAndClearsError`, `LoadAsync_NetworkError_SetsHasErrorTrueWithRealMessage_NoFabricatedData`, `LoadAsync_EmptyResult_SetsIsEmptyTrue_NoFabricatedData`; `CalendarViewModelTests` (same three shapes); `SeedDataProviderRemovalTests.SeedDataProviderType_NoLongerExistsInMobileAppAssembly` | Registered provider+customer, created a Provider/Customer/Appointment through the gateway, read it back via `GET api/v1/calendar/appointments/{email}` — real data, not seed fixtures. `GET api/v1/notifications` returned a genuine `[]`, not a fabricated list | ✅ |
| 2 | Every `*ApiService` call resolves against its backend route with 2xx or a correctly-typed error — not a 404 from a wrong path/verb/prefix — verified live | `MobileClientRouteResolutionTest` (13 tests) + `GatewayRoutingTest.RouteTable_MapsTopLevelCustomerGroupsToTheCustomerCluster`/`AllowlistedPrefix_IsRoutedNotRejected(path: "/api/v1/messages"\|"/api/v1/notifications")` (added by this gate's §3.1 fix) | register/login/create-provider/create-customer/book/status-transition/notes(GET+POST)/payment(GET+POST)/report/calendar all returned 2xx **through the gateway**. `messages`/`notifications` initially returned `gateway-no-route` 404 through the gateway (§3.1) — fixed in the same gate; re-verified routed (not 404) after the fix | ✅ *(found `⚠️ Partial` mid-gate, fixed before this row was closed — see §3.1)* |
| 3 | JWT forwarded unmodified; destination validates exactly as a direct call would | `GatewayJwtPassthroughTest.AC3_AValidJwt_TransitionsTheAppointment_ExactlyAsADirectCallWould`, `AC3_AValidJwtForTheWrongRole_IsForbidden_ExactlyAsADirectCallWould`, `AC3_AValidJwtForAStranger_IsForbidden_ExactlyAsADirectCallWould` | Provider JWT completed the status transition (200); the same route with the customer's JWT got 403 — the destination's own role check, reached unmodified through the gateway | ✅ |
| 4 | Anonymous/invalid JWT gets the same 401/403 a direct call would, never a proxied 200 | `GatewayJwtPassthroughTest.AC4_AnAnonymousRequest_Gets401_ExactlyAsADirectCallWould`, `AC4_AnExpiredJwt_Gets401_...`, `AC4_ATamperedJwt_Gets401_...` | `GET api/v1/calendar/appointments/{email}` with no header → 401; with `garbage.invalid.token` → 401 | ✅ |
| 5 | A stopped service returns a `failedService`-named error; the other six keep working | `GatewayFailureTranslationTest.AC5_UnreachableDestination_ReturnsTheShapedProblemDetails`, `AC5_TheOtherSixServices_AreUnaffectedByOneBeingDown` | `aspire resource profession-smcxvqes stop` → `GET api/v1/professions` through the gateway returned `502` + `"failedService":"profession"`; `GET api/v1/customers` (a different cluster) returned `200` in the same window | ✅ |
| 6 | Gateway keeps routing correctly after a backend's dynamic port is reassigned, without the gateway restarting | `AppHostWiringTest.GatewayIsRegistered`/`GatewayReferencesEveryService`/`GatewayWaitsForEveryService` (structural); `ARCHITECTURE.md` §6's spike (live, `aspire resource booking restart` ×2, F-015-T02) | Re-verified independently at this gate: `aspire resource profession-smcxvqes stop` then `start` (a fresh process, new dynamic port) — `GET api/v1/professions` through the gateway returned `200` afterward, gateway process never restarted | ✅ |
| 7 | Status update uses `POST .../status`; customer UI never offers "mark complete" | `BookingRouteBuilderTests.UpdateAppointmentStatus_BuildsPostToStatusRoute`, `BuildUpdateStatusPayload_SerializesStatusAsStringProperty`; `AppointmentDetailViewModelTests.ShowCompleteButton_CustomerSession_IsFalse`, `CompleteCommand_CustomerSession_CanExecuteIsFalse`, `ShowCompleteButton_ProviderSession_IsTrue`, `CompleteCommand_ProviderSession_CanExecuteIsTrue` | Provider: `POST api/v1/booking/appointments/{id}/status {"status":"Booked"}` → 200. Same route, customer JWT, `{"status":"Completed"}` → 403 | ✅ |
| 8 | Failure → error banner + retry; genuine zero-result → empty-state UI; `SeedDataProvider` unreachable | `CalendarViewModelTests`/`DashboardViewModelTests.LoadAsync_NetworkError_SetsHasErrorTrueWithRealMessage_NoFabricatedData`, `LoadAsync_EmptyResult_SetsIsEmptyTrue_NoFabricatedData`; `SeedDataProviderRemovalTests` (both tests, by reflection over the compiled assembly) | `GET api/v1/notifications` on a fresh account returned a genuine `[]` (200) — the shape `IsEmpty` now reaches, not a 404 masked as "use seed" | ✅ |
| 9 | Access-token expiry mid-session triggers a transparent refresh; session isn't silently dropped while a refresh token is valid | `JwtDelegatingHandlerTests.SendAsync_On401_RefreshSucceeds_RetriesOriginalRequestWithNewTokenAndNoEvent`, `SendAsync_On401_RefreshSucceeds_RetriesPostWithOriginalBody` | Not exercised live — the access token's ~60-minute lifetime makes waiting for a real expiry impractical within this gate's time budget, exactly the tradeoff the task brief anticipated. Relying on T09's unit-level proof (mocked `HttpMessageHandler` producing a 401 then a 200) for the 401→refresh→retry mechanism specifically. **Supporting live evidence, not a substitute:** `POST api/v1/auth/refresh` was called live through the gateway and returned a fresh access+refresh token pair, so the endpoint the handler depends on is real and reachable | ✅ (by T09's suite; live-supported, not live-proved end to end) |
| 10 | A non-idempotent write that times out ambiguously is never silently auto-retried | `JwtDelegatingHandlerTests.SendAsync_PostTimesOut_ThrowsAmbiguousWriteException_AndDoesNotRetry`, `SendAsync_PutTimesOut_ThrowsAmbiguousWriteException`, `SendAsync_PostReturns502FromGateway_ThrowsAmbiguousWriteException`, `SendAsync_PostReturns504FromGateway_ThrowsAmbiguousWriteException` (plus the negative control `SendAsync_GetReturns502FromGateway_DoesNotThrow`) | Not exercised live — reproducing a genuine gateway-hop timeout against a live backend on demand isn't practical without artificially blocking a socket, which risks leaving the AppHost in a bad state. Relying on T09's suite, which simulates the timeout/502/504 conditions directly against `HttpMessageHandler` | ✅ (by T09's suite) |
| 11 | Logout calls the server; the old refresh token is rejected afterward | `LogoutTest.Logout_ThenRefresh_TheOldRefreshTokenIsRejected`, `Refresh_WithAValidUnexpiredToken_MatchesTheCredential`, `Logout_WithATokenNoCredentialHolds_IsStillNoContent`; `AuthServiceTests.LogoutAsync_WithStoredRefreshToken_PostsToLogoutRouteAndClearsStorage` | **Fully proved live, through the gateway, with a fresh account:** registered `logout.verify@agendabuddy.dev`, `POST api/v1/auth/logout` with its refresh token → 204; `POST api/v1/auth/refresh` with the *same, never-otherwise-used* token → 401. (Also incidentally reconfirmed F-021's single-use refresh rotation: reusing an already-rotated token independently returns 401 too.) | ✅ |
| 12 | Route/base-URL resolution is covered by tests under the CI-run test project, not only the `#if MOBILE` Maui bootstrap | `MobileApp.Tests/Routing/*RouteBuilderTests.cs` (7 files, e.g. `BookingRouteBuilderTests.UpdateAppointmentStatus_BuildsPostToStatusRoute`); `ApiBaseUrlResolverTests.Resolve_EnvironmentVariableSet_WinsOverConfigurationAndFallback`, `Resolve_NoEnvironmentVariable_FallsBackToConfiguration`, `Resolve_NoEnvironmentVariableAndNoConfiguration_FallsBackToHardcodedDefault` | `dotnet test MobileApp.Tests/MobileApp.Tests.csproj /p:MobileWorkloads=false` run at this gate: 165 total, 158 passed, 7 skipped — all of the above execute under the plain `net10.0` fallback TFM, no Maui bootstrap required | ✅ |
| 13 | Report renders `revenueUnavailableReason` (never a number/blank); notifications empty state (not an error); payment copy never claims a `local_` payment was charged | `ProviderReportViewModelTests.LoadAsync_RevenueUnavailable_RendersExactCopyWithReason`, `LoadAsync_RevenueUnavailable_NeverRendersANumberOrBlank`; `PaymentViewModelTests.LoadAsync_LocalIntentSucceeded_RendersRecordedNotChargedCopy_NeverPaid`, `LoadAsync_LocalIntentAnyStatus_IsAlwaysNonCharging`; `NotificationsViewModelTests.LoadAsync_EmptyResult_RendersExactEmptyStateCopy` | `GET api/v1/providers/{email}/report` returned `"revenueAvailable":false,"revenueUnavailableReason":"Appointments do not record which service they were booked for, so revenue cannot be computed from stored data."` — never a number. `POST .../payment` recorded `"stripePaymentIntentId":"local_bb4298b061fd416d"` — the exact non-charging shape the copy keys off | ✅ |
| 14 | `[security]` T-302 — a path outside every `api/v1/{service}/**` prefix gets `gateway-no-route` 404, not a proxied response | `GatewayNoRouteTest.T302_UnmappedPath_Returns404NotProxied` (3 probe cases) | `GET /booking/health` (no `api/v1` prefix) → `gateway-no-route` 404. `GET /api/v1/messages/health` (unmapped prefix) → `gateway-no-route` 404. Contrast: `GET /api/v1/booking/health` (mapped prefix, unmapped sub-path) *is* proxied and gets Booking's own ordinary 404 — correct, and a useful confirmation the allowlist is prefix-scoped, not path-exact | ✅ |
| 15 | `[security]` T-303 — a backend's HSTS/redirect behavior through the gateway matches a direct call | `GatewayTransportSecurityParityTest.T303_GatewayForwardedRequest_TransportSecurityBehaviorMatchesDirectCall`, `T303_WithTheDefaultConfiguration_NeitherPathSendsHsts` | Not independently re-verified live — this project's local topology has no HTTPS endpoint on any service (`UseHttpsRedirection()` is a no-op everywhere today per T04's own finding), so there is no live redirect behavior to observe either through the gateway or directly. Relying on T04's suite, which additionally mutation-tested itself (temporarily added `UseForwardedHeaders()` to Profession, confirmed the test goes red, reverted) | ✅ (by T04's suite) |

**AC2 is the one criterion this gate downgrades from the task closing notes' claim.** F-015-T07's own
verification cited `MobileClientRouteResolutionTest` as proof "against a live AppHost," which is true for
where it points — but it constructs requests from `MobileApp.Routing.*RouteBuilder` output and fires them
directly at the hosted domain services (`ServiceHostFixture<BookingAnchor>` etc.), never through
`Gateway/Program.cs`'s actual route table. That is a legitimate thing to test (it proves the client's route
strings are correct), but it is not the same claim as "resolves through the gateway," which is what
`MobileApp` actually does at runtime. Running the real Gateway against real backends at this gate is what
surfaced the gap in §3.

---

## 3. One defect found by running the software, invisible to all 863 automated tests — fixed in this gate

### 3.1 🔴→✅ The Gateway's route allowlist had no entry for `api/v1/messages/**` or `api/v1/notifications/**`

**Reproduction, live, through the real Gateway (port 5000) in front of a real Customer service:**

```
$ curl -X GET http://localhost:5000/api/v1/messages -H "Authorization: Bearer <valid customer JWT>"
{"type":"https://agendabuddy.dev/errors/gateway-no-route","title":"No backend service matches this path",
 "status":404,"detail":"No destination configured for '/api/v1/messages'.", ...}

$ curl -X GET http://localhost:59462/api/v1/messages -H "Authorization: Bearer <same JWT>"   # Customer directly
[]
```

The same pattern holds for `api/v1/notifications`. Both routes exist, are correctly authenticated and
authorized, and return the right shape — confirmed by calling Customer's own bound port directly, bypassing
the Gateway. The problem is entirely in `Gateway/AspireServiceDiscoveryProxyConfigProvider.cs`'s
`_routeSpecs` allowlist (F-015-T03):

```csharp
private static readonly (string ServiceName, string RouteId, string PathPattern)[] _routeSpecs =
[
    ("booking", "booking", "/api/v1/booking/{**catch-all}"),
    ("calendar", "calendar", "/api/v1/calendar/{**catch-all}"),
    ("customer", "customer", "/api/v1/customers/{**catch-all}"),
    ("provider", "provider", "/api/v1/providers/{**catch-all}"),
    ("services", "services", "/api/v1/services/{**catch-all}"),
    ("profession", "profession", "/api/v1/professions/{**catch-all}"),
    ("identity", "identity-auth", "/api/v1/auth/{**catch-all}"),
    ("identity", "identity-device-token", "/device-token"),
];
```

`api/v1/messages` and `api/v1/notifications` are **top-level route groups on the Customer service**
(`Customer/Program.cs:255,333` — `app.MapGroup("/api/v1/messages")`, `app.MapGroup("/api/v1/notifications")`),
not nested under `/api/v1/customers`. F-015-T03 built its allowlist against
`docs/pdlc/context/01-api-surface.md`, which is the pre-F-014 catalog and has never listed these two groups
at all (F-014 added them; the context catalog was never refreshed for that — see the catalog corrections
this same task made). No task in F-015's plan re-checked the allowlist against F-014's actual route table
after the fact.

**Why none of the 863 automated tests caught it:**
- `GatewayRoutingTest`/`GatewayNoRouteTest` (F-015-T03) test exactly the seven-prefix allowlist that exists
  — they were never asked to check for a prefix that should exist but doesn't.
- `MobileClientRouteResolutionTest` (F-015-T07) fires requests built from `MobileApp.Routing.*RouteBuilder`
  output directly at the hosted domain services, never through `Gateway/Program.cs` — see AC2's note above.
- `GatewayJwtPassthroughTest`/`GatewayFailureTranslationTest` (F-015-T04) exercise the Booking cluster only.
- No test anywhere in the three suites constructs a real Gateway in front of a real Customer service and
  asks for `/api/v1/messages` or `/api/v1/notifications` through it.

**Impact:** `MobileApp`'s Messaging and Notifications screens — both real, both correctly implemented at the
route/verb/payload/copy layer (AC2, AC13's notifications half) — cannot reach the backend in practice,
because the one address the app is configured to call has no path to them. This is the same shape of gap
F-015 exists to close for the other five capability areas, recurring in the one place F-015's own plan didn't
re-check the allowlist against F-014's route table.

**Fixed in this gate, not deferred.** F-015-T14 found the gap; because it directly contradicts F-015's own
claim (a provider/customer sees their real messages and notifications, which requires reaching them through
the one address the client calls), it was fixed rather than filed. The fix was exactly the two-line addition
predicted above — `("customer", "customer-messages", "/api/v1/messages/{**catch-all}")` and
`("customer", "customer-notifications", "/api/v1/notifications/{**catch-all}")` added to `_routeSpecs`
(`Gateway/AspireServiceDiscoveryProxyConfigProvider.cs`). One pre-existing test needed a matching fix:
`GatewayRoutingTest.RouteTable_MapsEachDomainPrefixToItsOwnCluster`'s customer case used
`Assert.Single(config.Routes, r => r.ClusterId == clusterId)`, which broke once "customer" stopped being a
single-route cluster — changed to filter by `RouteId` (still unique per route) instead, which is what the
test actually meant to assert. Two new tests added as a direct regression guard: a
`RouteTable_MapsTopLevelCustomerGroupsToTheCustomerCluster` theory (asserts both new `RouteId`s map to the
`customer` cluster with the right path) and two new cases on `AllowlistedPrefix_IsRoutedNotRejected`
(`/api/v1/messages`, `/api/v1/notifications`) so this exact regression is caught by the routing suite next
time, not left to a live verification pass to rediscover. Suites re-run in full after the fix (§1's After
column); `RouteTable_HasExactlySevenClusters_NoMoreNoFewer` still passes unchanged — the fix adds routes to
an existing cluster, not a new one.

### 3.2 A minor, unexplained observation — not diagnosed, not claimed as a defect

While exercising AC5 (stopped-service test), a `GET api/v1/customers` response for the provider's own view
of the just-created customer returned `"id":"000000000000000000000000"` instead of the real ObjectId the
`POST` had returned moments earlier (`"id":"6a8bcd4b35083aa5ed7f8b6d"`). This could be a stale cache entry
inserted before the customer existed, a list-route projection quirk, or something else entirely — it was not
reproduced a second time, is unrelated to any of F-015's 15 ACs, and this gate's time budget did not extend
to a root-cause. Noted here rather than silently dropped; recommend a fresh, narrowly-scoped look if anyone
depends on Customer list-route id fidelity.

### 3.3 🔴→✅ Two real defects found by CI on the branch's first real PR run — fixed at the ship gate

`Mobile — iOS Build`, `Mobile — Android Build`, and `Integration — real services + MongoDB` all trigger
**only** on push/PR to `main` (by design, per each job's own comment in `dotnet.yml`) — so none of the three
had run even once across F-015's 14 tasks and 5 waves, only on push to a feature branch with no PR. Opening
PR #41 at the Ship gate was the first time any of them executed against this branch's code, and two of the
three failed:

1. **`AppShell.xaml.cs`'s `Routing.RegisterRoute(...)` resolved to the wrong `Routing`.** F-015-T06
   introduced `namespace MobileApp.Routing`; `AppShell.xaml.cs` lives in namespace `MobileApp` and calls
   the Maui Shell API `Microsoft.Maui.Controls.Routing.RegisterRoute` unqualified. C#'s namespace lookup
   prefers the nested sibling namespace over a global-usings static class, so the unqualified call now
   bound to `MobileApp.Routing` instead — `CS0234` on both mobile TFMs (`net10.0-android`, `net10.0-ios`).
   Neither MobileApp.Tests (net10.0 fallback, doesn't compile the `#if MOBILE` block) nor any backend/
   integration suite could have caught this — only an actual mobile-TFM compile exercises that file.
   **Fixed** by fully qualifying all four call sites as `Microsoft.Maui.Controls.Routing.RegisterRoute`.
2. **`AgendaBuddy.IntegrationTests.csproj`'s restore failed with `NETSDK1147`.** F-015-T07 added a
   `ProjectReference` to `MobileApp.csproj` (so `MobileClientRouteResolutionTest` could call
   `MobileApp.Routing.*RouteBuilder`), but the Integration CI job's `dotnet restore` didn't pass
   `/p:MobileWorkloads=false` — so `MobileApp.csproj` restored its default `net10.0-android;net10.0-ios;
   net10.0` TargetFrameworks, and the integration runner has no MAUI workloads installed. **Fixed** by
   adding `/p:MobileWorkloads=false` to the Integration job's restore and build steps, the same flag the
   backend job already uses.

Both fixed in the same gate, not filed — re-verified: integration suite 234/234 green locally with the
flag: 234/234; the Android TFM's `CS0234` errors are gone (only a local Android-SDK-platform gap remains,
which is this machine's environment, not CI's). Full CI on PR #41's second push (`b51d5a8`): all 6 jobs
green (`changes`, `build-and-test`, `Mobile — Unit Tests`, `Integration — real services + MongoDB`,
`Mobile — Android Build`, `Mobile — iOS Build`, `summary`). **This is the same shape of finding as §3.1** —
a real defect invisible to every test that had run before, caught only by actually running the thing (here,
CI itself) for the first time.

---

## 4. What this feature does not claim

1. **AC9 and AC10 are proved by T09's unit suite, not live end-to-end.** Waiting for a real ~60-minute
   access-token expiry, or engineering a genuine gateway-hop timeout against a live backend, were both
   judged not worth the risk/time within this gate's budget — consistent with the task brief's own guidance
   to say so explicitly rather than fake a live check. `POST api/v1/auth/refresh` itself was proved live
   (§2, AC9's row) as the one piece of supporting evidence that doesn't require simulating failure.
2. **AC15 (T-303) has no live redirect behavior to observe in this topology.** No service in
   `AppHostWiring.cs` has an HTTPS endpoint, so `UseHttpsRedirection()` is a no-op everywhere, live or
   direct — T04's own finding, not new here. The test suite's mutation-testing (T-303) is the strongest
   available proof until a real HTTPS/TLS topology exists (F-017).
3. ~~Messaging and Notifications are unreachable through the Gateway~~ — **found and fixed in this same
   gate** (§3.1), not an open item. Recorded here because it's the one gap 863 automated tests missed and a
   live run caught; the fix and its regression tests are what make the total 867.
4. **T-301 (gateway as a new single point of failure) is accepted, not mitigated**, per the threat model's
   own Step-12 disposition (ADR-040) — a single Aspire-run Gateway instance, matching every other resource's
   single-instance posture locally. Re-scored only if a real (non-Aspire) deployment materializes (F-017).
5. **Multi-device refresh-token conflicts are an unaddressed, known risk**, recorded in the PRD's Known
   Risks, not built here — F-021's single-use refresh semantics mean a second device (or a race on the same
   device) gets one success and one rejected replay, with no UX treatment for communicating that yet.
6. **Client-generated idempotency keys are out of scope.** AC10's "never auto-retry an ambiguous write" is a
   conservative mitigation, not a fix — a user who hits a genuine ambiguous timeout still has to manually
   check whether their write succeeded. Filed as a follow-up in the PRD.
7. **TLS termination is not claimed anywhere.** The Gateway proxies plaintext HTTP exactly as the backend
   does today, live-confirmed (every curl in this document is `http://`, not `https://`). F-017's scope.
8. **The Gateway's own single-instance availability is not load-tested or chaos-tested** beyond the one
   stopped-service scenario AC5 asks for. No latency/throughput SLO is asserted beyond ARCHITECTURE.md §6's
   loopback measurement (Gateway hop adds no latency distinguishable from Aspire's own service-discovery
   indirection at n=20 samples) — not re-measured at this gate, since T02's spike already recorded it with
   its own methodology and nothing in F-015-T05–T13 changed the routing mechanism.
9. **`BookingApiService`'s GET-appointment methods compose with Calendar, not a Booking GET that doesn't
   exist** (F-015-T07's own recorded deviation from `api-contracts.md` §2) — this is by design given
   Booking has never had a GET route, not a residual bug, but it means "the client calls Booking's own
   `GET .../appointments`" is not literally true anywhere in the system.
10. **The generated OpenAPI specs (F-015-T13) were not re-verified at this gate** beyond confirming the task
    closed — they are a build artifact, regenerable on demand, and nothing in T14's scope changed a backend
    route that would make them stale again.

---

## 5. Security scan (CONSTITUTION §7 — always required)

Run by hand, for the **fifth** consecutive feature. **F-017 still owns automating it.**

- **Dependency audit** (`dotnet list package --vulnerable --include-transitive`, all 28 projects including
  `Gateway`) — unchanged from F-014's baseline: one vulnerable package solution-wide, `SSH.NET` 2024.2.0
  HIGH (`GHSA-q939-rpr3-3284`) in `AgendaBuddy.IntegrationTests` only, dispositioned by ADR-030. **`Gateway`
  itself has zero vulnerable packages** — `Yarp.ReverseProxy` 2.3.0 (the one new package this feature adds
  anywhere) introduced no new advisory.
- **Dependency footprint NFR** — confirmed: `MobileApp.csproj`'s dependency footprint did not grow.
  `Routing/`, `Infrastructure/ApiBaseUrlResolver.cs`/`GatewayErrorMapper.cs`, `ProviderApiService.cs`,
  `ProviderReportViewModel.cs`/`PaymentViewModel.cs` are all new *files*, not new *packages* — no new
  `<PackageReference>` was added to `MobileApp.csproj` by any F-015 task.
- **New attack surface reviewed** — the Gateway is a new network-facing process (T-301, accepted;
  T-302/T-303, mitigated and tested — AC14/AC15 above). It has no business logic and does not parse,
  validate, or terminate the caller's JWT (auth passthrough only, confirmed live at AC3/AC4) — there is
  structurally nothing in it that could weaken authorization, only a routing table that could (and, per §3.1,
  briefly did for two route families) fail to route at all.
- **PII in Gateway telemetry** — not independently re-verified live at this gate (would require inspecting
  OTLP export payloads); T01's scaffold inherits `PiiRedactingProcessor` automatically via
  `AddServiceDefaults()`, the same mechanism every other service already relies on (threat T-NL-3,
  deprioritized at Design on exactly this basis).

---

## 6. What a reviewer should look at first

1. **`Gateway/AspireServiceDiscoveryProxyConfigProvider.cs`'s `_routeSpecs` allowlist.** This is where §3.1's
   gap lived (now fixed, with regression tests), and it remains the single point where a future
   F-014-shaped feature (a backend service growing a new top-level route group not nested under its own
   service's plural collection name) can silently become unreachable from the mobile client again — with
   every test in the routing/route-resolution suites still green, exactly as happened here. Any PR that adds
   a backend route should be required to show this file's diff, not just the backend `Program.cs`'s.
2. **The Gateway's failure-translation transform** (`Gateway/Program.cs`'s `AddResponseTransform` +
   `TranslateDestinationFailureAsync`, F-015-T04). It is a global YARP response transform, not middleware
   wrapped around `MapReverseProxy` — the ordering (default copy → transform → body-copy decision) is what
   makes it safe to overwrite even a destination's own genuine 5xx, not just the null-`ProxyResponse` case.
   Getting this ordering wrong would either leak a destination's real error body past the shaped
   `gateway-destination-unreachable` envelope, or (worse) swallow a legitimate response.
3. **`AppointmentDetailViewModel.ShowCompleteButton`/`CompleteCommand.CanExecute`** (F-015-T07, UX finding 3)
   — the customer-facing "mark complete" control must be **absent from the visual tree**, not merely
   disabled. A regression here (e.g. someone "simplifying" the binding back to an `IsEnabled` check) would
   reopen exactly the "why can't I do this?" UX anti-pattern the review flagged, and would do so silently,
   since a disabled button still passes most visual QA.
4. **`MobileApp/Infrastructure/ApiBaseUrlResolver.cs`'s priority chain.** `MAUI_API_BASE_URL` env var beats
   config beats the hardcoded fallback — get this order wrong and the app silently falls back to the old
   `http://localhost:6036/` default (Identity's port, not the Gateway's), which is exactly the defect F-015
   exists to fix, just reintroduced one layer up. `scripts/run-ios.sh`'s `SIMCTL_CHILD_MAUI_API_BASE_URL`
   export depends on this priority holding.
