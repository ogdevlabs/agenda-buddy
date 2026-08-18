# State
<!-- pdlc-template-version: 3.0.0 -->
<!-- This file is the live operational state of the PDLC workflow.
     It is written by PDLC hooks and commands — do not edit manually unless recovering from an error.
     Claude reads this file at the start of every session to auto-resume from the last checkpoint.
     If this file is missing or empty, PDLC will prompt you to run /pdlc init. -->

**Last updated:** 2026-08-18T00:30:00Z

---

## Current Phase

Construction

---

## Current Feature

aspire-wiring

---

## Active Task
<!-- The task currently claimed by Claude, from the git-native task store.
     Format: [task-id] — [task title]
     Example: F-002-T03 — Add OAuth2 login with GitHub
     Set to "none" when no task is active. -->

none

---

## Roadmap Claim

- **Feature ID:** F-013
- **Feature record:** docs/pdlc/tasks/F-013/_feature.md
- **Claimed by:** oscargarcia@ogdevlabs.onmicrosoft.com
- **Claimed at:** 2026-08-15T16:45:00Z
- **Branch:** feat/F-013-aspire-wiring

---

## Night Shift

_None active. Run `/night-shift <F-NNN>` to start an autonomous run (requires bypass-permissions mode)._

---

## Current Sub-phase

Wrap-up

---

## Last Checkpoint

Construction / Wrap-up / 2026-08-18T00:30:00Z

---

## Party Mode

agent-teams

---

## Active Blockers

- **⚠️ OPERATIONAL, HIGHEST RESIDUAL SEVERITY — rotate the `agenda_buddy` Atlas credential and review the cluster access log.** F-013 removed the credential from all tracked files, which does **not** remediate the disclosure: it remains in git history and stays valid until rotated at Atlas. Threat T-001 / PRD OQ-1. Merging F-013 does not close this. *(Raised by Phantom at Party Review — it was documented in four places but absent from the one list a handoff reader scans first.)*
- **F-013-T14 / ISSUE-001 — RESOLVED 2026-08-18.** The AppHost now starts all 7 services; AC-1.1, AC-1.2, AC-1.3, AC-2.3 and AC-3.4 are verified in `docs/pdlc/design/aspire-wiring/verification.md`. Root cause was a missing `AgendaBuddy.AppHost/Properties/launchSettings.json`: without `DOTNET_ENVIRONMENT=Development` the AppHost ran as `Production`, user secrets never loaded, every secret parameter went `ValueMissing`, and all seven services parked in `Waiting` with nothing logged. Both "blockers" in the original report were misdiagnoses — `AddProject<TProject>` was never at fault. A second defect surfaced once services could start: `WithReference(database)` injects `ConnectionStrings__agenda-buddy`, not the `ConnectionStrings:mongodb` that `MongoConnectionResolver` reads, which crashed `profession` on startup. Both fixed, +8 regression tests, 294 passing.
- **Three visual checks remain before /ship is fully attested** (a human at the dashboard, ~2 minutes): AC-3.4's traces/metrics/logs rendering for all 7, threat **T-004**'s span inspection (`http.route` templates, not raw paths carrying an email), and review finding **A-3** (JWT parameters masked). Traffic including an email-bearing path has already been generated.

---

## Context Checkpoint

```json
{
  "triggered_at": "2026-08-18T00:30:00Z",
  "active_task": null,
  "sub_phase": "Wrap-up",
  "step": "issue-001-fixed-ready-to-ship",
  "skill_file": "skills/build/steps/05-wrap-up.md",
  "work_in_progress": "F-013 aspire-wiring — ISSUE-001 fixed and verified end-to-end. 294 tests passing, 0 warnings. The fix is UNCOMMITTED in the working tree.",
  "next_action": "Review and commit the ISSUE-001 fix (launchSettings.json, AppHostWiring.cs, AppHostWiringTest.cs, README, docs), do the 3 dashboard visual checks, then run /ship.",
  "files_open": []
}
```

---

## Handoff

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
