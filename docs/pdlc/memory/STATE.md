# State
<!-- pdlc-template-version: 3.0.0 -->
<!-- This file is the live operational state of the PDLC workflow.
     It is written by PDLC hooks and commands — do not edit manually unless recovering from an error.
     Claude reads this file at the start of every session to auto-resume from the last checkpoint.
     If this file is missing or empty, PDLC will prompt you to run /pdlc init. -->

**Last updated:** 2026-08-22T23:55:00Z

---

## Current Phase

Construction Complete

---

## Current Feature

identity-hardening

_**F-016 `secure-public-endpoints` SHIPPED** as `v0.2.0` — merged `2134b8d`, PR #38, episode 002 (Final).
Operation closed 2026-08-22: smoke-tested against a live AppHost (all five formerly-anonymous PII GETs 401,
both Calendar routes 401/403, non-owners get `ProviderSummary` only, professions still 200, deleted POST 405),
backend re-verified 358/358 green on `main`, context catalog refreshed, review finding I-5 fixed. Cloud deploy
skipped for the second consecutive release — three unchanged blockers in `DEPLOYMENTS.md`._

_`api-refactor-foundations` (F-018) is **paused** — see `docs/pdlc/memory/.paused-feature.json`. Inception is
complete and merged; Construction was aborted at the wave-1 standup before any code. ⚠️ **Its plan is now
stale in a second way:** F-016 delivered the harness *and* the `Persistence` rename, so what remains is
OpenAPI/spec drift (partly done — `docs/api/openapi/` and `scripts/generate-openapi.sh` now exist),
`.editorconfig`, constitution amendments, the 10-green-run counter, mobile CI, the Tier 1–3 sweep, the Kafka
fake and final verification._

---

## Active Task
<!-- The task currently claimed by Claude, from the git-native task store.
     Format: [task-id] — [task title]
     Example: F-002-T03 — Add OAuth2 login with GitHub
     Set to "none" when no task is active. -->

none

---

## Roadmap Claim

- **Feature ID:** F-021
- **Feature record:** docs/pdlc/tasks/F-021/_feature.md
- **Claimed by:** oscargarcia@ogdevlabs.onmicrosoft.com
- **Claimed at:** 2026-08-22T15:35:00Z
- **Branch:** `feat/F-021-identity-hardening` (pushed)

⚠️ **The claim lives only on the feature branch.** The maintainer rolled `origin/main` back from `0d1a6ad`
to `5ef3e10` on 2026-08-22 — deliberately, to get in-flight F-021 work off `main` — so anyone pulling
`main` sees F-021 as *unclaimed* and sees none of its Inception or Construction artifacts. That resolves
when the PR merges. `main` still carries F-016's closeout and the tooling commit, which were in the same
push and were **not** rolled back.

---

## Night Shift

_None active. Run `/night-shift <F-NNN>` to start an autonomous run (requires bypass-permissions mode)._

---

## Current Sub-phase

—

---

## Last Checkpoint

Construction Complete / 2026-08-22T23:55:00Z — F-021 built: 7/7 tasks, 16/16 ACs attested in
`docs/pdlc/design/identity-hardening/verification.md`, threats T-101…T-105 mitigated and T-106 accepted.
**623 tests** (431 backend / 118 integration / 74 mobile), 0 failing, 0 warnings. Ready for `/ship`, which
needs the PR merged first.

---

## Party Mode

agent-teams

---

## Guardrail Log

| Timestamp | Guardrail | Detail |
|-----------|-----------|--------|
| 2026-08-18T12:44:29Z | ship_phase_mismatch | `/ship` started with Current Phase `Construction` (sub-phase Wrap-up), not `Construction Complete`. User confirmed: F-013's branch is merged to main and 14/14 tasks are done; the phase marker was never advanced after the ISSUE-001 fix. Bookkeeping gap, not unfinished work. |
| 2026-08-18T12:44:29Z | required_gate_unmet | CONSTITUTION §7 `Security scan (dependency audit + secret scan)` is marked always-required and un-uncheckable but is not implemented — CI has a single credential grep, not a scanner. Pre-existing project-wide gap, not introduced by F-013; owned by F-017. User authorized shipping with the gate unmet. Unit-test gate verified empirically: 305 passing / 0 failing / 0 warnings across 12 projects. |
| 2026-08-18T17:55:00Z | standards_gate_skipped | Define Step 6.5 (`--ideate`, advisory tier) skipped for F-016. The `nordstrom-standards-readiness` plugin **is installed**, but its six source standards repos do not resolve under this `gh` auth (needs SSO or VPN) and no local `.nordstrom-standards/` exists. Light skip per the advisory tier — the Plan-gate `--design` check will re-attempt. Same condition recorded at F-013 and F-018. |
| 2026-08-18T23:20:00Z | standards_gate_skipped | Review Step 12.6 (`enforcing` tier, full codebase assessment) **could not run**. The `nordstrom-standards-readiness` plugin is installed, but probing its sources confirms they still do not resolve under this `gh` auth (`nordstrom-engineering-standards`, `nordstrom-security-standards`: no response), there is no local `.nordstrom-standards/` cache, and no prior `docs/standards-readiness/` report to `--delta` against. Treated as **skip-with-notice (plugin unavailable)**, not as a user `/override`, so no ADR is minted. ⚠️ **This is the fourth consecutive gate this has blocked** — F-013 ship, F-018 Define, F-016 Define, F-016 Plan, and now F-016 Review. A gate marked `enforcing` that has never once executed is governance theatre; it needs either a reachable source (SSO/VPN, or a vendored `.nordstrom-standards/`) or an explicit decision to retire it. Recommend folding into F-017, which already owns CONSTITUTION §7's unimplemented scan gate. |
| 2026-08-18T23:35:00Z | review_warnings_accepted | Review approval gate (fix cycle 1 of 3): **0 Critical**. Maintainer chose **fix I-3 + I-4, accept the rest**. FIXED: **I-3** — AC-14 was verified on only 1 of the 6 remaining `ForbiddenException` catch sites; `RemainingLocalCatchSitesTest` now covers Booking `:125`/`:149`/`:174` and Services `:153`/`:177` in Production (integration 93 → 99). **I-4** — `CLAUDE.md` claimed "379 tests total: 305 backend" and omitted the integration command entirely; corrected to 531 (358+99+74) with the ADR-031 warning and a Key Files entry. ACCEPTED as logged warnings: **I-1** the providers-list cache holds *unprojected* entities and the projection is applied after the cache read — correct today, a trap for F-019/F-020 which rewrite that file; **I-2** `GET /api/v1/customers` returns full `CustomerEntity` (incl. `SubscribedProviderCollection`, `AppointmentCollection`, `KafkaTopic`) to any Provider-role caller — consistent with ADR-026's deferral of owner-scoping, now quantified against the real payload; **I-5** the catalog's 10-vs-9 handler line, due at the Ship refresh; and **A-1…A-7** advisories, notably that authorization failures are entirely unlogged (no log sink at all) so IDOR probing leaves no trace (F-021/F-024). |
| 2026-08-18T23:45:00Z | test_layers_skipped | Test Step 15: **layers 3–6 have no command in this project** and are **not required** gates in CONSTITUTION §7 — E2E (real Chromium), performance/load, accessibility, visual regression. Each discovered by searching every `.csproj`/`.json`/`.yml`/`.sh` for the usual runners (playwright/cypress, k6/NBomber/BenchmarkDotNet, axe/pa11y, percy/chromatic): no candidate found. Skipped with this warning rather than silently. Layer 7c (OWASP `dependency-check` CLI) is not installed → INFO. |
| 2026-08-18T23:45:00Z | required_gate_flagged_accepted | Test Step 15 **Layer 7 (security scan — always required, un-uncheckable)** RAN. **7a dependency audit:** `dotnet list package --vulnerable --include-transitive` reports exactly **one** vulnerable package across the whole solution — `SSH.NET 2024.2.0`, **HIGH**, `GHSA-q939-rpr3-3284` — in exactly **one** project, `AgendaBuddy.IntegrationTests`. All 25 pre-existing projects clean. This is **new on this branch** (the project did not exist on `main`, whose baseline was 0 vulnerable at the F-013 ship gate). Per Step 15 a new HIGH is a flagged required gate — **disposition already recorded in ADR-030** (maintainer-approved at T02: unreachable because Testcontainers only loads SSH.NET for Docker-over-SSH, which this project does not use, and the unreachability is *tested* by `ContainerRuntimeGuardTest`). **Not re-asked**, because re-deciding an ADR the maintainer already approved would be re-litigation. Confirms ADR-030's promise that the project-scoped `NU1903` suppression does not hide it from the audit report — it is listed. **7b secret scan on the 161 changed files:** clean on all six patterns (mongodb credential, AWS key, GitHub token, Stripe live key, PEM payload, assigned-secret literal); no `.env` files; every `appsettings` connection string still blank. ⚠️ Gate satisfied **by hand**, as at F-013 — CI still has only a credential grep, not a scanner. **F-017 still owns automating it.** |
| 2026-08-22T16:05:00Z | standards_gate_skipped | Define Step 6.5 (`--ideate`, advisory tier) skipped for F-021. Re-checked all three conditions on 2026-08-22: plugin installed, no local `.nordstrom-standards/`, no `docs/standards-readiness/` report to `--delta` against. Light skip per the advisory tier; the Plan-gate `--design` check will re-attempt. ⚠️ **Sixth consecutive gate blocked by this condition** (F-013 ship · F-018 Define · F-016 Define · F-016 Plan · F-016 Review · F-021 Define). Recommendation unchanged: give it a reachable source or retire it explicitly, folded into F-017 |
| 2026-08-22T23:20:00Z | standards_gate_skipped | Plan/Review Step 12.6 skipped again for F-021. Conditions re-checked and unchanged: plugin installed, its six source repos do not resolve under this `gh` auth, no local `.nordstrom-standards/`, no prior `docs/standards-readiness/` report to `--delta` against. **Seventh consecutive gate blocked by the same condition**, and the fifth marked `enforcing` that has never once executed. Treated as skip-with-notice (plugin unavailable), not a user `/override`, so no ADR is minted. The recommendation has not changed since F-013 and is now overdue: give it a reachable source (SSO/VPN or a vendored `.nordstrom-standards/`) or retire it explicitly. **F-017.** |
| 2026-08-22T23:40:00Z | required_gate_flagged_accepted | §7's always-required security scan RAN, **by hand for the third consecutive feature**. **Dependency audit:** unchanged from F-016 — one vulnerable package solution-wide, `SSH.NET` HIGH `GHSA-q939-rpr3-3284`, in `AgendaBuddy.IntegrationTests` only, dispositioned by ADR-030. **F-021 adds no package reference at all**: rate limiting and HSTS are both in the ASP.NET Core shared framework. **Secret scan:** clean on the six F-016 patterns over the changed files. The one place this feature could have introduced a leak is the new log sink, and that is asserted rather than reviewed (AC-16). ⚠️ CI still has a credential grep, not a scanner. **F-017 still owns automating it.** |
| 2026-08-22T23:45:00Z | pre_existing_test_deleted | `IdentityService_ConstructorParameters_ContainNoILogger` deleted — it asserted by reflection that `IdentityService` had **no** logger, which PRD requirement 17 contradicts directly. Replaced by the stronger content assertion (no address, password or token in any line). **F-021's only such deviation**, same class as F-016's ADR-025 deletion, recorded in ADR-034 and awaiting maintainer acknowledgement. Found while wiring it: the three sanitization tests beside it were **vacuous** — they iterated a logger factory connected to nothing. |
| 2026-08-22T23:50:00Z | test_layers_skipped | Test layers 3–6 (E2E, performance/load, accessibility, visual regression) skipped for F-021, as for F-016: no command exists in this project and none is a required §7 gate. Layers 1 (unit, **required**) and 2 (integration, required *by this PRD* because AC-6/AC-13/AC-15 are only meaningful against a running service) both ran green. |

