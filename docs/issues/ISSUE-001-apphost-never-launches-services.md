# ISSUE-001 — AppHost never launches the 7 services

**Status:** ✅ **RESOLVED 2026-08-18** · **Severity:** P1 · **Filed:** 2026-08-17
**Feature:** F-013 aspire-wiring · **Branch:** `feat/F-013-aspire-wiring`
**Tracker:** `agenda-buddy-6sl` (beads) · **Bisection log:** `docs/pdlc/tasks/F-013/F-013-T14.md`
**Blocked:** AC-1.1, AC-1.2, AC-1.3, AC-2.3, AC-3.4 — all now verified.

---

## Symptom

`dotnet run --project AgendaBuddy.AppHost` brought up MongoDB, Kafka and the Aspire dashboard, but **none of the seven API services ever started.** DCP created only the dashboard executable and **0** executables for the projects, while still registering their DCP `services` and `endpoints`. **Nothing was logged** — no error, no `FailedToStart`.

## Root cause

**`AgendaBuddy.AppHost` had no `Properties/launchSettings.json`, so `DOTNET_ENVIRONMENT` was unset and the AppHost ran as `Production` — and `HostApplicationBuilder` only adds user secrets in `Development`.** Every secret parameter was therefore unresolvable. With `Logging__LogLevel__Aspire=Debug` the app model says so plainly:

```
Resource jwt-public-key/jwt-public-key     changed state: Waiting -> ValueMissing
Resource jwt-private-key/jwt-private-key   changed state: Waiting -> ValueMissing
Resource mongodb-password/mongodb-password changed state: Waiting -> ValueMissing
```

A project resource whose environment depends on an unresolved parameter is never scheduled: it parks in `Waiting`. A wait is not an error, so nothing is logged — that is the whole of the silence.

The same cause starved MongoDB's password. Its value was never read back from user secrets, so Aspire generated a fresh one on every run (observed across three consecutive runs: `8Fz17…`, `Dh7j…`, `ZAgCc…`, each overwriting the secret) while `WithDataVolume()` kept the root user created by the first run. `MONGO_INITDB_ROOT_PASSWORD` is ignored on a non-empty `/data/db`, so authentication failed permanently, `mongodb` never reached `Healthy`, and `WaitFor(mongo)` never released the services either. Two gates, one cause.

### The two "blockers" in the original report were both misdiagnoses

- **Blocker 1 was wrong.** `AddProject<TProject>` is fine. All seven generic-overload resources are created and reach `Starting -> Waiting`. The side-by-side experiment was uncontrolled: `AddProject("booking-via-path", …)` was added *without* the parameter-backed `WithEnvironment`, so nothing gated it. The overload was never at fault, and neither was the `net10.0` metadata or `SuppressBuild`.
- **Blocker 2's culprit was (c)**, the parameter-backed `WithEnvironment`. Port clearing (a) and `WaitFor` (b) are innocent; `WaitFor` merely inherited the same failure through the Mongo health check.

## Second defect, found once the services could start

`profession` then crashed on startup (exit 134) with `No MongoDB connection string found`. `WithReference(database)` injects `ConnectionStrings__<resource name>` — `agenda-buddy` or `IdentityDb` — but `MongoConnectionResolver`'s primary key is `ConnectionStrings:mongodb` (`Library/Configuration/MongoConnectionResolver.cs:15`). Profession resolves its client eagerly, so it failed fast; the other six resolve lazily and would have failed on their first request instead.

## Fix

| File | Change |
|---|---|
| `AgendaBuddy.AppHost/Properties/launchSettings.json` | **New.** Sets `DOTNET_ENVIRONMENT=Development` so user secrets load. |
| `AgendaBuddy.AppHost/AppHostWiring.cs` | `mongodb-password` declared as a secret parameter and passed to `AddMongoDB`, so it is stable across runs alongside `WithDataVolume()`. |
| `AgendaBuddy.AppHost/AppHostWiring.cs` | Added `.WithEnvironment("ConnectionStrings__mongodb", database)` — the canonical key the services actually read, still pointing at each service's own database. `WithReference` is retained for the dashboard relationship. |
| `AgendaBuddy.AppHost.Tests/AppHostWiringTest.cs` | +8 tests: `MongoDbPasswordIsAStableSecretParameter`, and `EveryServiceReceivesTheCanonicalMongoConnectionStringKey` across all seven services. 36 pass. |

One-off local step, needed because the existing data volume still held the old root user: `docker volume rm agendabuddy.apphost-<hash>-mongodb-data`. Documented in README troubleshooting.

## Verification (2026-08-18, Rancher Desktop)

- `dotnet run --project AgendaBuddy.AppHost` in a clean shell with **no exported environment variables** — **AC-1.1** ✅
- DCP lists 8 executables (dashboard + 7 services) all `Running`, plus the `mongodb` and `kafka` containers — 9 resources, **AC-1.2** ✅
- `/health` returns `200 Healthy` on all seven — **AC-1.3** ✅ and, since `/health` runs the MongoDB connectivity check, **AC-2.3** ✅
- Allocated host ports were dynamic (`localhost:493xx`); no service bound `603x`, and `AppHostWiringTest.NoServiceBindsAHardcodedHostPort` still passes — **AC-1.4** ✅
- `WaitFor` ordering retained; secret parameters still declared `secret: true`, so threat T-003 masking is unchanged. **No tradeoff was taken.**

## Definition of done

- [x] `dotnet run --project AgendaBuddy.AppHost` starts all 7 services
- [x] Dashboard lists 9 resources, all 7 services `Healthy` (AC-1.2, AC-1.3)
- [x] AC-1.4 still holds — no service binds `localhost:603x`
- [x] `WaitFor` ordering retained
- [x] JWT keys still declared as secret parameters (T-003)
- [x] `verification.md` updated; F-013-T14 closed

## Environment notes

- Rancher Desktop puts `docker` at `~/.rd/bin`, which is **not on PATH** by default, and Aspire shells out to `docker`: `export PATH="$HOME/.rd/bin:$PATH"` first.
- The Rancher VM here is **2 CPUs / 4.1 GB** and already runs a k8s cluster. Mongo + Kafka + 7 services works, but is tight.
- Debug the app model with `Logging__LogLevel__Aspire=Debug` — resource state transitions and parameter states are logged at `Debug`, which is exactly what made this diagnosable.
