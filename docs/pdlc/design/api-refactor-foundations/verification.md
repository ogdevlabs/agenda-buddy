# Verification — API Refactor Foundations (F-018)

**Feature:** `api-refactor-foundations` (F-018) · **Task:** `F-018-T20` · **Date:** 2026-08-26
**Branch:** `feat/F-018-api-refactor-foundations` · **Pushed, PR #69 open (draft), not merged.**

AC-24's attestation: what is verified, what is verified *differently from how the criterion was worded*, and
what is not verified at all. Written so a reviewer can disagree with a specific line rather than with a
summary — same discipline as F-016's `verification.md`.

**Context a reader needs first.** F-018's Construction was aborted at the wave-1 standup on 2026-08-18,
before any code was written, to deliver a platform-remediation program (F-016 → F-021 → F-014 → F-015 →
F-017) first. F-016 absorbed 8 of F-018's original 20 tasks at its own Plan gate specifically because it
needed the integration-test harness to verify its own endpoint-authorization claims. F-018 resumed on
2026-08-26, after all five of those features had shipped, with its task store amended to mark the 8 absorbed
tasks done and reference what F-016 actually built. This document verifies **F-018's full 31-AC set**,
crediting F-016 where it did the work and F-018's Waves 1a/2a (this session) where they did.

---

## 1. Test gate

| Suite | Command | Result |
|---|---|---|
| Backend unit | `dotnet test agenda-buddy-backend.slnf --no-build` | **484 passing / 0 failing / 0 warnings**, 12 projects |
| Integration | `dotnet test AgendaBuddy.IntegrationTests/AgendaBuddy.IntegrationTests.csproj /p:MobileWorkloads=false` | **301 passing / 0 failing**, ~3m46s locally, confirmed again on PR #69 |
| Mobile | `dotnet test MobileApp.Tests/MobileApp.Tests.csproj /p:MobileWorkloads=false` | **165 (158 passing, 7 skipped)** — untouched by F-018; skip reason investigated by T19, confirmed deliberate (`AuthAcceptanceTests` needs a live Identity service) |

**New headline: 950 tests** (484 + 301 + 165). AC-24 was written against a "379" baseline captured at PRD-time
(2026-08-18, before F-016/F-021/F-014/F-015/F-017 shipped) — that number is stale by construction, not an
error. What AC-24 actually requires — **no regression, no deleted test file** — is re-verified fresh:

```
git diff --diff-filter=D --name-only origin/main...HEAD -- '*.cs'   →  (empty)
```

Zero `.cs` files deleted anywhere in the branch's full diff against `main`. 199 files changed, 2229
insertions, 175 deletions — the deletions are line-level (the F-018-T03 whitespace reformat, the
`KafkaClient` downcast fix, etc.), not file removals.

---

## 2. Acceptance criteria — 31

### Harness (AC-1 … AC-9)

| AC | Verified by | Status |
|---|---|---|
| 1 — `AgendaBuddy.IntegrationTests` exists, ≥1 real-HTTP passing test | F-016-T02 (built the project); now 301 tests | ✅ *(F-016)* |
| 2 — 7× `InternalsVisibleTo`, `WebApplicationFactory`-hosted boot for all 7 | F-016-T02, `Harness/EntryPoints.cs` | ✅ *(F-016)* — same AC-2 note as F-016's own verification: `InternalsVisibleTo` is not what enables hosting; a public per-service anchor type is |
| 3 — session RSA keypair in memory, no PEM in tracked files | F-016-T03 `CryptoSessionFixture`, `Library.Tests/Security/KeyMaterialHygieneTest.cs` | ✅ *(F-016)* |
| 4 — Mongo connection string reaches each host as `ConnectionStrings:mongodb`, zero production changes | F-016-T06 `ServiceHostFixture` | ✅ *(F-016)* |
| 5 — **Tier 1** route contract, all 7 services | `AgendaBuddy.IntegrationTests/Contract/*.cs` (7 files, F-018-T11) | ✅ *(F-018, this session)* |
| 6 — **Tier 2** persistence round-trip, 6 write services + Calendar seed-then-read | `AgendaBuddy.IntegrationTests/Persistence/*.cs` (7 files, F-018-T12) | ✅ *(F-018, this session)* |
| 7 — **Tier 3** audit fired, 6 services (Identity excluded, no `AddEventStore`) | `AgendaBuddy.IntegrationTests/Audit/*.cs` (6 files, F-018-T13) | ✅ *(F-018, this session)* |
| 8 — Identity tier 1+2 across all 5 write routes | `Contract/IdentityRouteContractTest.cs` + `Persistence/IdentityPersistenceTest.cs` (register→login→refresh→logout chain + device-token) | ✅ *(F-018, this session)* |
| 9 — token factory: valid / expired→401 / foreign-subject→403 | F-016-T05 `TokenFactory`, exercised again by F-018-T11/T13/T14's tests | ✅ *(F-016)* |

