# Deployments
<!-- pdlc-template-version: 1.1.0 -->
<!-- Canonical register of deployment environments for this project.
     Maintained by Pulse during the Ship and Verify sub-phases; read by the
     team on every ship to understand the current deployment surface. -->

**Project:** Agenda Buddy
**Last updated:** 2026-08-24

---

## Environments

### Environment: local

**Purpose:** Local development environment. As of F-013 (v0.1.0) the primary path is the .NET Aspire AppHost; Docker Compose remains as a legacy fallback.
**URL:** Aspire dashboard on https://localhost:17071 (per-service ports assigned by the AppHost)
**Status:** active

#### Deploy

- **Method:** .NET Aspire AppHost (primary) · Docker Compose (legacy fallback)
- **Command:** `dotnet run --project AgendaBuddy.AppHost`
- **Legacy command:** `docker compose -f docker-compose.yml -f docker-compose.override.yml up -d`
- **Workflow file:** AgendaBuddy.AppHost/AppHost.cs (`DeploymentTarget.Run`)
- **Custom deploy artifact:** none — default pipeline
- **Latest Deployment Review MOM:** n/a
- **Triggered by:** developer manually
- **Typical duration:** ~2 minutes (first run longer due to container image pulls)

#### Verification

- **Smoke test URL:** Aspire dashboard → resource list; per-service `/health` and `/alive`
- **Required smoke checks:** all 7 services reach `Healthy`; MongoDB reachable via `MongoHealthCheck`; Kafka broker reachable; traces, metrics and logs render in the dashboard

#### Rollback

- **Method:** manual — stop the AppHost process
- **Command:** `Ctrl-C` on the AppHost (legacy: `docker compose down`)
- **Reversibility window:** immediate
- **Last successful rollback:** n/a

#### Required secrets / env vars

| Name | Purpose | Source |
|------|---------|--------|
| Parameters:mongodb-password | MongoDB root password for the Aspire-managed container | User Secrets on AgendaBuddy.AppHost |
| Parameters:jwt-public-key | JWT signing public key | User Secrets on AgendaBuddy.AppHost |
| Parameters:jwt-private-key | JWT signing private key | User Secrets on AgendaBuddy.AppHost |
| LibrarySettings:MongoDB:DatabaseName | MongoDB database name | appsettings.json |
| LibrarySettings:MongoDB:EventsCollection | MongoDB events collection name | appsettings.json |

#### Tags

| Key | Value | Notes |
|-----|-------|-------|
| tier | dev | Local development only |
| cloud-provider | none | Aspire AppHost on the developer machine |

#### Deployment History

| Date | Version | Deployed by | Episode | Notes |
|------|---------|-------------|---------|-------|
| 2026-08-18 | v0.1.0 | Pulse | EPISODE_aspire-wiring_2026-08-17.md | First tracked local run under the Aspire AppHost. **Verified:** all 7 services `/health` = `Healthy` and `/alive` = 200; both containers up; all 3 dashboard visual checks passed human inspection (AC-3.4, T-004, A-3). No deployment to any remote environment. |
| 2026-08-18 | `v0.2.0` | Pulse | 002 | F-016 verified here **because the cloud target cannot be deployed from this machine** — see the cloud env's three blockers. Smoke-tested all 7 services' `/health` and `/alive` against a live AppHost. |
| 2026-08-23 | `v0.4.0` | Pulse | 004 | F-014's nine new routes verified here, same reason as v0.2.0/v0.3.0 — see the cloud env's blockers. **Verified:** all 7 services `/health`=`Healthy`/`/alive`=200; anonymous calls to the new notes/status routes 401 live; a freshly registered Provider's JWT validated across services and reached business logic (403/404, never 401) on notes, report, deactivate and notifications routes. Known shutdown gotcha recurred: `SIGTERM` on the AppHost left all 7 service processes orphaned, needing a second `pkill` on the project-path pattern. |
| 2026-08-24 | `v0.5.0` | Pulse | 005 (pending) | **Verified** against a live 8-process AppHost run on merged `main`. All 8 processes (7 services + Gateway) reached `/health`=`Healthy`/`/alive`=200. Registered and logged in a fresh Customer through the Gateway; the F-015-T14 fix held live — `GET api/v1/notifications` and `GET api/v1/messages` both returned `200 []` through the Gateway (previously `gateway-no-route` 404). Anonymous request to the same route: 401. Unmapped path (`/booking/health`, no `api/v1` prefix): `gateway-no-route` 404, confirming T-302 still holds post-merge. Known AppHost shutdown gotcha recurred (`SIGTERM` on the AppHost left all 8 processes orphaned); cleaned up by explicit PID. |

