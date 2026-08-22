# Deployments
<!-- pdlc-template-version: 1.1.0 -->
<!-- Canonical register of deployment environments for this project.
     Maintained by Pulse during the Ship and Verify sub-phases; read by the
     team on every ship to understand the current deployment surface. -->

**Project:** Agenda Buddy
**Last updated:** 2026-08-18

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

### Environment: cloud (Azure) — REGISTERED, NEVER DEPLOYED

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

> **Deploy SKIPPED again at v0.2.0 (F-016), 2026-08-18 — the second consecutive release.** All three blockers
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
