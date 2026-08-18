# State
<!-- pdlc-template-version: 3.0.0 -->
<!-- This file is the live operational state of the PDLC workflow.
     It is written by PDLC hooks and commands — do not edit manually unless recovering from an error.
     Claude reads this file at the start of every session to auto-resume from the last checkpoint.
     If this file is missing or empty, PDLC will prompt you to run /pdlc init. -->

**Last updated:** 2026-08-18T13:40:00Z

---

## Current Phase

Inception

---

## Current Feature

api-refactor-foundations

---

## Active Task
<!-- The task currently claimed by Claude, from the git-native task store.
     Format: [task-id] — [task title]
     Example: F-002-T03 — Add OAuth2 login with GitHub
     Set to "none" when no task is active. -->

none

---

## Roadmap Claim

- **Feature ID:** F-018
- **Feature record:** docs/pdlc/tasks/F-018/_feature.md (retitled `api-refactor-foundations` at Discover — stage 1 of 3)
- **Claimed by:** oscargarcia@ogdevlabs.onmicrosoft.com
- **Claimed at:** 2026-08-18T13:40:00Z
- **Branch:** (will be set at build pre-flight)

_F-013 `aspire-wiring` shipped as `v0.1.0` on 2026-08-18 and its claim was released._

---

## Night Shift

_None active. Run `/night-shift <F-NNN>` to start an autonomous run (requires bypass-permissions mode)._

---

## Current Sub-phase

Discover

---

## Last Checkpoint

Inception / Discover / 2026-08-18T13:40:00Z

---

## Party Mode

agent-teams

---

## Guardrail Log

| Timestamp | Guardrail | Detail |
|-----------|-----------|--------|
| 2026-08-18T12:44:29Z | ship_phase_mismatch | `/ship` started with Current Phase `Construction` (sub-phase Wrap-up), not `Construction Complete`. User confirmed: F-013's branch is merged to main and 14/14 tasks are done; the phase marker was never advanced after the ISSUE-001 fix. Bookkeeping gap, not unfinished work. |
| 2026-08-18T12:44:29Z | required_gate_unmet | CONSTITUTION §7 `Security scan (dependency audit + secret scan)` is marked always-required and un-uncheckable but is not implemented — CI has a single credential grep, not a scanner. Pre-existing project-wide gap, not introduced by F-013; owned by F-017. User authorized shipping with the gate unmet. Unit-test gate verified empirically: 305 passing / 0 failing / 0 warnings across 12 projects. |

---

## Active Blockers
<!-- PENDING MARKER — read this first at the start of the next session. Each item below is either
     an action only a human can take, or work that is written but not yet exercised. Nothing here is
     blocked on more code being written. -->

### 1. ⚠️ Rotate the `agenda_buddy` Atlas credential — highest residual risk, human-only

**→ `docs/issues/ISSUE-002-atlas-credential-rotation.md`** (tracker: `agenda-buddy-41s`)

A connection string with **full read/write** access to the cluster was committed to 17 tracked files.
F-013 removed it from the working tree; it **remains in git history and remains valid until the
password is changed at Atlas**. The cluster holds client names, email addresses, phone numbers and
appointment records — who met which therapist or coach and when. That makes an unrotated credential a
notifiable personal-data breach with a 72-hour GDPR clock, an unlogged data-modification risk with **no
backups to restore from**, and the first prerequisite for any cloud deployment. Documenting it again is
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

### 4. Roadmap ordering note (not a blocker, but read it)

**F-018 `refactor-minimal-apis` is claimed and being worked ahead of F-014–F-017** at the user's
explicit request. That's a deliberate call — a structural refactor of every endpoint is cheaper
before those four features add more endpoints to it — but it means three known-bad conditions stay
open longer than the roadmap order implies: six shipped-but-unreachable capabilities (F-014), a
mobile client that cannot reach the backend (F-015), and unauthenticated PII exposure on
`GET /api/v1/providers` (F-016).

### Resolved, kept for context