#### Notes

Terminate the AppHost with `Ctrl-C`. Legacy Compose path: `docker compose down`.

**Environment gotchas** (carried from the F-013 handoff, all still true):
- Rancher Desktop puts `docker` at `~/.rd/bin`, **not on PATH**. Aspire shells out to docker — `export PATH="$HOME/.rd/bin:$PATH"` first.
- `AgendaBuddy.AppHost/Properties/launchSettings.json` sets `DOTNET_ENVIRONMENT=Development`. **Deleting it silently breaks the whole graph** — user secrets load only in Development, so every secret parameter goes `ValueMissing` and all services park in `Waiting` with nothing logged (ISSUE-001).
- Debug the app model with `Logging__LogLevel__Aspire=Debug`; resource state transitions and parameter states are Debug-level only.
- MongoDB uses a persistent volume, so its password must stay stable. If auth breaks: `docker volume rm agendabuddy.apphost-<hash>-mongodb-data`.
- Running a service standalone needs `--no-launch-profile`, else launchSettings overrides `ASPNETCORE_ENVIRONMENT`.

**Verified at v0.1.0** (human inspection of the dashboard, 2026-08-18 — `agenda-buddy-e7e` closed): AC-3.4 traces/metrics/logs render for all 7 services; threat T-004 confirmed mitigated against live traffic (`http.route` is a template, `url.path` shows the email redacted, and the literal `customer.pii@example.com` never appeared in a span despite five deliberate attempts); review finding A-3 confirmed (both JWT parameters render masked on the `identity` resource).

**Shutdown gotcha observed 2026-08-18:** killing the AppHost with `SIGTERM` left six orphan service processes running and needed a second `pkill -f "agenda-buddy/.../bin/Debug"`. The two containers were removed cleanly. A normal `Ctrl-C` may behave differently — not investigated.

---

### Environment: cloud (Azure) — REGISTERED, NEVER DEPLOYED, **AND NOW DELIBERATELY DEFERRED**

> ## 🛑 Maintainer decision, 2026-08-22: Azure is not reviewed until two conditions are met
>
> **1. Every pending feature is completed** — F-014, F-015, F-017, F-018, F-019, F-020 (and F-022–F-024 if
> they are still on the roadmap by then).
> **2. The tech debt of "things no longer needed" is discharged** — the code, containers, scripts and
> configuration that exist only because of earlier shapes of this project, and that a deployment would
> otherwise carry into a cloud environment.
>
> **Until both hold, "deploy skipped" is the expected outcome of every ship and is no longer a gap to
> report.** This changes what the skip *means*: at v0.1.0 and v0.2.0 it was an unexercised capability being
> carried release after release, and each ship recorded it as a widening gap. It is now a **scheduled**
> deferral with named exit conditions, which is a different thing and should be read differently.
>
> **Why this is the right call and not procrastination.** Deploying now would provision infrastructure for a
> system whose own roadmap says six of its features do not work yet: F-014 exists because
> `NotificationService`, `MessageService`, `NoteService`, `PaymentService`, `ReportingService` and
> `DeactivateProviderCommand` have no DI registration, no collection and no route, so F-006–F-010 are marked
> Shipped on code nothing can call; F-015 exists because the mobile client cannot reach the backend at all.
> A cloud environment would make all of that a running cost and a security surface without making any of it
> work — and F-017, which owns the container story, has three Dockerfiles that publish `net10.0` onto a
> `dotnet/runtime:8.0` base and **cannot run**. There is nothing deployable to deploy yet.
>
> **What does NOT wait for this.** Rotating the Atlas credential (`agenda-buddy-41s`, P0) is independent and
> still urgent: the credential is valid, publicly recoverable from git history, and grants write access to a
> live cluster with no backups. It is a blocker *for* deployment, but its own justification does not depend
> on deployment ever happening.
>
> **Trackers:** `agenda-buddy-dwe` (first cloud deployment) is deferred against this decision. See ADR-035.



