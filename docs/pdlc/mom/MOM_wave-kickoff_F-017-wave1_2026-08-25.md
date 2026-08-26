# Wave Kickoff Standup — F-017 Wave 1

**Called by:** Neo (Architect)
**Participants:** Neo, Bolt, Pulse, Echo (Agent Teams mode — each spawned as an independent subagent, per this project's established `Party Mode: subagents` preference)
**Purpose:** Coordination check before claiming any of Wave 1's 4 unblocked tasks
**Date:** 2026-08-25

---

## Wave 1 tasks

F-017-T01, F-017-T02, F-017-T03, F-017-T08 — all `depends_on: []`.

## Round 1 findings

**Pulse:** No pairwise file conflicts. T01 must edit `events:` in *both* `docker-compose.yml` and `docker-compose.override.yml` (separate blocks, same service name — easy to miss half). No dangling Compose references to the deleted service names. T03's `if:` should gate the same way `build-and-test` already does, not the known-dead `library` filter output. No blockers.

**Bolt:** T02's fix is `EventAndCommands/EventAndCommands.csproj:32-36` — remove the `<None Update="appsettings.json"><CopyToOutputDirectory>Always</CopyToOutputDirectory></None>` item. Confirmed safe against `ConfigurationLoader.cs`'s read path. Found a related, same-root-cause suppression: `Customer.csproj:8` and `Provider.csproj:8` set `ErrorOnDuplicatePublishOutputFiles=false`, masking the identical collision for those two services specifically — recommends folding its removal into T02 rather than filing separately.

**Echo:** Confirmed T-001/T-002 (threat-model security ACs) don't touch Wave 1. Found a **real, previously undocumented defect**: `Profession/Dockerfile` already has the runtime/SDK major-version mismatch (`runtime:8.0` final stage vs `sdk:10.0` build stage) that T01's generalized guard is meant to catch — but Profession is a live API service, not one of the 3 files T01 deletes. If the guard is genuinely repo-wide (as specced), it fails on Profession immediately post-T01 unless fixed.

**Neo:** Independently found the same Profession/Dockerfile issue, plus a second real regression risk: `EventsAndCommands.Tests` has no appsettings.json of its own — it currently passes `ConfigurationLoaderTests` only because `EventAndCommands.csproj`'s `CopyToOutputDirectory=Always` transitively copies `EventAndCommands`'s own `appsettings.json` into the test project's output. T02's stated AC (`dotnet publish -t:PublishContainer` × 7) doesn't run the test suite, so this regression would land undetected by T02's own acceptance criterion.

## Verification (Neo, lead agent)

Both cross-cutting findings verified directly against the repo before acceptance:
- `Profession/Dockerfile:1` confirmed `runtime:8.0` (build stage `sdk:10.0`); all 5 other surviving service Dockerfiles correctly pair `aspnet:10.0`/`sdk:10.0`. Also found while checking: `Customer/Dockerfile` **does not exist** — the context catalog's "Calendar/Customer/Profession/Provider/Services follow Booking exactly" inference was wrong on two counts. Left alone (out of scope — Wave 2's image-build job uses SDK container support, not Dockerfiles, so a missing Dockerfile doesn't block it).
- `EventsAndCommands.Tests/` confirmed to contain no source-controlled `appsettings.json` (only build-output copies); `ConfigurationLoader.LoadConfiguration()` confirmed to load it as required (`optional: false`).

## Wave Execution Plan

1. **Confirmed safe parallel tasks:** T01, T02, T03, T08 — no file-level collisions. All 4 can build in parallel worktrees.
2. **Flagged sequential pairs:** none — no task blocks another.
3. **Scope widened within-task** (not new tasks, not a dependency-graph change):
   - **T01** also fixes `Profession/Dockerfile:1` (`runtime:8.0` → `aspnet:10.0`) — required for its own generalized AC-3 guard to hold true repo-wide, not speculative scope.
   - **T02** also (a) removes `ErrorOnDuplicatePublishOutputFiles=false` from `Customer.csproj:8` and `Provider.csproj:8` (same root cause, same fix), and (b) adds `EventsAndCommands.Tests/appsettings.json` (with its own `CopyToOutputDirectory`) so `ConfigurationLoaderTests` doesn't regress when the transitive copy is removed.
4. **Dependency updates:** none — the graph (`depends_on: []` for all 4) was already correct.

**Recommended order:** no ordering constraint; proceed in task-ID order (T01 → T02 → T03 → T08) for a single-agent build, or fully parallel if using worktree-isolated subagents.