### Infrastructure diagnostics (AC-10 … AC-14)

| AC | Verified by | Status |
|---|---|---|
| 10 — Docker daemon stopped → named error (Rancher Desktop, `~/.rd/bin`) | F-016-T04 `DockerPreflight`, verified live with a bogus `DOCKER_HOST` | ✅ *(F-016)* |
| 11 — simulated image-pull failure reported as infrastructure error | — | 🚫 **not built** — see §3 |
| 12 — every image pinned by explicit tag, no `:latest` | F-016 harness (`mongo:7.0.14`); ADR-018 accepts tag-not-digest as a known residual risk | ✅ *(F-016)* |
| 13 — mid-flight kill leaves zero orphan containers, verified by `docker ps` | `scripts/verify-container-reaping.sh` (F-018-T15) — **two real SIGKILL runs**, Mongo + Ryuk both reaped in ~10s each time, confirmed via `docker inspect` on the exact captured container IDs (not a global `docker ps` diff, since this box runs other concurrent Testcontainers sessions) | ✅ *(F-018, this session)* |
| 13b — unique database name per test, isolation within a shared container | F-016 harness design (ADR-017) | ✅ *(F-016)* |
| 14 — harness warns when an AppHost is already running | — | 🚫 **not built** — see §3 |

### Permanent audit guard, rename, OpenAPI (AC-15 … AC-19)

| AC | Verified by | Status |
|---|---|---|
| 15 — permanent guard fails when EventStore write is removed; one-time mutation red/green recorded | `Audit/EventStoreWriteGuardTest.cs` (F-018-T13) — mutated `BookingAppointmentCommandHandler.cs` to remove both `SaveAsync` calls, guard went red (`Assert.Contains` failure naming the file), restored, guard green again (22/22) | ✅ **with a narrowed claim, corrected at Party Review** — the guard proves the call site isn't deleted from the *file*, not that every *branch* calls it; see §3 |
| 16 — `EventAndCommands/Persistence/` exists, zero `Persitency` matches, own commit before first integration test | F-016-T01, `EventsAndCommands.Tests/Persistence/PersistenceNamespaceTest.cs` | ✅ *(F-016)* |
| 17 — OpenAPI generated for all 7 services, CI artifact, **not committed** *(as originally worded)* | `AgendaBuddy.IntegrationTests/OpenApi/OpenApiSpecGenerator.cs` + `OpenApiSpecCatalog.cs` (F-018-T16) | ✅ **with a deliberate, recorded deviation — see §3** |
| 18 — exits non-zero, no partial/empty spec on boot failure | `OpenApiSpecGeneratorTest.cs`'s AC-18 case — malformed `JWT_PUBLIC_KEY` causes a real DI-registration throw, asserts the designated output path is never created | ✅ *(F-018, this session)* |
| 19 — CI fails when a route changes without regeneration | `OpenApiSpecDriftTest.cs` (F-018-T17) — proven locally (renamed an operation ID, watched it fail naming the exact drifted line, reverted, watched it pass again) **and confirmed live on PR #69** (`Integration` job green, 301/301 including this test) | ✅ **with the baseline changed — see §3** |

### CI enforcement, mobile, governance (AC-20 … AC-27)