**Purpose:** Intended Azure Container Apps target, provisioned via `azd`.
**URL:** unknown — no deployment has been performed
**Status:** ⚠️ **not provisioned.** The capability exists in code and is unit-tested; nothing has ever run.

#### Deploy

- **Method:** Azure Developer CLI (`azd`) driving the `DeploymentTarget.Cloud` shape of the AppHost
- **Command:** `azd up` — **must be run interactively the first time**, because azd discovers parameter names through prompts
- **Workflow file:** `azure.yaml`, `.github/workflows/deploy.yml`
- **Custom deploy artifact:** none
- **Triggered by:** nobody yet
- **Typical duration:** unknown

#### Blockers before first deploy

1. ⚠️ **Rotate the `agenda_buddy` Atlas credential first** (`docs/issues/ISSUE-002-atlas-credential-rotation.md`, tracker `agenda-buddy-41s`). It remains in git history and remains valid. Deploying against it means the deployment and whoever else holds that credential share a live database. *(Corrected 2026-08-18: that database holds only synthetic/development data — no real client records — so this is re-graded MEDIUM. Still rotate before any deploy: the credential is valid, publicly recoverable, and there are no backups.)*
2. No Azure subscription is wired to this machine.
3. After the first interactive `azd up`, the discovered parameter names must go into the `AZD_ENV_VARS` repository secret for `.github/workflows/deploy.yml` to work.

> **Deploy SKIPPED at `v0.5.0` (F-015), 2026-08-24 — the fifth consecutive release, third under the
> deferral.** All three blockers unchanged. F-015's attack surface (the new Gateway process, JWT
> passthrough, the route allowlist) is verified against the local AppHost instead, per the pattern below.
> User confirmed at the deploy prompt.
>
> **Deploy SKIPPED at `v0.4.0` (F-014), 2026-08-23 — the fourth consecutive release, second under the
> deferral.** All three blockers unchanged. F-014's own attack surface (nine new authenticated routes,
> non-charging payments) is verified against the local AppHost instead, per the pattern below.
>
> **Deploy SKIPPED at `v0.3.0` (F-021), 2026-08-22 — the third consecutive release, and the first one where
> the skip is a scheduled deferral rather than a gap.** See the maintainer decision above. F-021 was verified
> against the local AppHost instead, exactly as F-016 was. One F-021-specific note for whoever eventually
> runs the first deploy: **both of this feature's security controls are off unless the cloud configuration
> turns them on.** `AppHostWiring.cs`'s `DeploymentTarget.Cloud` branch sets `Security__Hsts__Enabled` for all
> seven services and `Security__RateLimiting__Enabled` for `identity`, and `AppHostWiringTest` asserts it —
> but a deployment that bypasses the AppHost graph would ship without either control while every artifact
> records the feature as delivered (threat T-103). HSTS additionally does nothing until TLS is terminated,
> which is **F-017's**.
>
> **Deploy SKIPPED at v0.2.0 (F-016), 2026-08-18 — the second consecutive release.** All three blockers
> above are unchanged. Recorded rather than omitted so the gap does not become invisible through repetition:
> the cloud capability has now been carried, unexercised, through two tagged releases. Blocker 1 (rotate the
> Atlas credential) is human-only and is also the item that makes the new integration harness's fail-closed
> guard load-bearing. **`azd up` must be run interactively the first time**, so this cannot be automated from
> a session — it needs a human at a terminal with an Azure subscription attached.