- **F-013 SHIPPED as `v0.1.0` on 2026-08-18.** Tagged at `c86bca9` and pushed — the first PDLC-tracked release in a repo that had zero tags despite 13 features marked Shipped. Episode 001 committed. **Deploy deliberately skipped**, with reasons recorded in `DEPLOYMENTS.md` rather than silently omitted. `CONSTITUTION` §7's security scan was run **by hand** at the ship gate: **0 vulnerable packages** across all 25 projects, working tree clean, and 9 commits confirmed to still carry the credential in history. That was greps, not a scanner — it does **not** discharge the gate; F-017 still owns automating it.
- **69 whitespace findings fixed 2026-08-18.** `dotnet format` across `agenda-buddy-backend.slnf`, committed as a separate `style:` commit *after* the v0.1.0 tag. 305 tests pass before and after. **The repo still has no `.editorconfig`, so this drift will return** — adopting one is worth folding into F-018.
- **F-013-T14 / ISSUE-001 — RESOLVED 2026-08-18, merged in PR #35.** The AppHost now starts all 7 services. Root cause was a missing `AgendaBuddy.AppHost/Properties/launchSettings.json`: without `DOTNET_ENVIRONMENT=Development` the AppHost ran as `Production`, user secrets never loaded, every secret parameter went `ValueMissing`, and all seven services parked in `Waiting` with nothing logged. Both "blockers" in the original report were misdiagnoses — `AddProject<TProject>` was never at fault. A second defect surfaced once services could start: `WithReference(database)` injects `ConnectionStrings__agenda-buddy`, not the `ConnectionStrings:mongodb` that `MongoConnectionResolver` reads, which crashed `profession` on startup.
- **`agenda-buddy-prr` — RESOLVED 2026-08-18.** `MobileApp` did not compile under `/p:MobileWorkloads=false` (`CS0103 'Application'`), which had been failing the `build-mobile-tests` job outright — all 67 MobileApp tests had never run in CI. Guarded with the existing `MOBILE` constant.
- **CI guard that never ran — RESOLVED 2026-08-18.** `Assert every service starts in Development` consumed `secrets.CI_JWT_*`, which were never created. It was added by F-013 and CI only triggers on push to `main` or a PR to `main`, so it first executed — and first failed — on PR #35. It now generates a throwaway keypair in-step.

---

## Context Checkpoint

```json
{
  "triggered_at": "2026-08-18T13:40:00Z",
  "active_task": null,
  "sub_phase": "Discover",
  "step": "f-013-shipped-v0.1.0-starting-f-018-inception",
  "skill_file": "skills/brainstorm/steps/01-discover.md",
  "work_in_progress": "F-013 shipped as v0.1.0. F-018 Inception in Discover. Round 1 done: the requested scope was too large for one PRD and was decomposed into F-018 api-refactor-foundations (harness + MobileApp CI + Persitency rename + constitution amendments), F-019 api-refactor-pilot-booking (full Clean Architecture on Booking), F-020 api-refactor-rollout (remaining 6). Integration-test capability moved out of F-017 into F-018. MediatR stays the single dispatcher; SmallApiToolkit used only for DataResponse/validation-base/middleware.",
  "next_action": "Continue F-018 Discover at Round 2 (Future State / Key Capabilities), scoped to the foundations stage only: Testcontainers harness, MobileApp into CI, Persitency rename, constitution amendments.",
  "files_open": []
}
```

---

## Handoff

```json
{
  "phase_completed": "Operation / Ship",
  "next_phase": "Operation / Verify",
  "feature": "aspire-wiring",
  "feature_id": "F-013",
  "version": "v0.1.0",
  "key_outputs": [
    "docs/pdlc/memory/CHANGELOG.md",
    "docs/pdlc/memory/DEPLOYMENTS.md",
    "docs/pdlc/episodes/EPISODE_aspire-wiring_2026-08-17.md"
  ],
  "decisions_made": [
    "Tagged v0.1.0 at c86bca9 and pushed — the repo had zero tags despite 13 features marked Shipped, so Step 6's initial-tag rule applied",
    "Did NOT run scripts/ship-merge.sh — it hardcodes 'feature/<name>' but the branch is 'feat/F-013-aspire-wiring', and there was nothing to merge (PR #35 already merged it)",
    "Two '!' breaking commits (c6b70b6 PII span redaction, d00b87f credential removal) recorded under Breaking Changes rather than forcing v1.0.0, since this is a pre-1.0 project",
    "Applied dotnet format across agenda-buddy-backend.slnf — 69 pre-existing WHITESPACE findings, committed as a separate 'style:' commit AFTER the v0.1.0 tag; 305 tests pass before and after",
    "Left the v0.1.0 tag at c86bca9 rather than force-updating an already-pushed tag to include the style commit",
    "DEPLOY SKIPPED — recorded as skipped-with-reason in DEPLOYMENTS.md, not as a deployment",
    "Registered 'cloud' (Azure) in DEPLOYMENTS.md as a known-but-never-deployed environment so the unexercised capability is visible"
  ],
  "deploy_skipped_because": [
    "The unrotated agenda_buddy Atlas credential is a hard prerequisite — still in git history, still valid (ISSUE-002 / agenda-buddy-41s)",
    "No Azure subscription is wired to this machine",
    "The first azd up must be interactive to discover parameter names before AZD_ENV_VARS can be set"
  ],
  "next_action": "Verify sub-phase. There is no deployment to smoke-test; the only outstanding verification is the 3 local dashboard visual checks (agenda-buddy-e7e).",
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