| AC | Verified by | Status |
|---|---|---|
| 20 — separate CI job runs integration tests on every PR; a deliberately-failing test blocks the PR on its first run | F-016-T20 built the job. **PR #69 (this session) is the first real PR that job has ever run on** — confirmed green, separate from `build-and-test`, blocking-capable by construction (no `continue-on-error`) | ✅ **the "runs on every PR" half freshly confirmed live; the "deliberately-failing-test blocks" half remains a constructed inference from the job's shape, not a literal demonstration — see §3** |
| 21 — integration job prints duration, fails/warns above 10 minutes | `.github/workflows/dotnet.yml`'s `Test (duration-enforced)` step (`DURATION_BUDGET_SECONDS=600`), built at F-016/F-017; PR #69's real run completed in 5m23s, well under budget, tripwire not exercised (nothing to warn about) | ✅ *(mechanism pre-existing, exercised live without tripping)* |
| 22 (amended → T19 + T21) — 3 mobile CI jobs green + headline count reported | **All 3 confirmed live on PR #69**: `Mobile — Android Build` (3m36s), `Mobile — iOS Build` (15m25s), `Mobile — Unit Tests` (28s), all pass. Headline count reported as **950**, not 379 — see §1 | ✅ **with the headline-count deviation — see §3** |
| 23 — CONSTITUTION §1/§4/§9 amended, ADR-014…020 exist | F-018-T02 (this session): §1 net10.0, §4 MiniValidator-now/Validot-target per ADR-016, §9 records ADR-015's five packages. ADR-014…020 confirmed present in `DECISIONS.md` (verified by `grep`, not assumed) | ✅ *(F-018, this session)* |
| 24 — all pre-existing tests pass, no test file deleted | §1 | ✅ |
| 25 — `Identity/Program.cs` comment corrected (no EventStore) | F-018-T02, this session | ✅ *(F-018, this session)* |
| 26 — `.editorconfig` exists, `dotnet format --verify-no-changes` passes in CI | F-018-T03 (this session) — 168-file whitespace-only reformat, CI step added to `build-and-test`, **confirmed live on PR #69** | ✅ *(F-018, this session)* |
| 27 — beads issue tracks the 10-green-run count, assigned to maintainer | F-018-T04, this session — `agenda-buddy-ym9` | ✅ *(F-018, this session)* |

### Threat-derived `[security]` criteria (AC-28 … AC-31)

All four are materialized as structured ACs with **linked tests** (three inherited via F-016's absorption,
one native to F-018). `tasks.cjs check` reports zero `security-ac-untested` findings for F-018's own tasks
(T06, T08, T10 — the three that carry security-tagged ACs).

| AC | Threat | Linked test | Status |
|---|---|---|---|
| 28 — non-container endpoint refused, names the host | T-001 | `Harness/MongoEndpointGuardTest.T002_RejectsAnEndpointThatIsNotTheFixturesOwnContainer` | ✅ *(F-016, linked to F-018-T08 on resume)* |
| 29 — `ConnectionStrings__mongodb` non-container value aborts before any test | T-004 | `Harness/MongoFailClosedTest.T002_AbortsDuringFixtureConstruction_AndCreatesNoDatabase` | ✅ *(F-016, linked to F-018-T08 on resume)* |
| 30 — no PEM/private-key material tracked, no production `.csproj` references the harness | T-002 | `Library.Tests/Security/KeyMaterialHygieneTest.NoTrackedFile_ContainsPemKeyMaterial` | ✅ *(F-016, linked to F-018-T06 on resume)* |
| 31 — `KafkaClientFake` records the topic-creation call on the provider-registration path | — (CONSTITUTION §3 convention guard, not a modeled threat) | `Support/KafkaClientFakeProviderRegistrationTest.cs` (F-018-T10) | ✅ *(F-018, this session)* — **found and fixed a real production defect to make this true**, see §3 |

---

## 3. Deviations, gaps, and defects found live

**AC-11 and AC-14 were never built — genuine gap, not a false claim.** Both were originally scoped under
`F-018-T07` ("DockerPreflight: actionable diagnostics for infrastructure failure"), which the task store's
resume-time absorption note credits entirely to `F-016-T04`. Reading `F-016-T04`'s actual task body (this
session, at T20) shows it scoped itself to exactly one AC — the Docker-daemon-unreachable case (AC-10) — and
never mentions image-pull failures or an already-running-AppHost warning. Grepping the whole
`AgendaBuddy.IntegrationTests` tree for either behavior finds nothing. The absorption note overclaimed;
**corrected in `F-018-T07.md` and filed as `agenda-buddy-10g`** rather than silently marked done. Building either now would be new scope, not verification
— out of bounds for T20.