---

## Active Blockers

> ### 🔖 RESUME MARKER — updated 2026-08-22T23:55Z, **F-021 BUILT, NOT SHIPPED**
>
> **Everything F-021 is on `feat/F-021-identity-hardening`, and nothing of it is on `main`.** The maintainer
> rolled `origin/main` back from `0d1a6ad` to `5ef3e10` on 2026-08-22, deliberately, to take in-flight F-021
> work off `main`; the branch was created at `0d1a6ad` first, so nothing was lost. `main` keeps F-016's
> closeout (`2a8ee43`) and the tooling commit (`5ef3e10`), which were part of the same push and were **not**
> rolled back.
>
> **Built and green:** all three defects closed — non-destructive refresh rotation, per-IP limiting on
> `login` **and** `register` plus a self-clearing per-account lock, and transport security (HSTS + redirect
> before authentication) in all seven services. 7/7 tasks, 16/16 ACs attested, T-101…T-105 mitigated, T-106
> accepted. **623 tests** (431 / 118 / 74), 0 failing, 0 warnings, integration 1 m 28 s of a 600 s budget.
>
> **The next action is a human one: review and merge PR [#39](https://github.com/ogdevlabs/agenda-buddy/pull/39).**
> CI is green on all four jobs and the PR is mergeable; `/ship` cannot run before it merges.
>
> **Three things a reviewer should be told rather than discover:**
> 1. **One pre-existing test was deleted** — `IdentityService_ConstructorParameters_ContainNoILogger`
>    asserted by reflection that `IdentityService` had no logger, which contradicts requirement 17 head-on.
>    Replaced by the stronger content assertion. ADR-034. **This needs acknowledgement**, as F-016's ADR-025
>    deletion did.
> 2. **Both new controls default OFF** and are gated on configuration, not `IsProduction()` (ADR-033),
>    because the AppHost runs every service as *Production* locally. The AppHost's cloud graph turns them
>    on; a startup warning names the key when they are off outside a local run.
> 3. **Three pre-existing tests were vacuous** (`Login_ValidCredentials_DoesNotLog*` asserted over an empty
>    list) and one F-016 test had a latent order dependency that F-021's new test class exposed
>    (`TelemetryPiiTest.RedactionPreservesThePathShape`). Both fixed; details in `verification.md` §3.
>
> **Still not F-021's, and still true:**
> - **`Integration — real services + MongoDB` is not a required status check on `main`.** Branch protection
>   is a GitHub setting, not YAML. Needs the web UI or an API call with the credential `git` uses — `gh`
>   here is a different work identity.
> - **§7's security scan was satisfied by hand again**, for the third consecutive feature. **F-017.**
> - **§7's Integration checkbox: 3 of the 10 consecutive green runs** it is gated on. PR #39's run was the
>   third; F-016's PR #38 supplied the first two.

<!-- PENDING MARKER — read this first at the start of the next session. Each item below is either
     an action only a human can take, or work that is written but not yet exercised. Nothing here is
     blocked on more code being written. -->

### 1. ⚠️ Rotate the `agenda_buddy` Atlas credential — highest residual risk, human-only

**→ `docs/issues/ISSUE-002-atlas-credential-rotation.md`** (tracker: `agenda-buddy-41s`)

> ⚠️ **CORRECTED 2026-08-18 — the PII claims in this block are WRONG.** The maintainer confirmed the
> cluster holds **only synthetic / development data, never real people's records**. Severity re-graded
> **CRITICAL → MEDIUM**. Rotation is still required (the credential is still valid, publicly
> recoverable from `origin/main` history, grants write access to a live cluster, and there are no
> backups) — but there is **no personal-data breach, no GDPR clock and no notification duty**. See the
> correction block at the top of ISSUE-002.

A connection string with **full read/write** access to the cluster was committed to 17 tracked files.
F-013 removed it from the working tree; it **remains in git history and remains valid until the
password is changed at Atlas**. ~~The cluster holds client names, email addresses, phone numbers and
appointment records — who met which therapist or coach and when. That makes an unrotated credential a
notifiable personal-data breach with a 72-hour GDPR clock~~ — **struck 2026-08-18: the cluster holds only
synthetic/development data, so there is no personal-data breach and no GDPR clock.** What remains true:
an unlogged data-modification risk with **no backups to restore from**, Atlas resource abuse billed to
the project owner, and the first prerequisite for any cloud deployment. Documenting it again is
not progress; only the rotation closes it. ISSUE-002 has the exact Atlas steps, the access-log review
window, and the command that finds the first commit containing it.

### 2. Cloud deployment capability is written but never run

**→ `docs/deployment.md`** (tracker: `agenda-buddy-dwe`, blocked by `agenda-buddy-41s`)

`azure.yaml`, `.github/workflows/deploy.yml` and the `DeploymentTarget.Cloud` shape of the AppHost all
exist and are covered by 47 AppHost tests, but **no deployment has been performed** — there is no Azure
subscription wired to this machine. The first deployment must be run by hand (`azd up`) because azd
discovers the parameter names interactively; those names then go into the `AZD_ENV_VARS` repository
secret for the workflow. Item 1 is a hard prerequisite: deploying against an unrotated credential means
the deployment and whoever else holds it share a database.

### 3. ~~Three dashboard visual checks for F-013~~ — ✅ **DONE 2026-08-18**

**→ `docs/pdlc/archive/design/aspire-wiring/verification.md`** (tracker: `agenda-buddy-e7e` — **closed**)

Completed at the v0.1.0 Ship/Verify gate against a live AppHost. All 7 services reported
`/health` = `Healthy` and `/alive` = 200; 21× health + 21× alive + 5 deliberately email-bearing
requests were generated. A human confirmed all three: telemetry renders for all 7 in traces, metrics
and structured logs; `http.route` is a template and `url.path` shows the email **redacted** (the
literal `customer.pii@example.com` never appeared in a span despite five attempts, one of which
returned 200); and both JWT parameters render **masked** on the `identity` resource.

**Nothing in F-013 is now recorded as unverified.**

### 4. Roadmap ordering — F-016 ✅ **SHIPPED**, F-021 ✅ **BUILT (not shipped)**. Remaining: **F-014 → F-015 → F-017 → F-018–F-020**

F-018 was being worked ahead of F-014–F-017 at the user's request. That is **no longer the case**:
F-018 finished Inception (PR #37 merged) and then had Construction **aborted at the wave-1 standup,
before a single line of code**. A **program-level Discover** then ran across all four
(`docs/pdlc/brainstorm/brainstorm_platform-remediation_2026-08-18.md`, approved) and decomposed them
**4 → 6**:

- **F-016 goes first, and carries the verification harness.** `docs/pdlc/context/11-testing.md:148`
  establishes that `Program.cs` is not coverable and **there is no integration test in the solution**,
  so endpoint authz — precisely what F-016 changes — is the one thing nothing here can verify. The
  Calendar IDOR exists *because* of that gap. F-016 therefore absorbs **six tasks from F-018's
  already-approved, already-spiked plan** (T01, T05, T06, T08, T09, T14) as its wave 1.
- **F-021 `identity-hardening` is new** — split out of F-016 because it grew past one PRD. It carries
  the account-destroying `RefreshAsync`, the HTTPS-before-auth ordering, rate limiting, and the
  `AssertOwner` null-claim hole.
- **F-022–F-024 filed:** password reset (downstream of F-014 — needs `NotificationService`), token
  revocation, data-subject rights.
- ⚠️ **F-018 is now ~14 tasks, and its paused plan is stale.** Recorded in `.paused-feature.json`.

The known-bad conditions this order addresses, in the order they now get addressed:

- **F-014** — six shipped-but-unreachable capabilities (`NotificationService`, `MessageService`,
  `NoteService`, `PaymentService`, `ReportingService`, `DeactivateProviderCommand`): domain code and
  unit tests exist, but no DI registration, no configured collection, no HTTP route. F-006–F-010 are
  marked Shipped on code nothing can call.
- **F-015** — the mobile client cannot reach the backend: missing `api/v1/` prefixes, three wrong base
  URLs, no gateway, refresh-token flow wired but unused, `LogoutAsync` never calls the server.
- **F-016** — ✅ **SHIPPED `v0.2.0`, 2026-08-18.** The anonymous PII exposure, the Calendar IDOR and the
  never-called `AssertRole` are all closed — verified live at the ship gate, not by inspection. It also
  delivered the integration harness and the `Persistence` rename, both absorbed from F-018's plan.
- **F-021** — ✅ **BUILT 2026-08-22, awaiting review and merge.** The account-destroying refresh, the
  missing rate limiting and lockout, and the transport-security ordering are all closed on
  `feat/F-021-identity-hardening`. Its fourth inherited item was already closed by F-016-T09. The
  harness F-016 built is what caught the one defect this feature introduced (a rejected refresh answering
  500 instead of 401) — the second time that investment has paid for itself.
- **F-017** — three Dockerfiles publish `net10.0` onto a `dotnet/runtime:8.0` base and cannot run;
  CONSTITUTION §7's dependency-audit + secret-scan gate is still mandated-but-unimplemented.

F-018's Inception artifacts stay valid and its 20 tasks stay in the store — resuming it costs no
re-planning (`/continue`).

### Resolved, kept for context

- **F-013 SHIPPED as `v0.1.0` on 2026-08-18.** Tagged at `c86bca9` and pushed — the first PDLC-tracked release in a repo that had zero tags despite 13 features marked Shipped. Episode 001 committed. **Deploy deliberately skipped**, with reasons recorded in `DEPLOYMENTS.md` rather than silently omitted. `CONSTITUTION` §7's security scan was run **by hand** at the ship gate: **0 vulnerable packages** across all 25 projects, working tree clean, and 9 commits confirmed to still carry the credential in history. That was greps, not a scanner — it does **not** discharge the gate; F-017 still owns automating it.
- **69 whitespace findings fixed 2026-08-18.** `dotnet format` across `agenda-buddy-backend.slnf`, committed as a separate `style:` commit *after* the v0.1.0 tag. 305 tests pass before and after. **The repo still has no `.editorconfig`, so this drift will return** — adopting one is worth folding into F-018.
- **F-013-T14 / ISSUE-001 — RESOLVED 2026-08-18, merged in PR #35.** The AppHost now starts all 7 services. Root cause was a missing `AgendaBuddy.AppHost/Properties/launchSettings.json`: without `DOTNET_ENVIRONMENT=Development` the AppHost ran as `Production`, user secrets never loaded, every secret parameter went `ValueMissing`, and all seven services parked in `Waiting` with nothing logged. Both "blockers" in the original report were misdiagnoses — `AddProject<TProject>` was never at fault. A second defect surfaced once services could start: `WithReference(database)` injects `ConnectionStrings__agenda-buddy`, not the `ConnectionStrings:mongodb` that `MongoConnectionResolver` reads, which crashed `profession` on startup.
- **`agenda-buddy-prr` — RESOLVED 2026-08-18.** `MobileApp` did not compile under `/p:MobileWorkloads=false` (`CS0103 'Application'`), which had been failing the `build-mobile-tests` job outright — all 67 MobileApp tests had never run in CI. Guarded with the existing `MOBILE` constant.
- **CI guard that never ran — RESOLVED 2026-08-18.** `Assert every service starts in Development` consumed `secrets.CI_JWT_*`, which were never created. It was added by F-013 and CI only triggers on push to `main` or a PR to `main`, so it first executed — and first failed — on PR #35. It now generates a throwaway keypair in-step.

---

## Context Checkpoint

<!-- Written to be read COLD: a fresh session should be able to pick F-021 up from this block alone. -->

```json
{
  "written_at": "2026-08-22T23:55:00Z",
  "reason": "Construction complete; the next step is a human review-and-merge",
  "phase": "Construction Complete",
  "sub_phase": null,
  "feature": "identity-hardening",
  "feature_id": "F-021",
  "active_task": null,
  "resume_command": "/ship (BLOCKED until the PR merges)",

  "branch": "feat/F-021-identity-hardening",
  "branch_base": "0d1a6ad — the commit the maintainer rolled off main on 2026-08-22. The branch was cut FIRST, so nothing was lost.",
  "main_is_at": "5ef3e10 (F-016 closeout + tooling commit; NO F-021 work)",
  "working_tree": "clean",

  "progress": "7 of 7 tasks. 16 of 16 ACs attested in docs/pdlc/design/identity-hardening/verification.md.",

  "test_state": {
    "backend": "431 passing / 0 failing / 0 warnings across 12 projects via `dotnet test agenda-buddy-backend.slnf`. 358 -> 431 (+73).",
    "integration": "118 passing in 1 m 28 s via `dotnet test AgendaBuddy.IntegrationTests/AgendaBuddy.IntegrationTests.csproj` — a SEPARATE command (ADR-031). 99 -> 118 (+19). Needs a container runtime: export PATH=\"$HOME/.rd/bin:$PATH\" first.",
    "mobile": "74 (67 passing, 7 skipped) via `dotnet test MobileApp.Tests/MobileApp.Tests.csproj /p:MobileWorkloads=false` — re-verified, unchanged"
  },

  "WHAT_F021_ESTABLISHED": {
    "the_only_partial_update_primitive": "IRepository<T>.FindOneAndUpdateAsync(BsonDocument filter, BsonDocument update). Post-image (ReturnDocument.After) so the incremented counter comes back with the write. NEVER upserts — that is a property of the METHOD, and AC-9 rests on it. ADR-032 forbids growing it into a query builder, and a test counts the interface's update members so the next overload has to be argued for.",
    "filters_carry_the_guard": "Anything that must still be true at the moment of the write belongs in the FILTER, not in a preceding read. Refresh rotation's filter carries the presented hash (single use), the expiry, and 'not locked' — all three in one atomic operation.",
    "not_locked_needs_both_or_branches": "{$or: [{lock_until: null}, {lock_until: {$lte: now}}]}. In MongoDB a missing field satisfies NO comparison operator, so `lock_until <= now` alone never matches an account that has never been locked — which is nearly all of them. Verified against real Mongo, not just the double.",
    "in_memory_double_is_strict": "Identity.Tests/Helpers/InMemoryRepository.cs THROWS on any filter field or update operator it does not implement. Add support there rather than working around it: a double that silently ignores a clause reports green for a query MongoDB would answer differently. It also returns a SNAPSHOT, not the live stored object.",
    "fault_injection": "InMemoryCredentialRepository.FaultBetweenMatchAndWrite — an Action invoked after the filter matches and before any mutation. Throw a MongoException from it to reproduce a fault on the HANDLED path. This is what made AC-2 expressible; 11-testing.md:65 said it was not.",
    "config_gated_not_environment_gated": "Security:RateLimiting:Enabled and Security:Hsts:Enabled, BOTH DEFAULT OFF. Do NOT switch either to IsProduction(): every service runs as Production under the local AppHost. The AppHost injects Security__Local=true locally and turns both on in the Cloud branch of AppHostWiring.cs. ADR-033.",
    "harness_can_enable_them": "ServiceHostFixture.StartService(environment:, settings:) — pass settings to switch a control on for one hosted service. That capability IS threat T-103's mitigation; without it the only tests that could reach these controls would be ones nobody runs.",
    "logging_vocabulary": "credential.created / .login-ok / .login-failed / .locked / .reset / .rotated / .session-ended, each with acct_<12 hex of SHA-256(email)> and NEVER the address. PiiRedactingProcessor redacts SPANS, NOT LOGS."
  },

  "GOTCHAS_THAT_WILL_BITE": [
    "`scripts/tasks.cjs` DOES NOT EXIST in this repository, though docs/pdlc/tasks/index.md says it generates that file and F-016's records assume it. F-021's task files and the F-021 section of index.md are HAND-WRITTEN. `tasks.cjs check` could not enforce the security-AC-to-test linkage structurally, so each [security] AC names its test in the task body instead.",
    "The 401 body is NOT empty. UseStatusCodePages turns a bodyless 401 into ProblemDetails — the same surprise F-016 hit with 403. Assert indistinguishability as IDENTICAL bodies (after stripping the per-request requestId), never as ABSENT.",
    "ASP.NET's HSTS middleware SKIPS localhost, 127.0.0.1 and [::1] by default. A test asserting the header must use a non-localhost Host. Do not clear ExcludedHosts to make a test easier — that default is what stops a local experiment poisoning a browser's HSTS cache for weeks.",
    "TestServer leaves Connection.RemoteIpAddress NULL, so every harness request shares the limiter's 'unattributed' partition. Convenient here; it is also the shape of a real deployment behind a proxy that does not forward the address (agenda-buddy-end).",
    "UseHttpsRedirection is a NO-OP wherever no HTTPS port is known, which is every local run, every CI run and the whole integration suite. That is why the reorder was safe to do in all seven services at once — and why it fixes an exposure that only materialises once F-017 terminates TLS.",
    "docker is NOT on PATH under Rancher Desktop: export PATH=\"$HOME/.rd/bin:$PATH\" before anything that touches containers.",
    "Do NOT add AgendaBuddy.IntegrationTests to agenda-buddy-backend.slnf (ADR-031).",
    "Party mode is `solo` for this whole feature — the session carried a standing instruction not to call the Agent tool, which overrides STATE's `Party Mode: agent-teams`."
  ],

  "MEASURED_FACTS": {
    "bcrypt_verify_wf12": "262 ms on this hardware (20 iterations after JIT warm-up, BCrypt.Net-Next 4.0.3). 3.8 attempts/sec/core. This number is the reason the limiter exists, the reason it covers `register`, and the reason the lock is checked before the verify.",
    "limiter_defaults": "10 requests/minute per IP = ~2.6 s of BCrypt CPU/minute/IP, against a legitimate need of 2-3 attempts.",
    "lockout_defaults": "10 consecutive failures, 15-minute self-clearing window. No permanent lock and no admin unlock, because F-022 does not exist.",
    "integration_suite": "118 tests, 1 m 28 s warm."
  },

  "READ_FIRST_ON_RESUME": [
    "docs/pdlc/design/identity-hardening/verification.md — all 16 ACs, the red run, and the eleven things this feature does NOT claim",
    "docs/pdlc/memory/DECISIONS.md ADR-032/033/034, and ADR-011 which is now superseded",
    "Identity/Services/IdentityService.cs RefreshAsync + LoginAsync — the two orderings that are load-bearing",
    "AgendaBuddy.ServiceDefaults/TransportSecurity.cs — why the policy is central but the placement is not"
  ],

  "files_open": []
}
```

---

## Handoff

```json
{
  "phase_completed": "Construction",
  "next_phase": "Operation (/ship) — BLOCKED on a human merging the PR",
  "feature": "identity-hardening",
  "feature_id": "F-021",
  "key_outputs": [
    "Identity/Services/IdentityService.cs — non-destructive rotation, lock check before verify, atomic counter, credential-mutation logging",
    "Library/Repositories/IRepository.cs + MongoDbRepository.cs — FindOneAndUpdateAsync (ADR-032)",
    "AgendaBuddy.ServiceDefaults/TransportSecurity.cs — HSTS policy, UseAgendaBuddyTransportSecurity(), the startup flag audit",
    "Identity/Extensions/RateLimitingExtensions.cs + Identity/Configurations/SecurityOptions.cs",
    "AgendaBuddy.AppHost/AppHostWiring.cs — Security__Local locally, both controls on in the cloud graph",
    "docs/pdlc/design/identity-hardening/verification.md — 16/16 ACs attested",
    "DECISIONS.md ADR-032/033/034; ADR-011 superseded"
  ],
  "decisions_made": [
    "One narrow partial-update primitive on IRepository<T>, shared rather than Identity-only (ADR-032)",
    "Configuration-gated controls with the AppHost declaring local-vs-cloud; warn loudly rather than fail fast (ADR-033)",
    "UseHttpsRedirection stays unconditional — only HSTS is flag-gated, because six services already redirected unconditionally",
    "Replace the reflection guard that forbade a logger with a content assertion; log acct_<hash>, never the address (ADR-034)",
    "T-106 accepted (per-process limiter state); T-NL-2 accepted (a locked account answers faster than a wrong password, because hiding it re-arms T-101)"
  ],
  "next_action": "Human: review and merge PR #39, then run /ship",
  "human_only_items": [
    "Review and merge PR #39 (CI green, mergeable). main is PR-protected, and it was deliberately rolled back to 5ef3e10 to keep this work off it.",
    "Acknowledge the one deleted pre-existing test (ADR-034) — the same acknowledgement F-016's ADR-025 needed.",
    "Add `Integration — real services + MongoDB` to main's required status checks. Still not done; branch protection is a GitHub setting, not YAML.",
    "Rotate the Atlas credential (agenda-buddy-41s). Unchanged, still P0, still the hard prerequisite for any deployment."
  ],
  "open_questions": []
}
```

---

## Phase History

| Timestamp | Event | Phase | Sub-phase | Feature |
|-----------|-------|-------|-----------|---------|
| 2026-07-30T00:00:00Z | init | Initialization | — | none |
| 2026-07-30T00:01:00Z | init_complete | Initialization Complete | — | none |
| 2026-07-30T04:10:00Z | discover_complete | Discover Complete | Discover | auth-and-identity |
| 2026-07-30T04:20:00Z | prd_approved | PRD Approved | Define | auth-and-identity |
| 2026-07-30T04:45:00Z | design_approved | Design Approved | Design | auth-and-identity |
| 2026-07-31T05:05:00Z | inception_complete | Inception Complete | Plan | auth-and-identity |
| 2026-07-31T11:00:00Z | inception_complete | Inception Complete | Plan | mobile-app |
| 2026-07-31T11:05:00Z | construction_start | Construction Started | Build | mobile-app |
| 2026-07-31T11:40:00Z | construction_complete | Construction Complete | Build | mobile-app |
| 2026-08-15T16:45:00Z | roadmap_claim | Inception | Discover | aspire-wiring |
| 2026-08-15T17:30:00Z | inception_complete | Inception Complete | Plan | aspire-wiring |
| 2026-08-17T19:51:11Z | construction_start | Construction Started | Build | aspire-wiring |
| 2026-08-17T20:12:00Z | task_complete | F-013-T01 done — R-1 resolved, escape hatch taken | Build | aspire-wiring |
| 2026-08-17T20:25:00Z | wave_kickoff | Wave 2 standup — 4 dep edges added, ARCHITECTURE §3.3/§3.5 corrected | Build | aspire-wiring |
| 2026-08-17T20:45:00Z | task_complete | F-013-T03 done — MongoConnectionResolver + MongoHealthCheck, 22 tests | Build | aspire-wiring |
| 2026-08-17T20:58:00Z | task_complete | F-013-T02 done — AgendaBuddy.ServiceDefaults, 9 tests | Build | aspire-wiring |
| 2026-08-17T21:05:00Z | task_complete | F-013-T07 done — KafkaClient config-driven, 6 tests | Build | aspire-wiring |
| 2026-08-17T21:20:00Z | task_complete | F-013-T04 done — 28 per-service resolution tests (red half) | Build | aspire-wiring |
| 2026-08-17T21:35:00Z | task_complete | F-013-T05 done — shared IMongoClient across 7 services + EventStore | Build | aspire-wiring |
| 2026-08-17T21:45:00Z | task_complete | F-013-T08 done — AppHost, 28 model tests | Build | aspire-wiring |
| 2026-08-17T21:52:00Z | task_complete | F-013-T09 done — credential removed from 17 tracked files | Build | aspire-wiring |
| 2026-08-17T21:58:00Z | task_complete | F-013-T06 done — CI filters, AppHost build, 2 guards | Build | aspire-wiring |
| 2026-08-17T22:02:00Z | task_complete | F-013-T11 + T12 done — README, ADR-013 | Build | aspire-wiring |
| 2026-08-17T22:06:00Z | task_complete | F-013-T13 done — captive dependency fixed, 7/7 services start | Build | aspire-wiring |
| 2026-08-17T22:10:00Z | task_complete | F-013-T10 done — 17 ACs verified, 5 split to T14 (no container runtime) | Build | aspire-wiring |
| 2026-08-17T22:20:00Z | review_complete | Party Review — 0 Critical, 3 Important (all fixed), Echo did not report | Review | aspire-wiring |
| 2026-08-17T22:30:00Z | construction_paused | Build+Review done, 282 tests green; ship gated on T-014 (AppHost run unproven) | Wrap-up | aspire-wiring |
| 2026-08-18T00:30:00Z | issue_resolved | ISSUE-001 root-caused + fixed (missing launchSettings.json → Production → user secrets never loaded); 7/7 services Healthy under the AppHost; 294 tests green | Wrap-up | aspire-wiring |
| 2026-08-18T12:44:29Z | operation_start | Ship started. 2 guardrail warnings logged (phase-marker mismatch; §7 scan gate unimplemented). Unit gate verified: 305 passing, 0 warnings | Ship | aspire-wiring |
| 2026-08-18T12:55:00Z | tagged | v0.1.0 tagged at c86bca9 and pushed — first tag in the repo. CHANGELOG's first PDLC entry written | Ship | aspire-wiring |
| 2026-08-18T13:00:00Z | deploy_skipped | Deploy skipped with reasons recorded: unrotated Atlas credential gates it, no Azure subscription, first azd up must be interactive | Ship | aspire-wiring |
| 2026-08-18T13:10:00Z | verify_complete | §7 scan run by hand (0 vulnerable packages / tree clean / 9 commits still carry the credential). 3 dashboard visual checks confirmed by human against a live AppHost; agenda-buddy-e7e closed. F-013 has nothing unverified | Verify | aspire-wiring |
| 2026-08-18T13:30:00Z | operation_complete | Episode 001 committed and pushed. ROADMAP drift repaired (F-014–F-017 added). F-013 shipped, claim released. Artifacts archived | Reflect | aspire-wiring |
| 2026-08-18T13:40:00Z | roadmap_claim | F-018 refactor-minimal-apis claimed, ahead of F-014–F-017 at explicit user request | Discover | refactor-minimal-apis |
| 2026-08-18T15:25:00Z | discover_complete | Scope decomposed into F-018/F-019/F-020. Identity's 5 write endpoints + existing DTOs found; OTLP-suppression inference withdrawn | Discover | api-refactor-foundations |
| 2026-08-18T16:10:00Z | prd_approved | 27 reqs / 27 ACs / 9 stories, after a walkthrough that found 5 defects incl. AC-7 claiming an audit tier for a service with no audit trail | Define | api-refactor-foundations |
| 2026-08-18T16:30:00Z | spikes_complete | Both gating risks spiked BEFORE Design, both passed. Measured 4.45s container startup reversed container-per-test to per-class; ISwaggerProvider removed the feared 6th dependency | Design | api-refactor-foundations |
| 2026-08-18T17:05:00Z | design_approved | 5 artifacts. Threat model Full (7 threats). Repo verified PUBLIC; cluster confirmed SYNTHETIC — T-001 re-graded CRITICAL→MEDIUM and overstated PII/GDPR claims corrected across 5 documents | Design | api-refactor-foundations |
| 2026-08-18T17:45:00Z | inception_complete | 20 tasks / 7 waves / 31 ACs. Readiness Full → Fair (3 gaps, adversarial pass refuted all 3 self-rated Strongs); AC-31 added at the gate | Plan | api-refactor-foundations |
| 2026-08-18T17:33:01Z | construction_start | Construction started, tasks to run **sequentially** at user request. Pre-flight clean: channel in-sync; PR #37 (Inception artifacts) merged to main, branch rebased; `tasks.cjs check` clean apart from the 3 expected `security-ac-untested` warnings | Build | api-refactor-foundations |
| 2026-08-18T17:35:00Z | wave_kickoff | Wave 1 standup — the plan's "fully parallel" claim was **wrong**; 3 ordering edges found and applied (T02→T01 because CONSTITUTION §9 still forbids the rename; T01→T03 because T03's repo-wide `dotnet format` would absorb the rename diff AC-16 protects; T02→T03 because both write CONSTITUTION.md). Order set to T02→T01→T03→T04→T19 | Build | api-refactor-foundations |
| 2026-08-18T17:37:14Z | construction_aborted | Build aborted **before any code was written** at the user's explicit request, to deliver F-014–F-017 first. Claim released, feature paused, roadmap order restored. Inception artifacts remain valid — resume needs no re-planning | Build | api-refactor-foundations |
| 2026-08-18T17:37:14Z | feature_paused | Inception Complete — Ready for /build | Plan | api-refactor-foundations |
| 2026-08-18T17:46:32Z | roadmap_claim | F-014 claimed as anchor for a **program-level Discover** across F-014–F-017, chosen over starting Inception on one feature | Discover | platform-remediation |
| 2026-08-18T17:52:00Z | discover_complete | **Program decomposed 4 → 6.** All four premises verified against code and held; two were under-scoped; 10 catalogued defects belonged to no feature. F-016 split (→ F-021 identity-hardening), harness absorbed from F-018 (6 tasks) because `11-testing.md:148` proves endpoint authz is unverifiable today, F-022–F-024 filed. New order F-016 → F-021 → F-014 → F-015 → F-017. Claim moved to F-016 | Discover | platform-remediation |
| 2026-08-18T18:50:52Z | prd_approved | F-016 `secure-public-endpoints`: 20 requirements / 19 ACs / 9 stories. The flagged product call (authenticating provider discovery) was confirmed with evidence — F-003's shipped definition makes discovery post-signup. Anonymous PII GET count corrected 4 → 5 (`services/{email}` had been omitted) | Define | secure-public-endpoints |
| 2026-08-18T18:58:00Z | design_complete_pending_approval | 5 artifacts. Threat model **Full** (3/3): 8 threats, 1 CRITICAL / 2 HIGH / 5 MEDIUM — **5 of 8 created by this feature**. UX review **Skip** (0/3, no UI). Design **changed the PRD twice**: req 18 reassigned from F-021 into F-016 (T-001 makes the `AssertOwner` null-claim hole reachable, landing on the owner branch), and req 14's approach replaced by AD-1 because the existing exception handler is Development-only. 3 scope additions escalated to the human | Design | secure-public-endpoints |
| 2026-08-18T19:05:00Z | design_approved | 5 artifacts approved. All 7 mitigate-now threats confirmed; 3 open questions resolved in favour of the stronger option each time — T-003 → `Provider` role, T-007 → **delete the route**, T-005 → **add `Event.actor`**. ADR-022…028 written | Design | secure-public-endpoints |
| 2026-08-18T19:20:00Z | inception_complete | **20 tasks / 8 waves / 26 ACs** (19 + 7 threat-derived `[security]`). Readiness party **Full → Fair**, 4 gaps: **AC-12 contradicted ADR-025** (required a 403 on a route the ADR deletes — struck in-party, replaced by AC-26) and the **integration suite had no CI enforcement** (resolved at the gate by absorbing F-018's T18 as T20 — eight F-018 tasks now absorbed, not six). Standards gate skip-with-notice: plugin installed, sources unreachable | Plan | secure-public-endpoints |
| 2026-08-18T19:22:00Z | construction_start | Build started on `feat/F-016-secure-public-endpoints`, branched off freshly-pulled `main` at the maintainer's request. Wave 1 is a single task (T01) so no standup | Build | secure-public-endpoints |
| 2026-08-18T19:35:00Z | task_complete | **F-016-T01 done** — `Persitency` → `Persistence`. 11 files, one reference each, exactly as measured. **309 passing / 0 failing / 0 warnings** across 12 projects (305 baseline + 4 new). CONSTITUTION §9's prohibition retired *and its stated reason recorded as wrong* — the rename broke nothing. Red phase was 4 failing assertions, not a build break, because the test resolves the namespace via `Assembly.GetType` | Build | secure-public-endpoints |
| 2026-08-18T19:52:00Z | task_complete | **F-016-T02 done** — integration project + `InternalsVisibleTo` × 7. Three unanticipated findings: `WebApplicationFactory<Program>` is **ambiguous across 7 assemblies** (top-level statements → internal `Program` in the global namespace) — resolved via a public per-service anchor type, which also means `InternalsVisibleTo` is **not** what enables hosting, contrary to AC-2's rationale; **SSH.NET GHSA-q939-rpr3-3284 (HIGH) has no patched version** — accepted as unreachable and *tested* (ADR-030); excluded from the slnf per the MobileApp precedent so the unit gate stays Docker-free (ADR-031). Measured: container **3 s warm / 62 s cold**, beating the spike's 4.45 s. Backend 309 green, integration 9 green | Build | secure-public-endpoints |
| 2026-08-18T19:56:00Z | wave_kickoff | Wave 3 standup (solo) — **3 tasks confirmed parallel, 0 resequenced**; the plan's parallelism claim held, unlike F-018's wave 1. Five findings carried into the tasks: xUnit collection definitions are per-assembly so `Identity.Tests`' cannot be reused (B-1); `ContainerRuntimeGuardTest` starts a container unguarded (P-1); T04's probe/diagnose split is the substance of AC-7 (P-2); `MongoDbRepository<T>` is untestable without Mongo so its paging semantics land on T15 (E-1); keep the empty `METHOD()` stub per AC-19 (E-2) | Build | secure-public-endpoints |
| 2026-08-18T20:02:00Z | task_complete | **F-016-T03 done** — `CryptoSessionFixture` + `HarnessCollection` + AC-3's two tree-level assertions. Diverges from the `RsaKeyHelper` precedent by producing **no private-key PEM string at all**. Two corrections to how AC-3 had to be tested: the csproj half must match `ProjectReference`, not the project name, because seven production csprojs legitimately name the harness in `InternalsVisibleTo` (AC-2) and a string match would be red forever; and a **dead hardcoded public-key PEM constant** in `AuthenticationExtensionsTest` was the only committed PEM payload in the tree — removed, which is what makes AC-3 enforceable literally instead of with a carve-out. Hygiene tests placed in `Library.Tests` so the existing `api` CI job runs them. Backend 313 / integration 13 | Build | secure-public-endpoints |
| 2026-08-18T20:10:00Z | task_complete | **F-016-T04 done** — `DockerPreflight`. AC-7 **verified empirically end to end**: a bogus `DOCKER_HOST` now fails immediately with the endpoint, its source, the problem and four remedies instead of stalling. Probe split from diagnose so the message is testable without uninstalling Docker. **Both halves of the task's stated premise were wrong** and are corrected in code: Testcontainers.NET does not shell out to the docker CLI, and `/var/run/docker.sock` does not exist on this machine — the endpoint arrives via the `rancher-desktop` docker context, so a preflight hardcoded to the default socket would have reported a false failure. Never blocks on uncertainty, with its own test. Backend 313 / integration 23 | Build | secure-public-endpoints |
| 2026-08-18T20:17:00Z | task_complete | **F-016-T10 done** — `GetPagedAsync` on `IRepository<T>` and both implementers (exactly two, confirmed by grep). Negatives normalised to 0 in both, because `Skip(-1)` throws on the driver but is a silent no-op in LINQ — one interface would otherwise have two behaviours. Coverage split recorded rather than papered over: contract by reflection in `Library.Tests`, semantics against the in-memory implementer in `Identity.Tests`, **Mongo's own paging behaviour not covered until T15**. Backend 322 / integration 23 / mobile 74 unchanged | Build | secure-public-endpoints |
| 2026-08-18T20:22:00Z | wave_kickoff | Wave 4 standup (solo) — 2 tasks confirmed parallel, 0 resequenced. T06 kept independent of T05 by satisfying AC-4 against the **anonymous** `GET /api/v1/professions` route (B-1). Three findings pinned the fail-closed guard before it could be built wrong: never blindly overwrite the connection string or the guard compares its own value to itself (E-1); assert container **identity** via `GetConnectionString()`, never a `localhost` pattern (E-2); prove "no database created" by inspecting the container's database list, since a negative asserted by absence is unfalsifiable (E-3). Newly measured: all four appsettings resolution paths are empty strings, so the one live leak path is an env var | Build | secure-public-endpoints |
| 2026-08-18T20:28:00Z | task_complete | **F-016-T05 done** — `TokenFactory`. Tokens verified against the **services' own** `TokenValidationParameters`, read back out of `AddAgendaBuddyAuthentication`, so issuer/algorithm/clock-skew cannot drift from production and resurface as a mystery 401 in T07. No `CreateForeignSubjectToken` — that token is just `CreateToken` for somebody else, pinned as a decision by a test. `CreateTokenWithoutSubject` is the T-001 probe. Integration 27 | Build | secure-public-endpoints |
| 2026-08-18T20:34:00Z | task_complete | **F-016-T06 done — the second bottleneck is cleared, and the CRITICAL security AC is mechanically closed.** Real services now host over HTTP against a Mongo Testcontainer; `ProfessionHostTest` is the **first test in this solution to execute a route table** (`11-testing.md:148`). Fail-closed guard in two ordered layers: srv/credential rejection **before** a container starts (proven by asserting none was started), then host+port identity against the container's own endpoint — a dedicated test rejects `mongodb://127.0.0.1:27017` while the container is elsewhere, which is the case that broke the earlier pattern-check version at the threat party. The guard never echoes the rejected string, asserted. AC-20's "no database created" asserted **positively** by inspecting the container. **Verified empirically, against expectation: xUnit v2 does inject a collection fixture into a class fixture**, so session keypair + class container compose with no static workaround. Integration 41 in 16 s | Build | secure-public-endpoints |
| 2026-08-18T20:37:00Z | task_complete | **F-016-T07 done — the harness now observes what nothing in this solution could.** AC-6 proven over real HTTP against `PUT /api/v1/customers/{email}`: anonymous 401, expired 401, **foreign subject 403**, owner neither. All four green on the first run, so this confirms existing behaviour rather than fixing it. Test-only task, so the TDD gate holds without a manufactured red. **Trap found that would have produced a wrong conclusion:** `MiniValidator` runs *before* `AssertOwner` (`:150` vs `:153`), so a test with an invalid body gets 400 and never reaches the guard — and separately, validation preceding authorization lets an unauthorized caller probe validation rules (pre-existing; flagged to F-019/F-021). Integration 45 in 18 s | Build | secure-public-endpoints |
| 2026-08-18T20:44:00Z | wave_kickoff | Wave 6 standup (solo) — 0 resequenced, 0 edges added. **Three counting errors found in approved artifacts**, all verified against code: **9 query handlers, not 10** (the "10" originates in `15-cqrs-and-messaging.md:161`, which states *"10 queries, 10 handlers"* directly above a **9-row table**, and propagated into the PRD, ARCHITECTURE, the plan and T18's body); **7 hand-written `ForbiddenException` catch sites, not 8**; and **`Event.actor` cannot be "one assignment per handler"** because no handler can see the caller. Bolt found the ordering trap that would have made AC-13 fail *in Development only*. The actor-threading fork (~30 files vs ~8) was escalated to the maintainer, who chose to centralise in `EventStore` | Build | secure-public-endpoints |
| 2026-08-18T21:05:00Z | task_complete | **F-016-T08 done** — central 403 via the first `IExceptionHandler` in the codebase, in six services (not Identity). `UseExceptionHandler()` placed **after** the Development block, and both environments asserted, because the reverse order fails only locally. AC-13 satisfied by removing one local catch rather than shipping a test-only route. **Two design-doc corrections:** 7 catch sites not 8, and both 403 paths **already** share one body — `UseStatusCodePages()` converts a bodyless 403 to ProblemDetails, so my first AC-14 test asserted an empty body and correctly failed | Build | secure-public-endpoints |
| 2026-08-18T21:20:00Z | task_complete | **F-016-T09 done** — `AssertOwner`'s null-claim pass closed (T-001). `string.Equals(null, null)` is `true`, so a caller with no `sub` checked against an entity with no email was granted **ownership**. The pre-existing "no sub claim" test passed throughout because it supplied a *non-null* email — it threw for the wrong reason. Red was 3 of 5 new cases, precisely the null-null ones. AC-21's route half deliberately deferred to T11 and written into its task body | Build | secure-public-endpoints |
| 2026-08-18T21:45:00Z | task_complete | **F-016-T18 done** — query audit payloads reduced to `{"resultCount":N}` across **18 call sites in 9 handlers** via one `QueryAudit` factory, so "no entity data" is a property of one testable method. `Event.actor` stamped centrally in `EventStore` from `IHttpContextAccessor` (maintainer-chosen; ~8 files instead of ~30, and it attributes the 11 command handlers for free). ADR-027 amended, ARCHITECTURE §5 corrected | Build | secure-public-endpoints |
| 2026-08-18T21:55:00Z | task_complete | **F-016-T12 done** — the headline defect closed. All **five** anonymous PII GETs now 401; red phase had all five returning 200 with full records. Breaking with zero reachable consumers | Build | secure-public-endpoints |
| 2026-08-18T22:05:00Z | task_complete | **F-016-T17 done** — `POST /api/v1/professions` **deleted**, not role-gated (ADR-025 supersedes requirement 13; there is no admin role). Verified live before removal: **both** a Provider and a Customer token got 201 and wrote to the global catalogue. ⚠️ **The one AC-19 deviation** — one pre-existing test deleted because its subject was removed | Build | secure-public-endpoints |
| 2026-08-18T22:10:00Z | task_complete | **F-016-T16 done** — `Provider` role required to list customers (T-003). Brings `AssertRole` into use for the **first time**: per `13-security.md:137` it had never been called anywhere, so the `role` claim authorized nothing. Only the list route — a test pins that a customer can still read their own record | Build | secure-public-endpoints |
| 2026-08-18T22:15:00Z | task_complete | **F-016-T14 done** — role **and** ownership on `POST /api/v1/providers`. Both arms tested: a role check alone still lets one Provider create a record under another's email | Build | secure-public-endpoints |
| 2026-08-18T22:20:00Z | task_complete | **F-016-T13 done — the Calendar IDOR is closed** (T-006). Red phase: an intruder's token returned **200 with the owner's client email** on both routes. Every sibling service already guarded; Calendar was the one family that forgot, and nothing could catch it because there was no integration test. Guard sits **above** the cache read as a design invariant, pinned by a warm-cache regression test asserting "not 200-with-data" rather than "exactly 403" | Build | secure-public-endpoints |
| 2026-08-18T22:25:00Z | task_complete | **F-016-T11 done** — `ProviderSummary` projection. **Two api-contracts corrections:** there is **no `profession` field** and no service `duration` (neither exists on the entities, so F-015 would have bound to nothing), and the list is **homogeneous** because a mixed `items` array is not deserialisable. Added `OwnershipGuard.IsOwner` so ownership can be *branched on* rather than caught, keeping the null-claim rule in one place. Closes AC-21's second half | Build | secure-public-endpoints |
| 2026-08-18T22:30:00Z | task_complete | **F-016-T15 done** — pagination at the **database**, threaded through 12 files, because slicing in the endpoint would bound the response while leaving the extraction unbounded. Three things the ADR did not say, now recorded in it: the cache key must carry the page, `skip` arithmetic is overflow-guarded, and the envelope composes with T11's projection | Build | secure-public-endpoints |
| 2026-08-18T22:33:00Z | task_complete | **F-016-T19 done** — `verification.md`: all 26 ACs attested with every deviation named. Also closed the last near-miss by attesting AC-24 against the exact route it names, now that T12 authenticated it | Build | secure-public-endpoints |
| 2026-08-18T22:35:00Z | build_complete | **BUILD COMPLETE — 20/20 tasks.** F-016-T20 added a separate, duration-enforced integration CI job and fixed a silent-skip trap (the harness matched **no** path filter, so a harness-only change ran zero jobs). Backend **358** / integration **92** / mobile **74**, 0 failing, 0 warnings. All **7** threat-derived security ACs have linked tests. ⚠️ T20's job **has never run** and "blocking" needs a branch-protection change — both human-only. Review, Test and Wrap-up sub-phases **not yet run** | Review | secure-public-endpoints |
| 2026-08-18T22:40:00Z | branch_pushed | Branch pushed and **PR #38 opened** → `ogdevlabs/agenda-buddy#38`, at the maintainer's request. Opened via the GitHub API using the credential `git` already holds, **not `gh`** (which is authenticated to a different work identity) | Review | secure-public-endpoints |
| 2026-08-18T22:50:00Z | ci_failure | First CI run: **`Assert no committed database credential` failed** — correctly. Three fail-closed guard tests carried credential-**shaped** literals (synthetic passwords, real database name, Atlas-looking host). Every test and both AppHost guards passed; that grep was the only failure. **Not a missing repository secret** — the step is a `git grep` over tracked files and consumes none. Fixed by composing the strings at runtime (`HostileEndpoints`) rather than adding grep exclusions, which would have permanently weakened a project-wide scanner in a public repo | Review | secure-public-endpoints |
| 2026-08-18T23:05:00Z | ci_green | **Second run all green — and `F-016-T20`'s residual is RESOLVED.** The integration job ran for the **first time ever** and passed in **117–147 s** against its 600 s budget, on a cold runner; the PR itself was the trigger, so no throwaway branch was needed. Integration 93 (+1 theory case). ⚠️ Still outstanding: **"blocking" is a branch-protection setting** — `Integration — real services + MongoDB` must be added to `main`'s required status checks | Review | secure-public-endpoints |
| 2026-08-18T23:40:00Z | review_complete | **Party Review (solo)** — 0 Critical / 5 Important / 7 Advisory. Blast radius: **0 at-risk callers** across 19 public/signature-changed symbols, and the route sweep independently confirmed the PRD's "zero reachable consumers" premise *by a wider margin than claimed* (MobileApp calls `customer` singular, `booking/{id}`, `calendar?from=` and has **no provider service at all**). Phantom verified **7/7** threat mitigations with code *and* linked tests. Muse skipped (triage 0/3); Step 12.5 skipped for the same reason; Step 12.6 could not run. Approved with **fix cycle 1**: I-3 (AC-14 verified on 1 of 6 catch sites) and I-4 (`CLAUDE.md` stale, integration command missing) **fixed**; I-1/I-2/I-5 accepted as logged warnings | Review | secure-public-endpoints |
| 2026-08-18T23:50:00Z | test_complete | **Test complete.** Layer 1 unit **358** / 0 failing / 0 warnings (required gate ✅). Layer 2 integration **99** (not required; run anyway). Layers 3–6 have no command in this project and are not required — logged skips. **Layer 7 security scan (always required) RAN by hand:** 7a found exactly one vulnerable package solution-wide (`SSH.NET` HIGH, ADR-030-accepted, and confirming the `NU1903` suppression does not hide it from the report); 7b clean on six patterns across 161 changed files. Mobile 74 untouched. **531 tests total** | Test | secure-public-endpoints |
| 2026-08-18T23:55:00Z | construction_complete | **Construction Complete** — episode **002** drafted (`docs/pdlc/episodes/EPISODE_secure-public-endpoints_2026-08-18.md`, Status: Draft). PR #38 open, mergeable, CI green. Ready for `/ship` | — | secure-public-endpoints |
| 2026-08-19T00:20:00Z | merged_and_tagged | Merged to `main` as `2134b8d` (true merge commit), tagged **v0.2.0**, PR #38 merged. CHANGELOG entry added at `docs/pdlc/memory/CHANGELOG.md` — **not** the repo root, which has no CHANGELOG. Cloud deploy **skipped**, second consecutive release, three unchanged blockers | Operation | secure-public-endpoints |
| 2026-08-22T15:25:00Z | operation_complete | **F-016 shipped and Operation closed.** ⚠️ The gate stayed open **four days** — tag pushed 2026-08-18, closed 2026-08-22, with the STATE/DEPLOYMENTS edits uncommitted in a working tree the whole time. Verify ran against a live AppHost (no deployed environment exists): 9/9 authz checks as designed, register→login→authenticated-read end to end, non-owner projection confirmed live, backend re-run **358/358** on `main`, dependency audit + secret scan clean apart from the ADR-030 `SSH.NET` HIGH. **One new finding:** no cache invalidation exists anywhere in the solution (`agenda-buddy-xrw`, P2) — found by *running*, as in episode 001. Review finding **I-5 fixed** (9 queries, not 10) plus the same error in `00-overview.md`; context catalog refreshed for `01`/`11`/`13`/`15`/`00`; episode 002 **Final**; METRICS + Readiness reconciliation recorded | Idle | none |
| 2026-08-22T16:40:00Z | design_complete_pending_approval | F-021: 5 artifacts. Threat model **Full** (3/3): six threats, five to mitigate now, one accepted; five deprioritized. UX review **Skip** (0/3, no UI). **A measurement inverted the feature's own premise:** BCrypt verify at work factor 12 costs **262 ms** on this hardware, so guessing runs at 3.8 attempts/sec/core — while the same cost makes **unauthenticated CPU exhaustion** trivial, because the attacker spends the *server's* CPU (T-101). Two consequences: the limiter covers **`register` as well as `login`**, and it must be evaluated before any BCrypt work | Design | identity-hardening |
| 2026-08-22T20:00:00Z | design_approved | All six threat decisions recorded: T-101…T-105 mitigate now, T-106 accepted (per-process limiter state, one replica, no deployment). T-NL-2 accepted as a deliberate trade — a locked account answers *faster* than a wrong password, and hiding that oracle costs 262 ms per attempt and re-arms T-101. ADR-032/033/034 written; **ADR-011 marked SUPERSEDED**, with a note that its re-evaluation trigger ("if any auth anomaly is detected in logs") could never have fired because Identity had no log sink | Design | identity-hardening |
| 2026-08-22T20:10:00Z | inception_complete | **7 tasks / 16 ACs**, nine of them `[security]` with a linked test. Authored by hand: `scripts/tasks.cjs` is **not present in this repository**, so the task store and its index section are hand-written and `tasks.cjs check`/`done` could not enforce the security-AC linkage structurally. Every `[security]` AC names its test in the task file instead — a weaker mechanism, stated rather than glossed | Plan | identity-hardening |
| 2026-08-22T21:00:00Z | task_complete | **F-021-T01 done** — `FindOneAndUpdateAsync` on `IRepository<T>` (ADR-032), post-image, never upserting, plus `FaultBetweenMatchAndWrite` in the in-memory double. Blast radius measured first: exactly **two** implementers, both updated. The double is deliberately **strict** — an unimplemented operator throws rather than being ignored, because a double that silently skips a filter clause reports green for a query MongoDB would answer differently. Its own red test caught that it returned the **live** stored object rather than a snapshot, which made two successive post-images compare equal | Build | identity-hardening |
| 2026-08-22T21:40:00Z | task_complete | **F-021-T02 done — the account-destroying refresh is closed.** One `FindOneAndUpdateAsync` whose filter carries the presented hash (single use), the expiry check and a "not locked" condition; the credential is never deleted. Red phase was AC-2 under an **injected fault** — the case `11-testing.md:65` said could not be written. Both `$or` branches on `lock_until` are required, verified against real MongoDB: a missing field satisfies no comparison operator, so `lock_until <= now` alone would never match an account that has never been locked | Build | identity-hardening |
| 2026-08-22T22:10:00Z | task_complete | **F-021-T03 done** — atomic `$inc` counter and a self-clearing 15-minute lock. Lock checked **before** `BCrypt.Verify` (D-9), or a locked account costs 262 ms per attempt and the lock amplifies the DoS beside it — provable because the counter only moves on the verify-failed path. Success path is **one** write, not two: the rotation login already performed now carries the reset. AC-7's assertion had to change shape — the 401 body is **not** empty, because `UseStatusCodePages` turns a bodyless 401 into ProblemDetails, the same surprise F-016 hit with its central 403 | Build | identity-hardening |
| 2026-08-22T22:25:00Z | task_complete | **F-021-T04 done** — credential mutations logged with `acct_<12 hex>`, never the address (D-8/T-105). Deleted the reflection guard that forbade a logger (ADR-034) and discovered the three tests beside it were **vacuous**; they now run against real output with a `NotEmpty` guard, which is what made the first one go red | Build | identity-hardening |
| 2026-08-22T22:50:00Z | task_complete | **F-021-T05 done** — `UseAgendaBuddyTransportSecurity()` in ServiceDefaults plus one call per service, before `UseAuthentication` in all seven; Identity's Development-only guard removed (under the AppHost that condition was always true). HSTS flag-gated, conservative defaults, no `preload`/`includeSubDomains`. **The redirect is deliberately NOT flag-gated** — six services already called it unconditionally, so a flag defaulting to off would silently remove a control (ADR-033, amending ARCHITECTURE §4). AC-12 is a source-text test because `IApplicationBuilder` exposes no ordered list of middleware; it also bans direct `UseHttpsRedirection` calls, which is the likelier regression | Build | identity-hardening |
| 2026-08-22T23:15:00Z | task_complete | **F-021-T06 done** — per-IP sliding window on `login` **and** `register`, registered first in the pipeline so a throttled request costs no CPU and takes no write, 429 with `Retry-After` (the framework default is 503, which tells a client nothing). AppHost declares the run: `Security__Local=true` locally, both controls **on** in the cloud graph — the only artifact distinguishing "written" from "switched on". **The harness caught a defect no unit test could:** reading the signing key strictly at the top of `RefreshAsync` turned every *rejected* refresh into a 500, because the harness hosts Identity with no `JWT_PRIVATE_KEY` and every unit test sets it in its constructor | Build | identity-hardening |
| 2026-08-22T23:50:00Z | build_complete | **BUILD COMPLETE — 7/7 tasks, 16/16 ACs.** Backend **431** (+73) / integration **118** (+19) / mobile **74** unchanged = **623**, 0 failing, 0 warnings; integration 1 m 28 s against 600 s. Context catalog refreshed where this feature made it false: `13-security.md`, `01-api-surface.md`, `02-entry-points.md`, `04-data-access.md`, `05-data-model.md`, `06-configuration.md`, `11-testing.md`. Two findings filed rather than fixed: `agenda-buddy-b0w` (no unique index on `credentials.email` on any path anyone uses — the one `createIndex` lives in a script documented as stale) and `agenda-buddy-end` (the limiter collapses to one bucket behind a proxy that does not forward the client address). Also fixed in passing: a latent order dependency in F-016's `TelemetryPiiTest` that F-021's new test class exposed | Review | identity-hardening |