#### Verification

- **Smoke test URL:** n/a — never deployed
- **Required smoke checks:** to be defined at first deploy

#### Rollback

- **Method:** undefined — no deployment to roll back
- **Reversibility window:** n/a

#### Tags

| Key | Value | Notes |
|-----|-------|-------|
| tier | dev | Provisional. Re-tag at first real deploy; `/night-shift` refuses `tier: production`. |
| cloud-provider | azure | Azure Container Apps via azd |

#### Deployment History

| Date | Version | Deployed by | Episode | Notes |
|------|---------|-------------|---------|-------|
<!-- No deployments. Capability written in F-013, never executed. -->

---

## Cross-environment references

- **Promotion path:** local → cloud (Azure, not yet provisioned)
- **Shared infrastructure:** MongoDB Atlas cluster `agenda_buddy` — ⚠️ currently reachable with a credential that is still in git history
- **Data migration policy:** not yet defined. Note there are **no backups** of the Atlas cluster.
- **Smoke test dependencies:** none yet

---

## Change Log

| Date | Change | Author |
|------|--------|--------|
| 2026-07-30 | Initial DEPLOYMENTS.md created at PDLC initialization | Atlas |
| 2026-08-18 | `local` re-described for the Aspire AppHost (primary) with Compose as legacy fallback; secrets table replaced with the three AppHost parameters; env gotchas and unverified checks recorded; first v0.1.0 history row added | Pulse |
| 2026-08-18 | Registered `cloud` (Azure) as a known-but-never-deployed environment with its three blockers, tagged `tier: dev` provisionally, so the unexercised capability is visible rather than implied | Pulse |
| 2026-08-18 | Deploy skipped for the v0.1.0 ship — recorded as skipped-with-reason, not as a deployment | Pulse |
| 2026-08-18 | v0.2.0 (F-016): recorded the **second consecutive** cloud deploy skip with its three unchanged blockers, and logged the local AppHost verification used in its place | Pulse |
| 2026-08-22 | **Azure review deferred by maintainer decision** until every pending feature is complete and the no-longer-needed tech debt is discharged (ADR-035). The skip stops being reported as a widening gap and becomes a scheduled deferral with named exit conditions. Credential rotation explicitly does **not** wait for it | Pulse |
| 2026-08-22 | `v0.3.0` (F-021): third consecutive cloud deploy skip, first under the deferral. Added the note that F-021's two security controls are configuration-gated and off unless the cloud graph turns them on | Pulse |
| 2026-08-23 | `v0.4.0` (F-014): fourth consecutive cloud deploy skip, second under the deferral. Nine new routes and non-charging payments verified against the local AppHost instead | Pulse |
| 2026-08-23 | Local Deployment History row finalized with smoke-test results: 7/7 Healthy, anonymous 401 confirmed live, authenticated round trip reached business logic on 4 of 9 new routes | Pulse |
| 2026-08-24 | `v0.5.0` (F-015): fifth consecutive cloud deploy skip, third under the deferral. New Gateway process, JWT passthrough, and the route allowlist verified against the local AppHost instead | Pulse |
| 2026-08-26 | `v0.6.0` (F-017) shipped without a matching skip row here — gap noted in passing, not backfilled | Pulse |
| 2026-08-26 | `v0.7.0` (F-018): seventh consecutive cloud deploy skip, sixth under the ADR-035 deferral. F-022–F-026 still remain, so the deferral's exit condition is unmet. No live AppHost smoke test run — user-approved: F-018 changed one production line (Provider's Kafka downcast fix), already exercised by 301 passing integration tests + 7 green CI Docker builds | Pulse |