**AC-17's "not committed" clause was superseded mid-session by ADR-048.** F-016 shipping on 2026-08-18 closed
the anonymous `GET /api/v1/providers` exposure ADR-020 named as the commit-deferral's exit criterion. On
resume, the user confirmed (see STATE.md, 2026-08-26T14:xx) that the specs should now be committed rather
than continuing to honor a since-satisfied deferral. `docs/api/openapi/*.json` are committed, superseding
F-015-T13's HTTP-scraped content (confirmed semantically identical by structural diff, differing only in
indentation). AC-17 is satisfied in spirit — a stronger outcome than originally scoped, not a weaker one —
but is not what its literal 2026-08-18 wording says. ADR-048 records the reasoning.

**AC-19's drift baseline changed for the same reason.** Originally "the previous run's artifact... rather
than the spec body," because nothing was committed. Now the committed spec body **is** the baseline — simpler
than what was planned, and a stronger check (drift is caught on the very first run, not the second).

**AC-20's "blocks the PR" half is a constructed inference, not a literal demonstration.** PR #69 had no
failing tests, so the negative case — a red integration test actually blocking a PR — was not directly
observed this session, matching F-016's own verification.md's identical disclosure for the same job when it
was first built. The job's shape (a required check, no `continue-on-error`, `timeout-minutes: 20`) makes
blocking-on-failure a safe inference, not a proven one. Recorded as a residual open item, not a false claim.

**AC-22's headline count (379) is stale by construction.** The PRD was written 2026-08-18, before five
features shipped. Reporting the current, re-verified 950 rather than the literal "379" is the correct reading
of AC-22's *intent* ("the headline test count is reported [accurately]"), not a deviation from it.

**A real production defect was found and fixed to make AC-31 true.** `Provider/Requests/RequestCollection.cs`
passed `IKafkaClient` through a `(kafkaClient as KafkaClient)!` downcast into `AddProviderCommandHandler`,
whose constructor demanded the concrete class. Substituting `KafkaClientFake` (any `IKafkaClient` other than
the real one) made the cast evaluate to `null` at runtime — the `!` suppresses only the compiler, not the
runtime — and the handler NRE'd before ever reaching `CreateTopicIfNotExist`. This was already documented as
a known risk in `docs/pdlc/context/15-cqrs-and-messaging.md` but nothing had connected it to F-018-T10's plan
before building it. Fixed: the constructor now takes `IKafkaClient`, the cast is gone. The identical, still-
dormant pattern in `Booking/Requests/RequestCollection.cs` and `Customer/Requests/RequestCollection.cs` was
**not** fixed (nothing currently substitutes their registration) — filed as `agenda-buddy-5og`.

**Two more real defects found live, not fixed (test-only tasks, no production changes in scope):**
`UpdateCustomerCommandHandler` audits its failure branch under the wrong event `Type` (`"UpdateProviderCommand"`,
a copy-paste bug) — filed `agenda-buddy-id4`. `UpdateServicesFromProviderCommandHandler` writes no audit
event at all on its provider-not-found branch, a real CONSTITUTION §3 gap distinct from its sibling
`AddServicesToProviderCommandHandler`, which audits both branches — filed `agenda-buddy-f49`.

**One process gap, self-corrected.** `F-018-T15`'s worktree agent committed directly onto the shared feature
branch instead of its isolated worktree branch, bypassing the intended worktree-merge step. Its changes
(`scripts/verify-container-reaping.sh`, a `.github/workflows/dotnet.yml` step) didn't collide with anything
else in flight, so nothing was lost — but it's a deviation from the wave's stated execution mode, noted for
future wave briefs rather than silently absorbed.

**One bookkeeping gap, self-corrected.** `F-018-T10`, `T11`, and `T12`'s `tasks.cjs done` file write was
never committed by their respective worktree agents (only `T15`/`T16` remembered to `git add` it) — caught
during post-merge verification and fixed with a dedicated commit before this task started.

