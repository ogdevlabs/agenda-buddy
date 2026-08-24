# Episode 005: API Gateway and Mobile Contract

**Episode ID:** 005
**Feature name:** API Gateway and Mobile Contract — the mobile client gets a single address to call, and every route it calls is corrected against the real backend
**Feature slug:** api-gateway-and-mobile-contract
**Feature ID:** F-015
**Date built:** 2026-08-23 → 2026-08-24, on `feat/F-015-api-gateway-and-mobile-contract` — PR [#41](https://github.com/ogdevlabs/agenda-buddy/pull/41), CI green (6/6 jobs after two ship-gate fixes), mergeable
**Phase delivered in:** Construction (14 tasks, 5 waves) + two defects found and fixed at the Ship gate itself
**Date shipped:** 2026-08-24 — merged as `1d61955` (GitHub API, `merge_method=merge`), tagged **`v0.5.0`**, PR #41
**Status:** **Final** — Operation phase closed 2026-08-24 after live verification against a running 8-process AppHost

---

## What Was Built

`MobileApp` (F-012) was the only client of Agenda Buddy's seven backend services and could not reach any of
them — no fixed port survived F-013's dynamic-port assignment, every configured base URL was dead code, and
every domain route path/verb/payload the client sent was wrong. F-015 gives it one address to call.

**`Gateway`, an eighth AppHost process.** A thin YARP reverse proxy with an explicit
`api/v1/{service}/**` route allowlist — never a catch-all (T-302) — built from the same Aspire
service-discovery config every service already reads, polled every 2 seconds so a backend's dynamic-port
reassignment never needs the Gateway to restart (proven live: two `aspire resource booking restart`
cycles kept routing correctly). No business logic, no auth validation — JWT forwarded byte-for-byte, so the
destination validates it exactly as it would a direct call (T-303 mitigated, proven by mutation-testing).
A destination failure returns a shaped `ProblemDetails` naming the failed cluster (`failedService`), mapped
in the client to a human-readable banner ("Booking is unavailable right now. Try again.").

**Every `MobileApp` route, verb, and payload corrected** against the real backend contract — including
swapping the status update onto F-014's server-owned transition route, and hiding (not just disabling) the
customer-facing "mark complete" control. Route-building logic was extracted into seven plain, Maui-free,
DI-free classes under `MobileApp/Routing/` so it is unit-testable under `MobileApp.Tests`'s `net10.0`
fallback TFM. `SeedDataProvider` — the fabricated-data fallback that had masked this entire class of defect
since F-012 shipped — is **deleted entirely**; a real error or a real empty result now reaches the UI.
`LogoutAsync` calls the server-side logout endpoint (proven live: the old refresh token is rejected
afterward), and a 401 mid-session transparently refreshes and retries once, with non-idempotent writes
guarded against silent auto-retry on an ambiguous timeout.

**One real defect found and fixed inside Construction, not filed:** the closing verification task (T14)
found the Gateway's allowlist had no entry for `api/v1/messages/**` or `api/v1/notifications/**` — both real
top-level route groups F-014 added to the Customer service, invisible to every task's own tests because none
of them constructed a real Gateway in front of a real Customer service and asked for those two paths. Fixed
with a two-line allowlist addition and four regression tests in the same gate that found it.

**Two more real defects found at the Ship gate itself**, invisible to all 867 tests that existed before PR
#41: `Mobile — iOS/Android Build` and `Integration — real services + MongoDB` trigger only on push/PR to
`main`, so neither had executed even once across the whole Construction phase. Opening the PR was the first
real run, and it caught (1) a namespace collision — `AppShell.xaml.cs`'s unqualified
`Routing.RegisterRoute(...)` resolved to the sibling `MobileApp.Routing` namespace this feature introduced
instead of `Microsoft.Maui.Controls.Routing`, breaking both mobile TFMs — and (2) a missing
`/p:MobileWorkloads=false` on the Integration job's restore, needed because this feature's own
`MobileClientRouteResolutionTest` added a `ProjectReference` from `AgendaBuddy.IntegrationTests` to
`MobileApp.csproj`. Both fixed in the same gate; the second CI run went fully green (6/6 jobs) before merge.

Tests went from 863 (Construction close) to **867** (T14's own fix), unchanged by the two Ship-gate fixes
(both were build/CI-environment defects, not missing test coverage) — **468 backend + 234 integration + 165
mobile**, 0 failing.

---

## Links

| Artifact | Path |
|---|---|
| PRD | [`PRD_F-015_api-gateway-and-mobile-contract_2026-08-23.md`](../archive/prds/PRD_F-015_api-gateway-and-mobile-contract_2026-08-23.md) |
| Brainstorm | [`brainstorm_api-gateway-and-mobile-contract_2026-08-23.md`](../archive/brainstorm/brainstorm_api-gateway-and-mobile-contract_2026-08-23.md) |
| Design | [`docs/pdlc/design/api-gateway-and-mobile-contract/`](../archive/design/api-gateway-and-mobile-contract/) — ARCHITECTURE, data-model, api-contracts, threat-model, ux-review |
| Verification | [`verification.md`](../archive/design/api-gateway-and-mobile-contract/verification.md) — 15/15 ACs, three defects found by running the software (§3.1, §3.3), ten things this feature does *not* claim |
| Tasks | [`docs/pdlc/tasks/F-015/`](../tasks/F-015/) — T01…T14 |
| Decisions | ADR-040 (T-301), ADR-041, ADR-042 (Nordstrom standards gate retired project-wide) |

---

## Key Decisions & Rationale

**ADR-040 — the Gateway's single-instance posture is accepted, not mitigated (T-301).** A single Aspire-run
Gateway instance matches every other resource's single-instance posture locally; there is no load balancer
or redundancy to add without a real (non-Aspire) deployment target, which does not exist yet. Re-scored only
if a real deployment materializes — that is F-017's scope, not this feature's.

**ADR-041 → superseded by ADR-042 — the Nordstrom standards gate is retired outright for this project.**
Ten consecutive gate call sites had failed to reach the plugin's six source repos under this machine's `gh`
auth, across F-013 through F-015. Rather than log an eleventh skip, the maintainer decided the gate never
applied in the first place: Agenda Buddy is a personal `fererelabs` project, not a Nordstrom enterprise
engagement. `CONSTITUTION.md` §9 now records the exemption; no future gate call site should prompt for it.

**The routing allowlist is the single point of truth for client reachability, and it is not derived from
anything else.** §3.1's finding (messages/notifications unreachable) and the review-first-look note in
`verification.md` §6 both point at the same file: `Gateway/AspireServiceDiscoveryProxyConfigProvider.cs`'s
`_routeSpecs`. Any future feature that adds a backend route group is invisible to `MobileApp` through the
Gateway until a line is added here — and nothing fails loudly when it's missing.

---

## What the implementation found that the plan didn't

Three defects, all found by **running** the software (or, in the third case, running CI for the first time)
— continuing the pattern recorded after every episode so far.

1. **The Gateway's route allowlist had no entry for `api/v1/messages/**` or `api/v1/notifications/**`**
   (found by F-015-T14, fixed in the same gate). Both are real Customer-service route groups F-014 added;
   T03's allowlist was built against a pre-F-014 context catalog and no task re-checked it against F-014's
   actual route table. No test in any of the three suites had ever constructed a real Gateway in front of a
   real Customer service and asked for either path — closed with a two-line addition plus four regression
   tests, one of them a correction to a pre-existing test whose assumption (one route per cluster) broke once
   "customer" stopped being a single-route cluster.
2. **A `Routing.RegisterRoute` namespace collision broke both mobile build TFMs** (found at the Ship gate,
   PR #41's first CI run). F-015-T06 introduced `namespace MobileApp.Routing`; `AppShell.xaml.cs` lives in
   namespace `MobileApp` and calls the Maui Shell API unqualified, so C#'s namespace lookup bound it to the
   sibling namespace instead. Neither `MobileApp.Tests` (net10.0 fallback, doesn't compile the `#if MOBILE`
   block) nor any backend/integration suite could have caught this — only an actual mobile-TFM compile does,
   and `Mobile — iOS/Android Build` had never run on this branch before the PR existed.
3. **The Integration CI job's restore failed with `NETSDK1147`** (found alongside #2, same CI run).
   F-015-T07's `ProjectReference` from `AgendaBuddy.IntegrationTests` to `MobileApp.csproj` restored
   MobileApp's default `net10.0-android;net10.0-ios;net10.0` TargetFrameworks with no MAUI workloads
   installed on that runner. Fixed with the same `/p:MobileWorkloads=false` flag the backend job already
   uses — a flag that existed in the codebase for exactly this reason, just not yet applied to this job.

**The common thread across #2 and #3:** `Mobile — iOS Build`, `Mobile — Android Build`, and
`Integration — real services + MongoDB` all trigger only on push/PR to `main` — by design, to keep them off
every ordinary feature-branch push — which means a 14-task, 5-wave Construction phase produced code that had
never once been exercised by two of its three most expensive CI jobs until the PR that shipped it.

---

## Test Summary

| Layer | Required (§7) | Command | Result |
|---|---|---|---|
| 1 — Unit | **yes** | `dotnet test agenda-buddy-backend.slnf` | ✅ **468** passing / 0 failing / **0 warnings**, 13 projects incl. `Gateway` (baseline 452) |
| 2 — Integration | no in §7, **yes by this PRD** | `dotnet test AgendaBuddy.IntegrationTests/…csproj` | ✅ **234** passing, **2 m 11–37 s** of a 600 s budget (baseline 175) |
| 3–6 — E2E / perf / a11y / visual | no | — | ⊘ no command in project; logged skips |
| 7a — Dependency audit | **yes** | `dotnet list package --vulnerable --include-transitive` | ⚠️ **1 HIGH**, unchanged: `SSH.NET` in `AgendaBuddy.IntegrationTests` only (ADR-030). `Gateway`'s one new package (`Yarp.ReverseProxy` 2.3.0) introduces no new advisory |
| 7b — Secret scan | **yes** | 6 patterns over changed files | ✅ clean |
| Mobile | — | `…/MobileApp.Tests.csproj /p:MobileWorkloads=false` | ✅ **165** (158 passing, 7 skipped) (baseline 74) |

`dotnet format agenda-buddy-backend.slnf --verify-no-changes` clean on `main` post-merge.

**15/15 acceptance criteria attested** in `verification.md` §2 (13 PRD + 2 threat-derived `[security]`), each
against a named test and live evidence. AC2 was **downgraded** from the task-closing claim of "verified
against a live AppHost" to "verified against the real backend, but not through the real Gateway" — the
distinction §3.1's finding then made concrete. AC9/AC10/AC15 rely on their tasks' unit suites rather than a
live re-derivation, recorded explicitly in `verification.md` §4 rather than glossed as fully proven.

---

## Known Tradeoffs & Tech Debt

| Item | Disposition |
|---|---|
| **AC9/AC10 not proved live end-to-end** | Waiting for a real ~60-minute access-token expiry, or engineering a genuine gateway-hop timeout against a live backend, were both judged not worth the time/risk within the gate's budget. Resting on T09's unit suite (mocked `HttpMessageHandler`) |
| **AC15 (T-303) has no live redirect behavior to observe** | No service in this topology has an HTTPS endpoint, so `UseHttpsRedirection()` is a no-op everywhere — T04's own finding, not new here. Real proof needs a real HTTPS/TLS topology (F-017) |
| **T-301 accepted, not mitigated** | Single-instance Gateway, matching every other resource's local posture. Re-score only if a real deployment materializes |
| **Multi-device refresh-token conflicts unaddressed** | F-021's single-use refresh semantics mean a second device (or a race on the same device) gets one success and one rejected replay, with no UX treatment. Recorded in the PRD's Known Risks |
| **Client-generated idempotency keys out of scope** | AC10's "never auto-retry an ambiguous write" is a conservative mitigation, not a fix — a genuine ambiguous timeout still needs a manual check. Filed as a follow-up |
| **TLS termination not claimed anywhere** | The Gateway proxies plaintext HTTP exactly as the backend does today — F-017's scope |
| **A minor, unreproduced observation** | A `GET api/v1/customers` response briefly showed a zeroed `ObjectId` instead of the real one during the AC5 stopped-service test — not reproduced a second time, not root-caused, noted rather than silently dropped |
| **The Gateway's own availability is not load- or chaos-tested** beyond the one stopped-service scenario AC5 asks for | No latency/throughput SLO beyond ARCHITECTURE.md §6's loopback measurement (no distinguishable overhead at n=20) |
| **`BookingApiService`'s GET-appointment methods compose with Calendar**, not a Booking GET that doesn't exist | By design (Booking has never had a GET route), not a residual bug — but means "the client calls Booking's own GET" is not literally true anywhere |
| **The generated OpenAPI specs were not re-verified at this gate** beyond confirming the regenerating task closed | Build artifact, regenerable on demand; nothing in the Ship-gate fixes changed a backend route |
| **`Mobile — iOS/Android Build` and `Integration — real services + MongoDB` cannot run on an ordinary feature-branch push** | By design (keeps expensive jobs off every push), but it means a whole Construction phase can complete with two of three CI jobs never having run once. **Worth a process change** — see Reflect Notes |
| **§7's security scan satisfied by hand for the fifth consecutive feature** | **F-017** still owns automating it |
| **The standards-readiness gate is now retired for this project (ADR-042)**, not merely skipped | Ten consecutive skips resolved into an explicit exemption rather than an eleventh skip |

---

## Agent Team

| Agent | Role in this episode |
|---|---|
| **Real subagents, one per task, worktree-isolated** | At the maintainer's explicit request — a deviation from every prior feature's solo execution. Waves 1–5 ran 3, 2, 4, 3, and 1 tasks respectively, parallelized within a wave via git worktrees, merged back after each wave. Two merge conflicts across the whole feature (`AppHostWiring.cs`/`.csproj` in Wave 2; none in Wave 3 despite three tasks touching overlapping `MobileApp/` areas) — both resolved without losing either agent's deliverable |
| **Solo (Ship gate)** | Merge, tag, live smoke test, and the two CI-only defect fixes were diagnosed and fixed by the ship-gate session directly, not by a subagent — the fixes were small (namespace qualification, one build flag) and diagnosis needed live CI log inspection |

---

## Verified at the Ship gate — not inferred from a green suite

Full smoke-test record in `DEPLOYMENTS.md`'s `local` environment and STATE.md's Verify checkpoint; the
headlines:

- **All 8 processes (7 services + the new Gateway) `Healthy` under a live AppHost**, `/alive` = 200 on all
  eight — the first time this feature's Gateway has been smoke-tested against the exact commit that shipped
  to `main`, not a pre-merge branch state.
- **Register → login → real data, through the Gateway, on merged `main`**: a fresh Customer account
  registered and logged in entirely through `http://127.0.0.1:5000`, not any backend service's own port.
- **The T14 fix re-verified live, post-merge**: `GET api/v1/notifications` and `GET api/v1/messages` through
  the Gateway both returned `200 []` — the exact routes that returned `gateway-no-route` 404 before T14's fix,
  now proven correct on the actual shipped commit, not just the commit that fixed it.
- **T-302 re-confirmed intact**: an unmapped path (`/booking/health`, no `api/v1` prefix) still answers
  `gateway-no-route` 404 through the Gateway on `main`.
- **Anonymous access re-confirmed**: the same authenticated-only route returns 401 with no bearer token.
- **The documented AppHost shutdown gotcha recurred exactly as recorded**: `SIGTERM` on the AppHost left all
  8 processes orphaned (one more than F-014's 7, now that the Gateway is part of the graph), needing cleanup
  by explicit PID. Not a defect — `DEPLOYMENTS.md` already carries this note from F-013.

What the live run did **not** re-derive: full AC1…AC15 correctness, which rests on the 166 new automated
tests (16 unit, 59 integration, 91 mobile) run against real MongoDB over real HTTP and a real Gateway —
proportionate scope for a manual smoke pass alongside a suite that size, matching what every prior episode's
live verification also chose to spend effort on.

---

## Reflect Notes

### Per-agent contributions

- **Real subagents (T01–T13)**: scaffolded the Gateway, spiked Aspire's dynamic-port re-resolution, wired
  the AppHost, built the seven-service allowlist, corrected every `MobileApp` route/verb/payload, extracted
  testable routing classes, deleted `SeedDataProvider`, wired refresh/logout, and finalized UX copy and the
  report/payment screens.
- **Solo (T14 + Ship gate)**: closing verification against a live AppHost (found and fixed the
  messages/notifications allowlist gap); merge, tag, deploy-skip decision, and diagnosis/fix of both
  CI-only defects found on PR #41's first real run.

### What went well

- **The subagent-per-task model held up across the largest task count and biggest wave (Wave 3, 4 parallel
  tasks) this project has run.** Only two merge conflicts total, both resolved without dropping either
  agent's actual deliverable — the worktree-isolation approach is working as designed at this scale.
- **The closing-verification task (T14) did exactly what it exists to do**: found a real, client-facing gap
  (messages/notifications unreachable) that 863 automated tests had missed, and it was cheap enough to fix
  in the same gate rather than file.
- **The Ship-gate CI failures were diagnosed and fixed fast** (two defects, both root-caused and fixed within
  the same session, verified locally before re-pushing) — the second CI run went fully green on the first
  retry.
- **Blast-radius discipline held again**: AC2's downgrade was recorded honestly rather than left as an
  overclaim, and the allowlist fix's regression tests target the exact layer that missed it the first time
  (the Gateway's own routing table), not the client or the backend.

### What broke or slowed us down

- **Two of three CI jobs had never run, at all, across the entire 14-task Construction phase.**
  `Mobile — iOS/Android Build` and `Integration — real services + MongoDB` only trigger on push/PR to
  `main`, and this feature's branch had no PR open until the Ship gate. A namespace collision and a missing
  build flag — both real, both would have failed any CI run — sat undetected through 5 waves and 863 green
  tests, found only because opening the PR was itself the first real exercise of those jobs.
- **The same "context catalog drifted, nothing re-checked it" pattern that F-016 named `stale-context-propagated`
  recurred here in a new shape**: T03's allowlist was correct against the context catalog it was built from,
  but that catalog was already stale relative to F-014's shipped routes, and no task's own scope included
  re-checking against the *code*, not the doc.
- **No formal Review sub-phase ran** — same gap F-014 recorded, now a second consecutive occurrence. The
  human PR review (#41, now including the two CI-fix commits) stands in for it, same as F-014.

### What to improve next time

- **Open the PR earlier — at Construction start, as a draft, not at Ship.** This is the direct fix for the
  two Ship-gate CI defects: if PR #41 had existed since Wave 1, `Mobile — iOS/Android Build` and
  `Integration` would have caught the namespace collision and the workload-restore gap the moment the
  responsible task's commit landed, not five waves and one Ship gate later.
- **Add a task-level check that re-verifies a route allowlist (or any config table keyed off another
  service's routes) against that service's actual `Program.cs`, not against the context catalog**, whenever
  the plan depends on another feature's shipped shape. F-016's `stale-context-propagated` proposal and this
  feature's §3.1 finding are the same root cause, twice.
- **Run the Review sub-phase as its own step, as F-014's retro already recommended and this feature did not
  do either.** Two consecutive skips is a pattern, not a one-off.

### Metrics snapshot

- **Cycle time:** 2 days — Discover claimed 2026-08-23T14:00Z, shipped 2026-08-24. Longer than F-014/F-021's
  same-day cycles, proportionate to the largest task count (14) and test delta (+166) of any feature shipped
  so far.
- **Test pass rate:** 867/867 = **100%** (468 + 234 + 165).
- **Tasks completed:** 14/14 (T01–T14).
- **Review findings:** 0 — no formal Review sub-phase ran this cycle (second consecutive, after F-014); the
  human PR review that merged #41 stands in its place.

---

## Deployment Record

| Item | Detail |
|---|---|
| **Merged** | `1d61955` (merge commit, GitHub API `merge_method=merge`), PR #41, CI green 6/6 on the second run (`changes`, `build-and-test`, `Mobile — Unit Tests`, `Integration — real services + MongoDB`, `Mobile — Android Build`, `Mobile — iOS Build`, `summary`) |
| **Tagged** | `v0.5.0` (minor bump — `feat` commits present, no `BREAKING CHANGE` marker) |
| **Deployed to** | `local` (Aspire AppHost) only — where the verification above was performed, against the merged `main` commit |
| **CI/CD method** | None triggered — no deploy workflow ran; cloud deploy skipped per ADR-035 (see below) |
| **Custom deploy artifact** | No — user declined at the Step 9.1 prompt, default pipeline |
| **Deployment Review Party** | Not convened — no custom artifact offered |
| **Overrides used** | None |
| **Config changes introduced** | `.github/workflows/dotnet.yml`'s Integration job gained `/p:MobileWorkloads=false` on its restore/build steps (Ship-gate fix, not a feature config change) |
| **New tags recorded** | None — both environments were already tagged (`local`: `dev`; `cloud`: `dev`, provisional) |
| **Rollback tested** | No — nothing deployed to roll back |
| **DEPLOYMENTS.md updated** | Yes — local Deployment History row finalized with live smoke-test results against merged `main`; cloud section's skip note extended (fifth consecutive skip, third under the ADR-035 deferral) |
| **Cloud** | ⚠️ **Deferred by decision, not blocked** — ADR-035: Azure is not reviewed until every pending feature is complete and the no-longer-needed tech debt is discharged. Fifth consecutive release without a remote deployment |
| **Still outstanding, and independent of the deferral** | Rotating the Atlas credential (`agenda-buddy-41s`, P0) |

---

## Approval

**Status:** Approved
**Approved by:** oscargarcia@ogdevlabs.onmicrosoft.com
**Approved date:** 2026-08-24
**Version shipped:** `v0.5.0` (tag at merge `1d61955`)
**Links:** PR [#41](https://github.com/ogdevlabs/agenda-buddy/pull/41)
