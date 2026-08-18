# State
<!-- pdlc-template-version: 3.0.0 -->
<!-- This file is the live operational state of the PDLC workflow.
     It is written by PDLC hooks and commands — do not edit manually unless recovering from an error.
     Claude reads this file at the start of every session to auto-resume from the last checkpoint.
     If this file is missing or empty, PDLC will prompt you to run /pdlc init. -->

**Last updated:** 2026-08-18T20:38:00Z

---

## Current Phase

Construction

---

## Current Feature

secure-public-endpoints

_In **Construction / Build**. Inception complete 2026-08-18 — PRD, design and plan all approved. Came from the approved **Platform Remediation** program Discover, which re-scoped and re-sequenced F-014–F-017 into six features. Program log: `docs/pdlc/brainstorm/brainstorm_platform-remediation_2026-08-18.md` (`status: prd-approved`). New sequence: **F-016 → F-021 → F-014 → F-015 → F-017 → F-018–F-020**, with F-022–F-024 filed._

_`api-refactor-foundations` (F-018) is **paused** — see `docs/pdlc/memory/.paused-feature.json`. Inception is complete and merged; Construction was aborted at the wave-1 standup before any code._

---

## Active Task
<!-- The task currently claimed by Claude, from the git-native task store.
     Format: [task-id] — [task title]
     Example: F-002-T03 — Add OAuth2 login with GitHub
     Set to "none" when no task is active. -->

none

---

## Roadmap Claim

- **Feature ID:** F-016
- **Feature record:** docs/pdlc/tasks/F-016/_feature.md
- **Claimed by:** oscargarcia@ogdevlabs.onmicrosoft.com
- **Claimed at:** 2026-08-18T17:52:00Z *(claim moved F-014 → F-016 when Discover put F-016 first; F-014 released)*
- **Branch:** `feat/F-016-secure-public-endpoints` — created at build pre-flight 2026-08-18T19:22Z **off freshly-pulled `main`** (`e35938b`) at the maintainer's request, not off the F-018 branch. The Inception artifacts and the F-018 abort travelled across via stash.
- **Claim commit:** ⚠️ **not committed or pushed** — no authorization given. Single-dev repo, so there is no claim race to lose; the push is bookkeeping, not a lock.

_F-018's claim was released on 2026-08-18T17:37:14Z when Construction was aborted; the feature is `In Progress`-but-unclaimed on the roadmap and resumable with `/continue`. Its branch `feat/F-018-api-refactor-foundations` holds the Inception artifacts and **zero** implementation commits._

_F-013 `aspire-wiring` shipped as `v0.1.0` on 2026-08-18 and its claim was released._

---

## Night Shift

_None active. Run `/night-shift <F-NNN>` to start an autonomous run (requires bypass-permissions mode)._

---

## Current Sub-phase

Build

---

## Last Checkpoint

Construction / Build / 2026-08-18T20:38:00Z — **waves 1–5 complete: the verification harness is done.** Next: **wave 6**, the production-behaviour wave (10 tasks; T08, T09, T18 ready now). A wave-6 standup has not been held.

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

---

## Active Blockers