**One structural CI security-scan gap found live at Test layer 7b, fixed.** Running gitleaks locally
against the branch's full commit range (`origin/main..HEAD`) surfaced a match `gitleaks-action`'s own PR-scan
step never reported on PR #69. Root cause, confirmed by replaying PR #69's exact CI-logged command locally:
`gitleaks-action` always scans with `--first-parent --no-merges`, which never diffs a merge commit's second
parent — exactly where this project's own worktree-agent Construction convention (`git merge --no-ff`) puts
every line a sub-agent wrote. The specific match was a false positive (`GenerateThrowawayPrivateKeyPem`
builds PEM-shaped text from a key generated fresh at test time, never persisted, documented in the test's
own remarks) — resolved with a `.gitleaksignore` fingerprint + inline `gitleaks:allow` comment, same pattern
as F-017's canary fix. **The structural gap is the real finding**, independent of this one instance: any
secret introduced only via a worktree merge's second parent would have passed `gitleaks-action`'s step with
"no leaks found" every time, silently defeating CONSTITUTION §7's "always required, cannot be unchecked"
promise. Fixed by adding a second, independent full-range gitleaks step to `security-scan` with no
`--first-parent`/`--no-merges`. Filed `agenda-buddy-wow` (P1) — live-CI confirmation of the new step is still
owed, same class of gap as F-018-T21.

**One Important finding at Party Review, fixed.** Neo (N1) and Echo (E1) — linked, same root cause —
found that `EventStoreWriteGuardTest` proves less than AC-15's literal wording claims: it checks the whole
handler *file* for the audit call, not every *branch*. The exact gap it would need to catch to be a true
per-branch guard (`UpdateServicesFromProviderCommandHandler`'s missing audit, `agenda-buddy-f49`) was already
found and filed by hand, not by this guard — proving the finding's point. Fixed by narrowing the claim in
`F-018-T13.md` rather than building a bigger per-branch static-analysis check, which Neo judged
disproportionate under YAGNI for a "permanent guard" task. Three Advisory findings (stale `api-contracts.md`
OpenAPI-commit line, a low-value missing test-isolation case) accepted as logged warnings — see STATE.md's
Guardrail Log.

---

## 4. Not verified, and why

| Item | Why |
|---|---|
| AC-11 (image-pull failure diagnostics) | Never built — see §3. Requires new scope, filed rather than built under T20. |
| AC-14 (AppHost-already-running warning) | Never built — see §3. Same disposition. |
| AC-20's negative case (a red integration test actually blocking a PR) | Would require deliberately breaking a test and pushing a second throwaway PR — judged not worth a second live-CI round trip for a inference this safe; flagged rather than claimed. |
| CONSTITUTION §7 **Integration** checkbox | Deliberately left unchecked — gated on 10 consecutive green integration runs, tracked by `agenda-buddy-ym9` (F-018-T04). This session's PR #69 is one data point, not ten. |
| CONSTITUTION §7 **Security scan** gate | Implemented by F-017 (unlike at F-016's time); PR #69's `Security — dependency audit` job passed clean — no new findings from F-018's changes. |
| Atlas credential rotation (`ISSUE-002`) | Human-only, outside every feature to date; unchanged by F-018. |
| `AgendaBuddy.AppHost` / a running Aspire cluster | F-018 changes no Aspire wiring; the 93 AppHost tests still pass, untouched. |

---

## 5. Design documents corrected by implementation

| Document | Correction |
|---|---|
| `docs/pdlc/tasks/F-018/F-018-T07.md` | Absorption note corrected: F-016-T04 delivered AC-10 only, not AC-10/11/14 as originally claimed |
| `docs/pdlc/memory/DECISIONS.md` | **ADR-048** added: ADR-020's commit deferral cleared by F-016 shipping; specs now committed |
| `docs/pdlc/memory/CONSTITUTION.md` | §1 (net10.0, not .NET 8), §4 (MiniValidator-now/Validot-target per ADR-016), §9 (ADR-015's five packages recorded), §2 (`.editorconfig` + CI format gate, replacing "not yet configured") |
| `CLAUDE.md` | Headline test count 883→950; `AgendaBuddy.IntegrationTests/` folder description filled out (Contract/Persistence/Audit/OpenApi/Support all now populated); OpenAPI-spec provenance (two mechanisms, one committed); CI job list (format-check, container-reaping, spec-drift) |
| `docs/pdlc/context/15-cqrs-and-messaging.md` | Provider's `(kafkaClient as KafkaClient)!` downcast marked fixed; Booking/Customer's identical pattern marked still-dormant |
