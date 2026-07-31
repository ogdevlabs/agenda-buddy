# Deployments
<!-- pdlc-template-version: 1.1.0 -->
<!-- Canonical register of deployment environments for this project.
     Maintained by Pulse during the Ship and Verify sub-phases; read by the
     team on every ship to understand the current deployment surface. -->

**Project:** Agenda Buddy
**Last updated:** 2026-07-30

---

## Environments

### Environment: local

**Purpose:** Local development environment via Docker Compose
**URL:** http://localhost (per-service ports defined in launchSettings.json)
**Status:** active

#### Deploy

- **Method:** Docker Compose
- **Command:** `docker compose -f docker-compose.yml -f docker-compose.override.yml up -d`
- **Workflow file:** docker-compose.yml / docker-compose.override.yml
- **Custom deploy artifact:** none — default pipeline
- **Latest Deployment Review MOM:** n/a
- **Triggered by:** developer manually
- **Typical duration:** ~2 minutes (first build longer due to image pulls)

#### Verification

- **Smoke test URL:** http://localhost:{port}/swagger (per service)
- **Required smoke checks:** Swagger UI loads; MongoDB connection healthy; Kafka broker reachable

#### Rollback

- **Method:** manual — `docker compose down` and redeploy previous image
- **Command:** `docker compose down`
- **Reversibility window:** immediate
- **Last successful rollback:** n/a

#### Required secrets / env vars

| Name | Purpose | Source |
|------|---------|--------|
| LibrarySettings:MongoDB:ConnectionString | MongoDB connection | appsettings.json / User Secrets |
| LibrarySettings:MongoDB:DatabaseName | MongoDB database name | appsettings.json |
| LibrarySettings:MongoDB:EventsCollection | MongoDB events collection name | appsettings.json |

#### Tags

| Key | Value | Notes |
|-----|-------|-------|
| tier | dev | Local development only |
| cloud-provider | none | Docker Compose local |

#### Deployment History

| Date | Version | Deployed by | Episode | Notes |
|------|---------|-------------|---------|-------|
<!-- No tracked deployments yet. -->

#### Notes

Terminate with: `docker compose down`

---

## Cross-environment references

- **Promotion path:** local → (staging TBD) → (production TBD)
- **Shared infrastructure:** none yet — all environments isolated
- **Data migration policy:** not yet defined
- **Smoke test dependencies:** none yet

---

## Change Log

| Date | Change | Author |
|------|--------|--------|
| 2026-07-30 | Initial DEPLOYMENTS.md created at PDLC initialization | Atlas |