> ### 🔖 RESUME MARKER — updated 2026-08-18T20:38Z, harness complete
>
> **F-016 `secure-public-endpoints` is in Construction / Build, 8 of 20 tasks done, on branch
> `feat/F-016-secure-public-endpoints` (13 commits, nothing pushed).**
>
> **Waves 1–5 are complete — the entire verification harness works.** T01, T02, T03, T04, T05, T06,
> T07 and T10. The feature's central claim is now demonstrated rather than asserted: a real service is
> hosted over HTTP against a MongoDB Testcontainer, and `AuthFailurePathTest` proves 401-on-expired and
> **403-on-foreign-subject** against a real route.
>
> **Next is wave 6 — the production-behaviour wave, 10 tasks across six services.** Ready now:
> **T08** (central 403 — gates every other endpoint task), **T09** (`AssertOwner` null-claim fix —
> must precede T11 and T13), **T18** (audit payloads + `Event.actor`). **Hold a wave-6 standup first:**
> the plan flags internal ordering that the dependency graph only partly encodes.
>
> Everything in `## Context Checkpoint` still applies. What the harness now gives you:
>
> - `[Collection(HarnessCollection.Name)]` on every harness test class — shares the session keypair and
>   serialises the `JWT_PUBLIC_KEY` race. Also `IClassFixture<ServiceHostFixture<XAnchor>>` for a service.
> - `ServiceHostFixture<TEntryPoint>.StartService()` → a `ServiceHost` with `.Client` (real HTTP through
>   the full pipeline), `.Database` (this test's own DB) and `.DatabaseName`. Container per class,
>   database per test.
> - `new TokenFactory(crypto)` → `CreateToken(subject, role)`, `CreateExpiredToken(...)`,
>   `CreateTokenWithoutSubject()` (the T-001 probe).
> - Anchor aliases live in `GlobalUsings.cs` — `ProfessionAnchor`, `CustomerAnchor`. **Add one per
>   service as you need it**; the type is that service's public `MongoDbConfiguration`.
> - Two traps already paid for: `MiniValidator` runs **before** `AssertOwner` on the `{email}` PUT
>   routes, so a test with an invalid body gets 400 and never reaches the guard. And **never write
>   `ConnectionStrings__mongodb`** — it poisons the next test class and defeats the fail-closed guard.
>
> Fastest path back in: `/build`. **Filter the ready queue on `epic:secure-public-endpoints`** — it is
> not feature-scoped and will otherwise hand you a paused F-018 task.

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

### 4. Roadmap ordering — **REVERSED, then decomposed 2026-08-18. New order: F-016 → F-021 → F-014 → F-015 → F-017**

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
- **F-016** — **unauthenticated PII exposure.** `GET /api/v1/providers` returns every provider's full
  record including customer emails, anonymous and unpaginated. Both Calendar routes are IDOR-able and
  `OwnershipGuard.AssertRole` is never actually called. **This is the highest-severity item in the
  four and is worth considering ahead of F-014.**
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

<!-- ⚠️ CONTEXT CLEARED HERE at the maintainer's request, 2026-08-18T19:55Z.
     This block is written to be read COLD — a fresh session should be able to resume
     F-016 Construction from it alone, without any of the prior conversation. -->

```json
{
  "triggered_at": "2026-08-18T19:55:00Z",
  "reason": "maintainer asked for a marker so context could be cleared mid-Build",
  "phase": "Construction",
  "sub_phase": "Build",
  "feature": "secure-public-endpoints",
  "feature_id": "F-016",
  "active_task": null,
  "skill_file": "skills/build/steps/02-build-loop.md",
  "resume_command": "/build",

  "branch": "feat/F-016-secure-public-endpoints",
  "branch_base": "main @ e35938b (freshly pulled; branched at the maintainer's request, NOT off the F-018 branch)",
  "commits_on_branch": 13,
  "pushed": false,
  "working_tree": "clean apart from PDLC docs (STATE, the wave-3 MOM, task files)",

  "progress": "8 of 20 tasks done — WAVES 1-5 COMPLETE, the whole verification harness. T01 rename, T02 project + InternalsVisibleTo x7, T03 CryptoSessionFixture + AC-3 hygiene, T04 DockerPreflight, T05 TokenFactory, T06 ServiceHostFixture + fail-closed guard, T07 401/403 over real HTTP, T10 GetPagedAsync.",
  "ready_queue_next": ["F-016-T08 central 403 (gates every endpoint task)", "F-016-T09 AssertOwner null fix (MUST precede T11 and T13)", "F-016-T18 audit metadata + Event.actor"],
  "critical_path": "T01 -> T02 -> T03 -> T06 -> T07 -> T08 -> T12 -> T15 -> T19 -> T20 (10 deep). SIX of ten are done. Both named bottlenecks (T02, T06) are cleared. Remaining: T08 -> T12 -> T15 -> T19 -> T20.",

  "test_state": {
    "backend": "322 passing / 0 failing / 0 warnings across 12 projects via `dotnet test agenda-buddy-backend.slnf`. 305 baseline -> 309 (T01) -> 313 (T03 hygiene x4) -> 322 (T10: +3 contract, +6 semantics). Waves 4-5 added nothing here; they are all integration tests.",
    "integration": "45 passing in 18 s via `dotnet test AgendaBuddy.IntegrationTests/AgendaBuddy.IntegrationTests.csproj` — a SEPARATE command, see ADR-031. 9 (T02) + 4 (T03) + 10 (T04) + 4 (T05) + 14 (T06) + 4 (T07). The 18 s figure is a datum for T20 duration enforcement.",
    "mobile": "74 (67 passing, 7 skipped) via `dotnet test MobileApp.Tests/MobileApp.Tests.csproj /p:MobileWorkloads=false` — re-verified after T10 touched Library, unchanged"
  },

  "WAVE_3_ESTABLISHED": {
    "HarnessCollection": "Harness/CryptoSessionFixture.cs. Every harness test class must join it: [Collection(HarnessCollection.Name)]. DisableParallelization = true for the JWT_PUBLIC_KEY startup race, and it shares the one CryptoSessionFixture. NOTE: Identity.Tests/Auth/TestCollectionDefinition.cs could NOT be reused — xUnit collection definitions are per-assembly. The pattern transfers, the type does not.",
    "DockerPreflight": "Harness/DockerPreflight.cs. Call EnsureAvailable() before ANY container start; ContainerRuntimeGuardTest already does. Resolution order matches Testcontainers: DOCKER_HOST -> docker.host in ~/.testcontainers.properties -> current docker context -> default socket. It NEVER blocks on uncertainty (tcp:// reports available) because a false positive would stop a working suite.",
    "CryptoSessionFixture": "Exposes PublicKeyPem and a live RSA SigningKey. Deliberately NO private-key PEM string — a string is what gets logged or pasted into a fixture. T05 signs with SigningKey; T06 exports PublicKeyPem as JWT_PUBLIC_KEY.",
    "GetPagedAsync": "Task<(IEnumerable<TEntity> Items, long TotalCount)> GetPagedAsync(int skip, int take) on IRepository<T>. Negatives normalised to 0 in BOTH implementers, because Skip(-1) throws on the driver but is a no-op in LINQ. Clamping the caller's pageSize is T15's job, not the repository's (ADR-023).",
    "measured_docker_facts": "No /var/run/docker.sock on this machine. ~/.docker/config.json currentContext = rancher-desktop -> unix:///Users/<user>/.rd/docker.sock. Testcontainers.NET does NOT shell out to the docker CLI; it uses the engine API over that socket. Both halves of T04's stated premise were wrong and are corrected in DockerPreflight's remarks."
  },

  "WAVE_4_5_ESTABLISHED": {
    "how_to_write_an_endpoint_test": "[Collection(HarnessCollection.Name)] on the class, plus IClassFixture<ServiceHostFixture<XAnchor>>. Constructor takes (ServiceHostFixture<XAnchor> host, CryptoSessionFixture crypto). Then: using var service = host.StartService(); service.Client for real HTTP, service.Database for this test's own DB. new TokenFactory(crypto) for tokens. See AuthFailurePathTest for the full pattern.",
    "anchors": "GlobalUsings.cs holds the per-service aliases (ProfessionAnchor, CustomerAnchor). ADD ONE PER SERVICE as needed: the type is that service's public MongoDbConfiguration. Booking's namespace is Booking.Configuration (SINGULAR); the other six are *.Configurations (plural).",
    "xunit_mechanics_VERIFIED": "xUnit v2 DOES inject a collection fixture into a class fixture's constructor — verified empirically with a throwaway probe, contrary to expectation. That is what lets the session-scoped keypair and the class-scoped container compose with no process-static workaround. The class fixture type must be PUBLIC (CS0051).",
    "fail_closed_guard": "MongoEndpointGuard, two layers. AssertNotObviouslyRemote (srv:// or credentials) runs BEFORE the container starts, so an Atlas string aborts without pulling a 1.13 GB image. AssertTargetsContainer compares HOST AND PORT against the container's own reported endpoint — NOT a localhost pattern, which was broken at the threat party. It never echoes the rejected string, so a credential cannot reach CI logs.",
    "never_write_the_connection_string": "ServiceHostFixture treats the environment as READ-ONLY and injects the container endpoint via WebApplicationFactory UseSetting. Writing ConnectionStrings__mongodb would (a) make the guard compare its own value to itself and (b) poison the NEXT test class, which starts a different container on a different port and would read the previous class's endpoint as a conflict and abort. JWT_PUBLIC_KEY is the exception and MUST be an env var: AuthenticationExtensions.cs:16 reads it directly, not through IConfiguration, and throws at DI-registration time.",
    "validation_precedes_authorization": "On the {email} PUT routes MiniValidator.TryValidate runs BEFORE OwnershipGuard.AssertOwner (Customer/Program.cs:150 vs :153). A test with an invalid or empty body gets 400 and NEVER REACHES THE GUARD — it reads as 'the guard does not fire'. Also a mild information-disclosure smell for F-019/F-021: an unauthorized caller can probe validation rules.",
    "what_AC4_real_HTTP_means": "The HttpClient issues real HTTP through the service's whole pipeline — routing, authn, authz, model binding, exception handler — against real MongoDB. The transport underneath is TestServer's in-memory one, not TCP. That is what Microsoft.AspNetCore.Mvc.Testing provides and what T02's EntryPoints design selected. Stated because AC-4 says 'real HTTP request'."
  },

  "WAVE_3_DEBT": [
    "MongoDbRepository<T>.GetPagedAsync has NO test of its Mongo semantics — not unit-testable (live DB + the driver's fluent chain ends in an extension method Moq cannot intercept). First real exercise is T15's paginated endpoint tests. T19's attestation must not claim otherwise. Standup finding E-1.",
    "AC-3's tree-hygiene tests live in Library.Tests, NOT the harness, so the existing api CI job runs them on every PR — the harness is out of the slnf (ADR-031) and its CI job does not exist until T20. Consider moving them once T20 is green.",
    "DockerPreflight residual: if the socket exists and something is listening but the daemon is wedged, the preflight passes and Testcontainers' own error surfaces. Detecting that needs a real API call with its own timeout budget."
  ],

  "READ_FIRST_ON_RESUME": [
    "docs/pdlc/prds/plans/plan_F-016_secure-public-endpoints_2026-08-18.md — the 10 known gaps section names everything that will surprise the implementer",
    "docs/pdlc/design/secure-public-endpoints/threat-model.md — 7 mitigate-now threats are [security] ACs on T06/T08/T09/T13/T16/T17/T18",
    "docs/pdlc/design/secure-public-endpoints/ARCHITECTURE.md — AD-1 (section 2) explains why requirement 14 could not be met as scoped",
    "AgendaBuddy.IntegrationTests/Harness/EntryPoints.cs — why WebApplicationFactory<Program> cannot be used here"
  ],

  "GOTCHAS_THAT_WILL_BITE": [
    "`tasks.cjs ready` is NOT feature-scoped. It returns paused-F-018 tasks (F-018-T02/T04/T19) alongside F-016's. ALWAYS filter on label epic:secure-public-endpoints or Build will start an F-018 task.",
    "docker is NOT on PATH under Rancher Desktop. `export PATH=\"$HOME/.rd/bin:$PATH\"` before anything that touches containers.",
    "Do NOT add AgendaBuddy.IntegrationTests to agenda-buddy-backend.slnf (ADR-031). That slnf is the Docker-free unit gate documented in CLAUDE.md and run by CI's api job.",
    "WebApplicationFactory<Program> does NOT compile: 7 assemblies each emit an internal Program in the global namespace. Use Harness/EntryPoints.cs, which anchors each service to a distinct public type. Note Booking's namespace is Booking.Configuration (singular) while the other six are *.Configurations (plural).",
    "`tasks.cjs ac list <task> --json` reports tag=None threat=None even when the tags ARE persisted. Read the raw task file (acceptance_criteria: [\"AC1|security|T-002|...\"]) or use `tasks.cjs check`. This produced a false security-ac-unmaterialized reading at the readiness party.",
    "T09 (AssertOwner null-claim fix) MUST precede T11 (ProviderSummary projection). The projection selects owner-vs-non-owner with AssertOwner, whose null-claim pass lands on the OWNER branch and returns the unprojected entity. Building T11 first ships the bypass. The dep edge exists — do not optimise it away.",
    "T13's cache test must assert 'NOT 200-with-data', not 'exactly 403'. CacheAside has no test and returns default! on a 500ms lock timeout, surfacing as a spurious 404.",
    "F-016-T20 (integration CI job) CANNOT be completed without the maintainer: main is PR-protected and CI is path-filtered, so it needs a throwaway branch PUSHED BY A HUMAN. The task graph cannot express that.",
    "Party mode is `solo` for this whole feature — the session carried a standing 'do not call the Agent tool unless requested' instruction that overrides STATE's `Party Mode: agent-teams`. Every MOM records this."
  ],

  "MEASURED_FACTS": {
    "container_start_warm": "3 s — BETTER than the 4.45 s F-018's spike measured, so ADR-017's container-per-class decision holds with margin",
    "container_start_cold": "62 s, dominated by the 1.13 GB mongo:7.0 pull. A CI consideration for T20 — image caching is worth ~1 min per cold runner.",
    "rename_scope": "11 .cs files, one reference each; zero in any .json/.yml/.csproj/.slnf. Matched the F-018 Discover measurement exactly.",
    "host": "Rancher Desktop, docker 29.5.3, 2 CPUs / 4.1 GB, k8s already running (11 containers)"
  },

  "OPEN_DECISION_THE_MAINTAINER_MAY_REVISIT": "ADR-030 — SSH.NET GHSA-q939-rpr3-3284 (HIGH) enters via Testcontainers and has NO patched version (2023.0.0 through 2025.0.0 all flagged; pinning was attempted and cannot fix it). Accepted as unreachable, and the unreachability is TESTED by Harness/ContainerRuntimeGuardTest.cs. NU1903 is suppressed in that project only; the vulnerability report still lists it, so F-017's audit gate is unaffected. This was surfaced to the maintainer for possible reversal and they moved on to clearing context, so IT STANDS AS ACCEPTED — but it was explicitly flagged as a call a different maintainer could reasonably make differently. CONSTITUTION section 7's dependency-audit gate is unimplemented, which is what makes this uncomfortable.",

  "files_open": []
}
```

---

## Handoff

```json
{
  "phase_completed": "Inception (all four sub-phases) + Construction waves 1-2",
  "next_phase": "Construction / Build — wave 3",
  "feature": "secure-public-endpoints",
  "feature_id": "F-016",
  "branch": "feat/F-016-secure-public-endpoints",
  "branch_pushed": false,
  "commits": 5,
  "tests": "backend 309 passing / 0 failing / 0 warnings (12 projects); integration 9 passing; mobile 74 untouched",
  "task_status": "2 of 20 done — T01, T02. Next ready: T03, T04, T10.",

  "key_outputs": [
    "docs/pdlc/prds/PRD_F-016_secure-public-endpoints_2026-08-18.md (Approved, 26 ACs)",
    "docs/pdlc/design/secure-public-endpoints/ — ARCHITECTURE, data-model, api-contracts, threat-model (Full/Approved), ux-review (Skip)",
    "docs/pdlc/prds/plans/plan_F-016_secure-public-endpoints_2026-08-18.md (20 tasks / 8 waves)",
    "docs/pdlc/brainstorm/brainstorm_platform-remediation_2026-08-18.md (inception-complete)",
    "docs/pdlc/mom/ — threat-model, readiness-party, and the F-018 wave-1 standup",
    "DECISIONS.md ADR-022..ADR-031",
    "AgendaBuddy.IntegrationTests/ — the harness project, Harness/EntryPoints.cs, ContainerRuntimeGuardTest, InternalsVisibleToTest",
    "EventsAndCommands.Tests/Persistence/PersistenceNamespaceTest.cs"
  ],

  "decisions_made": [
    "F-018 Construction ABORTED before any code; claim released; paused (docs/pdlc/memory/.paused-feature.json). F-018 now ~12 tasks — F-016 absorbed EIGHT (T01,T05,T06,T07,T08,T09,T14,T18). DO NOT rebuild a harness that exists.",
    "F-014-F-017 decomposed into SIX features. Order: F-016 -> F-021 -> F-014 -> F-015 -> F-017 -> F-018-F-020. F-022/023/024 filed.",
    "ADR-022: shared IExceptionHandler registered OUTSIDE the IsDevelopment() guard — requirement 14 could NOT be met as scoped because the existing handler is Development-only in all 7 services.",
    "ADR-023: paginated contract — page/pageSize, MaxPageSize 100, CLAMP not reject, envelope {items,totalCount,page,pageSize}, 204 retired. F-015 consumes this.",
    "ADR-025: POST /api/v1/professions DELETED, not role-gated — there is no admin role. Supersedes requirement 13.",
    "ADR-026: GET /api/v1/customers requires the Provider role. Scope addition.",
    "ADR-027: Event gains a nullable actor field — F-016 is no longer schema-change-free.",
    "ADR-030: SSH.NET HIGH CVE accepted as unreachable, and the unreachability is TESTED. See the Context Checkpoint's OPEN_DECISION field.",
    "ADR-031: integration project excluded from agenda-buddy-backend.slnf, per the MobileApp precedent, so the unit gate stays Docker-free."
  ],

  "next_action": "Run /build. It will resume at Build Step 4. Filter the ready queue on label epic:secure-public-endpoints (see the Context Checkpoint gotcha). Wave 3 is T03 CryptoSessionFixture, T04 DockerPreflight, T10 GetPagedAsync.",

  "do_not_redo": [
    "Do not re-run the Testcontainers feasibility spike: proven on this machine 2026-08-18. 3 s warm, 62 s cold.",
    "Do not try WebApplicationFactory<Program>: ambiguous across 7 assemblies. Use Harness/EntryPoints.cs.",
    "Do not try to pin SSH.NET to a safe version: every published version through 2025.0.0 is flagged. Attempted and measured.",
    "Do not add the integration project to agenda-buddy-backend.slnf (ADR-031).",
    "Do not rebuild the harness or re-derive its patterns: waves 1-5 are DONE and 45 integration tests pass. Do not try WebApplicationFactory<Program>. Do not assume xUnit v2 cannot inject a collection fixture into a class fixture — it can, verified.",
    "Do not run the Nordstrom standards gate: the plugin is installed but its six source repos do not resolve. Skipped with notice at both Define and Plan; same as F-013 and F-018.",
    "Do not check CONSTITUTION section 7's Integration box: gated on 10 consecutive green runs, tracked separately (F-018 T04, NOT absorbed)."
  ],

  "outstanding_not_closed_by_this_feature": [
    "ROTATE the Atlas credential (ISSUE-002) — human-only, still valid, still recoverable from this PUBLIC repo's history. It is what makes T06's fail-closed guard load-bearing.",
    "F-016-T20 needs a maintainer-pushed throwaway branch to verify.",
    "CONSTITUTION section 7's dependency-audit + secret-scan gate remains unimplemented (F-017). ADR-030 will be its first finding, with expected disposition 'accepted'.",
    "Deferred from ADR-026: owner-scoping GET /api/v1/customers to the caller's own SubscribedCustomerCollection is the stronger fix.",
    "Deferred from ADR-022: nine other exception-to-status mappings. FormatException -> 400 is the best next candidate (most likely live 500)."
  ],

  "pending_questions": []
}
```

_Superseded Construction handoff (F-013), retained because its gotchas and do-not-redo list are still live:_

```json
{
  "phase_completed": "Construction / Build + Review + ISSUE-001 fix",
  "next_phase": "Ship",
  "feature": "aspire-wiring",
  "feature_id": "F-013",
  "branch": "feat/F-013-aspire-wiring",
  "branch_pushed": true,
  "commits": 24,
  "tests": "294 passing, 0 failing, 0 warnings (dotnet test agenda-buddy-backend.slnf)",
  "baseline_before_feature": "189 passing across 10 projects",
  "READ_FIRST": [
    "docs/issues/ISSUE-001-apphost-never-launches-services.md — the blocker, with the full resolution path",
    "docs/pdlc/design/aspire-wiring/verification.md — which acceptance criteria are verified vs unverified",
    "docs/pdlc/reviews/REVIEW_aspire-wiring_2026-08-17.md — findings, incl. the Critical Echo caught late",
    "docs/pdlc/episodes/EPISODE_aspire-wiring_2026-08-17.md — what the plan got wrong and why"
  ],
  "task_status": "14 of 14 done. F-013-T14 closed 2026-08-18.",
  "next_action": "Commit the ISSUE-001 fix (uncommitted), do the 3 dashboard visual checks, then /ship.",
  "do_not_redo": [
    "Do not re-run the T-01 spike: R-1 is settled. Aspire.MongoDB.Driver is excluded, driver pinned at 2.25.0, Aspire 13.4.6 hosting-only, no workload exists.",
    "Do not try to run the Nordstrom standards gate (Step 12.6): the six .nordstrom-standards/* repos do not resolve under this gh auth. Needs SSO or VPN.",
    "Do not re-trust the dev certificate: already done, and it did not fix ISSUE-001.",
    "Do not re-investigate ISSUE-001 as an AddProject<TProject> or endpoint-annotation problem: both were disproven. Root cause was the missing launchSettings.json / non-Development environment.",
    "Do not add MobileApp to CI's api job: it does not compile (agenda-buddy-prr). CI targets agenda-buddy-backend.slnf on purpose."
  ],
  "decisions_made": [
    "R-1 escape hatch taken — no Aspire MongoDB client integration; AddSingleton<IMongoClient> + custom MongoHealthCheck",
    "IRequestCollection registered Scoped — a pre-existing captive dependency stopped 6 of 7 services starting in Development",
    "Profession seeding moved from DI-registration-time .Wait() to a hosted service",
    "PiiRedactingProcessor added — url.path was exporting email addresses (threat T-004 was real, not theoretical)",
    "Dead IMongoDbConfiguration registrations deleted (review I-3)",
    "Atlas credential removed from 17 tracked files — removal is NOT remediation"
  ],
  "outstanding_not_closed_by_merge": [
    "⚠️ ROTATE the agenda_buddy Atlas credential and review the cluster access log — still in git history, still valid (threat T-001 / OQ-1)",
    "3 dashboard visual checks: AC-3.4 rendering, threat T-004 span inspection, review finding A-3 JWT masking",
    "CONSTITUTION §7 dependency-audit + secret-scan gate still unimplemented — deferred to F-017",
    "agenda-buddy-prr — MobileApp CS0103; also breaks the build-mobile-tests CI job",
    "Echo's 2 advisory test gaps: the guarded legacy MongoDbConfiguration ctor throw, and ProfessionSeedHostedService.StartAsync",
    "scripts/seed/seed-mongo.sh is stale — hardcodes mongo:27017 and targets databases no service reads"
  ],
  "environment_gotchas": [
    "Rancher Desktop: docker lives at ~/.rd/bin and is NOT on PATH. Aspire shells out to docker — export PATH=\"$HOME/.rd/bin:$PATH\" first.",
    "Rancher VM is 2 CPUs / 4.1 GB and already runs a k8s cluster. Mongo + Kafka + 7 services is tight.",
    "AppHost secrets are in user secrets and ONLY load in Development: Parameters:jwt-public-key, Parameters:jwt-private-key, Parameters:mongodb-password. AgendaBuddy.AppHost/Properties/launchSettings.json sets DOTNET_ENVIRONMENT=Development — deleting it silently breaks the whole graph (ISSUE-001).",
    "MongoDB runs on a persistent volume, so its password must stay stable. If auth ever breaks: docker volume rm agendabuddy.apphost-<hash>-mongodb-data.",
    "Debug the app model with Logging__LogLevel__Aspire=Debug — resource state transitions and parameter ValueMissing states are only logged at Debug.",
    "Services run standalone with --no-launch-profile, else launchSettings forces Development and overrides ASPNETCORE_ENVIRONMENT.",
    "macOS has no `timeout`; use background + sleep + kill."
  ],
  "pending_questions": []
}
```

_Superseded handoff (F-012 mobile-app, shipped) retained for reference:_

```json
{
  "phase_completed": "Construction / Build",
  "next_phase": "Ship",
  "feature": "mobile-app",
  "branch": "feature/mobile-app",
  "key_outputs": [
    "MobileApp/MobileApp.csproj",
    "MobileApp/MauiProgram.cs",
    "MobileApp/AppShell.xaml",
    "MobileApp/Infrastructure/JwtDelegatingHandler.cs",
    "MobileApp/Infrastructure/ISecureStorageService.cs",
    "MobileApp/Services/AuthService.cs",
    "MobileApp/Services/BookingApiService.cs",
    "MobileApp/Services/CalendarApiService.cs",
    "MobileApp/Services/CustomerApiService.cs",
    "MobileApp/Services/MessagingApiService.cs",
    "MobileApp/Services/NotificationApiService.cs",
    "MobileApp/Services/PushNotificationService.cs",
    "MobileApp/ViewModels/LoginViewModel.cs",
    "MobileApp/ViewModels/DashboardViewModel.cs",
    "MobileApp/ViewModels/CalendarViewModel.cs",
    "MobileApp/ViewModels/CustomersViewModel.cs",
    "MobileApp/ViewModels/AppointmentDetailViewModel.cs",
    "MobileApp/ViewModels/MessagingViewModel.cs",
    "MobileApp/ViewModels/MessageThreadViewModel.cs",
    "MobileApp/ViewModels/NotificationsViewModel.cs",
    "Library/Entities/DeviceTokenEntity.cs",
    "Library/Services/DeviceTokenService.cs",
    "Identity/Program.cs (POST /identity/device-token)",
    "Identity.Tests/Security/LoginLogSanitizationTest.cs",
    ".github/workflows/dotnet.yml (Android + iOS CI jobs)"
  ],
  "test_counts": {
    "MobileApp.Tests": 63,
    "Library.Tests": 74
  },
  "decisions_made": [
    "All 14 plan tasks completed across 7 waves",
    "AppointmentStatus enum extended with Confirmed + Cancelled values",
    "Shell navigation: 5 tabs + login non-tab root + appointmentDetail + messageThread stack routes",
    "Cancel/Complete use ActionSheet (bottom sheet) not DisplayAlert (UX F-005 fix)",
    "All error banners include Try again button (UX F-002 fix)",
    "Push payload body is PII-free generic text (T-002 mitigation)",
    "POST /identity/device-token requires JWT auth; no device token logged (CONSTITUTION §4)",
    "MobileWorkloads=false fallback TFM for local dev + CI unit tests"
  ],
  "next_action": "Run /pdlc ship mobile-app to open PR",
  "pending_questions": []
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
