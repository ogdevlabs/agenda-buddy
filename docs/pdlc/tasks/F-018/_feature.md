---
id: F-018
title: api-refactor-foundations
status: shipped
priority: 19
labels: [roadmap, "priority:19"]
claimed_by: null
created: 2026-08-18
updated: 2026-08-26
---
**Stage 1 of 3 in the API refactor program (F-018 → F-019 → F-020).** Build the safety net and clear the mechanical blockers *before* any endpoint is rewritten, because until an integration-test harness exists the only net under a 7-service endpoint rewrite is unit tests — the exact gap episode 001 concluded let two real defects survive review.

Scope:
1. **Testcontainers-based integration-test harness** — real MongoDB (and Kafka where needed) per test run, wired into CI. Makes CONSTITUTION §5's "all integration tests pass" satisfiable for the first time.
2. **`MobileApp` CI — mostly already done; scope corrected 2026-08-18 after checking the workflow.** `agenda-buddy-prr` is closed, `MobileApp.Tests` passes 67 (7 skipped, verified locally), and CI already runs three dedicated mobile jobs: `build-android` (installs `maui-android`), `build-ios` (on `macos-latest`, installs `maui-ios`), and `build-mobile-tests`. The MAUI-workload and macOS-runner concerns raised at Discover were already handled. What actually remains: confirm those three jobs pass on a real run (they are path-filtered, so they may not have executed recently), and decide whether the project's headline test count should be reported as **379** (305 backend + 74 mobile) rather than 305. The stale CI comment claiming MobileApp does not compile has been corrected.
3. **`EventAndCommands/Persitency/` → `Persistence/`** — the long-deferred rename. CONSTITUTION §9 forbade it "until a dedicated refactor is planned"; this program is that refactor, so the condition is met. §9's prohibition is retired in the same change.
4. **Constitution amendments + ADRs** for the program: §1 (still says .NET 8 — it is net10), §4 (MiniValidator → Validot), §9 (the five packages approved; stale rename prohibition removed).

Does **not** touch the endpoint layer — that is F-019 (pilot) and F-020 (rollout).

Program-level research and the 10 evidenced defects live in `docs/pdlc/brainstorm/brainstorm_refactor-minimal-apis_2026-08-18.md`.
