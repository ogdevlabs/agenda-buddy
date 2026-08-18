# 08 — CI/CD and Deployment

> **⚠️ F-013 delta (2026-08-18, `v0.1.0`) — this file was written 2026-08-15 and has NOT been re-read since.**
>
> **Stale.** CI gained path filters, an AppHost build step, a guard that every service starts in `Development`, and a credential-pattern guard that now **includes** `docs/pdlc` (it previously excluded the one tree that had already ingested the credential). The startup guard generates a throwaway JWT keypair in-step rather than consuming repository secrets. `azure.yaml` and `.github/workflows/deploy.yml` were added for an Azure Container Apps target — **written and unit-tested, never executed.** The three broken class-library Dockerfiles (finding 4) are **not** fixed; F-017 owns them.
>
> `file:line` anchors below may have shifted. Authoritative sources for the change: `docs/pdlc/archive/design/aspire-wiring/ARCHITECTURE.md`, `docs/pdlc/episodes/EPISODE_aspire-wiring_2026-08-17.md`. A full targeted rehydration is queued as the first step of F-018.


**Source of truth:** `.github/workflows/dotnet.yml` (the only workflow file), 8 `Dockerfile`s, `docker-compose.yml` + `docker-compose.override.yml`, `.dockerignore`, `scripts/seed/`.

---

## Branching and merge rules

Per `CONSTITUTION.md` §6: feature-branch model, one branch per feature named `feature/[kebab-case]`, single PR to `main`, **merge commit** strategy (full branch history preserved), `main` protected by PR + human approval.

Confirmed by history — recent merges are all `Merge pull request #NN from ogdevlabs/feature/<slug>`:

| PR | Branch | Merged |
|---|---|---|
| #29 | `feature/upgrade-to-net10` | 2026-07-31 |
| #30 | `chore/update-roadmap-and-readme` | 2026-07-31 |
| #31 | `feature/mobile-app` | 2026-07-31 |
| #32 | `fix/mobile-build-launch` | — |
| #33 | `fix/local-run` | — |
| #34 | `feature/ux-redesign` | 2026-08-02 |

Commit format is conventional (`<type>(<scope>): <description>`), matching `CONSTITUTION.md` §6.

⚠️ **Branch prefixes drift from the stated convention.** §6 specifies `feature/[kebab-case]`, but `chore/` and `fix/` prefixes are also in use (#30, #32, #33). Harmless, but the constitution documents only one prefix.

⚠️ **No branch-protection configuration in the repo** — no `.github/CODEOWNERS`, no ruleset file. Protection is configured server-side. `[unknown — outside repo]` whether the required-status-checks list includes the CI jobs below.

---

## The pipeline (`.github/workflows/dotnet.yml`)

**Triggers** (`:3-7`): `push` to `main`, `pull_request` targeting `main`.
**Permissions** (`:9-11`): `contents: read`, `pull-requests: read`.

Five jobs. The first computes path filters; the other four are conditional on it.

### Job 1 — `changes` (`:17`)

`ubuntu-latest`, uses `dorny/paths-filter@v3` (`:26`) to emit four boolean outputs.

| Filter | Watches | Anchor |
|---|---|---|
| `library` | `Library/**`, `EventAndCommands/**`, `Directory.Build.props` | `:30-33` |
| `api` | `Library/**`, `EventAndCommands/**`, the 7 service dirs, `Kafka/**`, `*.Tests/**` **minus** `!MobileApp.Tests/**`, `Directory.Build.props`, `*.sln` | `:34-48` |
| `mobile` | `MobileApp/**`, `Library/**`, `Directory.Build.props` | `:49-52` |
| `mobile-tests` | `MobileApp/**`, `MobileApp.Tests/**`, `Library/**`, `Directory.Build.props` | `:53-57` |

⚠️ **The `library` output is computed and never consumed.** No job's `if:` references `needs.changes.outputs.library` (`:64`, `:121`, `:144`, `:167` use `api`, `mobile`, `mobile`, `mobile-tests`). Dead output.

⚠️ **`Identity.Tests` changes alone do not trigger CI.** The `api` filter's test glob is `'*.Tests/**'` (`:45`) which matches `Identity.Tests/**`, so this is actually covered — but note `:46`'s `!MobileApp.Tests/**` negation means a PR touching **only** `MobileApp.Tests` sets `api=false` and `mobile-tests=true`, which is the intended split.

⚠️ **Changes to CI itself, `global.json`, `docker-compose*.yml`, `Dockerfile`s, `scripts/`, or `README.md` trigger no job at all.** All four filters are false, so a PR editing `global.json` (the SDK pin) or any `Dockerfile` merges to `main` **with zero builds and zero tests run**. This is the most consequential gap in the pipeline — and it is how the `runtime:8.0` Dockerfile defect below could survive the .NET 10 upgrade.

### Job 2 — `build-and-test` (`:62`)

`ubuntu-24.04` (the only pinned runner; the other three use `ubuntu-latest`/`macos-latest`). Runs when `api == 'true'`.

| Step | Line | Command |
|---|---|---|
| Checkout | `:68` | `actions/checkout@v4` |
| Setup .NET | `:70-73` | `actions/setup-dotnet@v4`, `dotnet-version: 10.0.x` |
| NuGet cache | `:75-81` | key `${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}` |
| Restore | `:83-84` | `dotnet restore /p:MobileWorkloads=false` |
| Build | `:86-87` | `dotnet build --no-restore --configuration Release /p:MobileWorkloads=false` |
| Test | `:89-97` | `dotnet test --no-build --configuration Release --filter "Category!=Acceptance" --collect:"XPlat Code Coverage" --logger "trx;LogFileName=test-results.trx" --results-directory ./TestResults` |
| Publish results | `:99-105` | `dorny/test-reporter@v1`, `if: always()` |
| Upload coverage | `:107-113` | `actions/upload-artifact@v4`, `**/coverage.cobertura.xml`, `if-no-files-found: warn` |

⚠️ **`/p:MobileWorkloads=false` on the solution-wide restore/build means `MobileApp` is compiled as the plain `net10.0` library slice in this job** — so `MauiProgram.cs` and `AppShell.xaml.cs` (both `#if MOBILE`) are excluded. Correct for speed, but it means this job never compiles the shipping app shape.

⚠️ **`--filter "Category!=Acceptance"` excludes the acceptance suite** (`MobileApp.Tests/Acceptance/AuthAcceptanceTests.cs:8` is the only `[Trait("Category", "Acceptance")]`). Nothing in CI ever runs it, and no separate job does either. Acceptance tests exist but are permanently skipped.

⚠️ **Coverage is uploaded as an artifact but never gated.** No threshold check, no ReportGenerator step, no PR comment, no Codecov/Coveralls upload. `CONSTITUTION.md` §7 requires unit tests but sets no coverage gate; `INTENT.md` "What Success Looks Like" targets ">80% unit test pass rate" — nothing measures it. `CLAUDE.md` describes the pipeline as "restore → build → test → **coverage upload**", which is accurate but the upload is inert.

⚠️ **`if-no-files-found: warn`** (`:113`) means a total coverage-collection failure degrades to a warning, not a failure.

### Job 3 — `build-android` (`:118`)

`ubuntu-latest`, when `mobile == 'true'`. `dotnet workload install maui-android` (`:133`), then `dotnet build MobileApp/MobileApp.csproj /p:MobilePlatform=android -c Release` (`:136`).

### Job 4 — `build-ios` (`:141`)

`macos-latest`, when `mobile == 'true'`. `dotnet workload install maui-ios` (`:156`), then `dotnet build MobileApp/MobileApp.csproj /p:MobilePlatform=ios -c Release` (`:159`).

⚠️ **Both mobile jobs `build` only — they never `test` and never `publish` an artifact.** No `.apk`/`.aab`, no `.ipa`, no signing, no upload. There is no path from CI to a distributable app.

⚠️ **No NuGet cache on the mobile jobs**, and `dotnet workload install` runs from scratch on every invocation — the slowest steps are uncached.

⚠️ **`macos-latest` is unpinned** while the project sets `ValidateXcodeVersion=false` (`MobileApp.csproj:42`) to tolerate Xcode drift. A runner-image Xcode bump is absorbed silently rather than surfaced.

### Job 5 — `build-mobile-tests` (`:164`)

`ubuntu-latest`, when `mobile-tests == 'true'`. One step (`:178-184`):
```
dotnet test MobileApp.Tests/MobileApp.Tests.csproj /p:MobileWorkloads=false \
  --filter "Category!=Acceptance" --collect:"XPlat Code Coverage"
```

⚠️ **No `--logger trx`, no `test-reporter` step, and no coverage upload** — unlike job 2. Mobile test results are visible only in raw job logs.

### Pipeline-wide gaps

- ⚠️ **No security scanning.** `CONSTITUTION.md` §7 marks "Security scan (dependency audit + secret scan)" as **always required, cannot be unchecked**. There is no `dotnet list package --vulnerable`, no CodeQL, no Dependabot config (`.github/dependabot.yml` absent), no secret scanner. Given that a live Atlas credential sits in 14 tracked files (`06-configuration.md`), a secret scanner would have caught it. **This is an unmet mandatory gate.**
- ⚠️ **No Docker image build in CI.** Eight Dockerfiles exist; none is built, scanned, or pushed by any workflow. The `runtime:8.0` defect below is invisible to CI for exactly this reason.
- ⚠️ **No deployment job, no environment, no release workflow.** CI stops at build+test. `docs/pdlc/memory/DEPLOYMENTS.md` exists in the memory bank; the repo contains no deployment automation.
- ⚠️ **No integration or E2E job.** `CONSTITUTION.md` §7 leaves integration/E2E/perf/a11y/visual-regression unchecked, so this is consistent with the constitution — but `CONSTITUTION.md` §5 Definition of Done requires "All integration tests pass", which no job verifies.
- ⚠️ **No concurrency group** — pushing twice in quick succession runs overlapping builds.
- ⚠️ **No `timeout-minutes`** on any job.

---

## Container images

Eight Dockerfiles: one per API service (7) plus `Library`, `Kafka`, `EventAndCommands`. Read in full: `Booking`, `Identity`, `Library`, `Kafka`, `EventAndCommands`. **Inference:** `Calendar`, `Customer`, `Profession`, `Provider`, `Services` follow `Booking/Dockerfile` exactly with the project name substituted.

### API service pattern (`Booking/Dockerfile`, `Identity/Dockerfile`)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base   # :1
USER $APP_UID                                        # :2  ✅ non-root
WORKDIR /app
EXPOSE 8080 / 8081                                   # :4-5
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build      # :7
COPY ["Booking/Booking.csproj", "Booking/"]          # :10
RUN dotnet restore "Booking/Booking.csproj"          # :11
COPY . .                                             # :12
RUN dotnet build   -c $BUILD_CONFIGURATION -o /app/build    # :14
FROM build AS publish
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false  # :18
FROM base AS final
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Booking.dll"]                 # :23
```

Good: multi-stage, non-root via `$APP_UID`, `.dockerignore` present and linked into each `csproj` as `Content`.

⚠️ **`RUN dotnet restore "Booking/Booking.csproj"` at `:11` precedes `COPY . .` at `:12`, but only `Booking.csproj` was copied at `:10`** — the restore cannot resolve the four `ProjectReference`s (`EventAndCommands`, `Kafka`, `Library`, `Library.ServerAuth`), whose `.csproj` files are not yet present. **Inference:** the restore at `:11` fails or partially succeeds, and the real restore happens implicitly during `dotnet build` at `:14` — which defeats the layer-caching intent of the split entirely. Every source change re-restores every package.

⚠️ **`EXPOSE 8080/8081` contradicts `appsettings.json`**, which binds `http://localhost:6033` (`06-configuration.md`). A container built from this Dockerfile binds the loopback on 6033 and exposes 8080 — **unreachable**. Only the `identity` Compose service fixes this, via `Kestrel__Endpoints__HTTP__Url=http://0.0.0.0:80` (`docker-compose.override.yml:130`) — and it maps to port **80**, matching neither `EXPOSE` value.

⚠️ **No `HEALTHCHECK` instruction** in any Dockerfile, and no health endpoint exists to point one at (`12-observability.md`).

⚠️ **Base images are unpinned by digest** (`aspnet:10.0`, `sdk:10.0`) — builds are not reproducible.

### ⚠️ Class-library Dockerfiles publish onto a .NET 8 runtime

`Library/Dockerfile`, `Kafka/Dockerfile`, and `EventAndCommands/Dockerfile` all follow this shape:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build     # Library/Dockerfile:1
...
FROM mcr.microsoft.com/dotnet/runtime:8.0           # Library/Dockerfile:13  ⚠️
WORKDIR /app
COPY --from=publish /app/publish .
```

| File | SDK stage | Final base | Line |
|---|---|---|---|
| `Library/Dockerfile` | `sdk:10.0` | **`runtime:8.0`** | `:1` / `:13` |
| `Kafka/Dockerfile` | `sdk:10.0` | **`runtime:8.0`** | `:1` / `:13` |
| `EventAndCommands/Dockerfile` | `sdk:10.0` | **`runtime:8.0`** | `:1` / `:12` |

**Failure scenario:** `docker compose build common-library` succeeds (the build stage uses SDK 10), then any attempt to execute the published `net10.0` assemblies on the `runtime:8.0` base fails with a framework-not-found error. F-011 upgraded every `csproj` and both `aspnet:` Dockerfiles to 10.0 but **missed these three `runtime:` lines** — and because CI builds no images (above), nothing caught it.

⚠️ **These three projects are class libraries and should not have Dockerfiles at all.** They have no `ENTRYPOINT`/`CMD` (`Library/Dockerfile` ends at `:15` with a `COPY`), so the resulting containers exit immediately. `docker-compose.yml` nonetheless declares them as services — `events` (`:58-64`), `kafka-library` (`:66-70`), `common-library` (`:72-76`) — with `events` even getting a hostname, container name, and environment block (`docker-compose.override.yml:111-117`). `EventAndCommands` is presented as a running microservice in Compose but is a library with no entry point.

⚠️ **`EventAndCommands/Dockerfile:1` uses lowercase `as build`** while every other stage alias uses `AS`. BuildKit warns (`FromAsCasing`).

---

## Compose deployment topology

Detailed in `06-configuration.md`. The deployment-relevant conclusions:

- ⚠️ **Only 1 of 7 API services (`identity`) is in Compose.** `provider` and `services-api` are commented out; Booking, Calendar, Customer, Profession were never added. `docker compose up` cannot run the application.
- ⚠️ Three of the ten Compose services are no-op class-library containers.
- ⚠️ `kafka0` is `tail -f /dev/null` (`docker-compose.override.yml:67`) — an idle container alongside the real `broker`.
- ⚠️ `kafka-ui` points at `schema-registry:8181` (`:7`) but the registry listens on `8081` (`:62`) — the UI's schema view is broken.
- ⚠️ `kafka-ui` uses `:latest` (`docker-compose.yml:6`) — the only unpinned image tag.
- ⚠️ Compose Mongo (`mongodb://mongo:27017`, `:133`) and `appsettings.json` Mongo (Atlas) are **different databases**, so behaviour differs between `docker compose up` and `dotnet run`.
- ⚠️ `PATH_BASE` env vars (`:115`, `:139`) are set but no `UsePathBase` exists in any `Program.cs` — the path-prefix routing a gateway would need is configured but not implemented.

## Local development scripts

`scripts/seed/`:
- `setup-dev-environment.sh` — `[not read in this scan]`
- `seed-mongo.sh` — waits for Mongo (`:8-10`), then `mongoimport --drop` into `ProviderDb.providers`, `CustomerDb.customers`, `IdentityDb.credentials` (`:13-34`), then creates the unique `email` index on credentials (`:37-41`), then echoes the six seeded accounts and the password `DevPass123!` (`:44-46`).
- `docker-compose.seed.yml` — `[not read in this scan]`
- ⚠️ Two of three seed targets are databases no service reads (`05-data-model.md`).
- ⚠️ `--drop` on all three imports makes the script destructive.

`compose/data/` holds `message.json` (fed to `kafka-init-topics`, `docker-compose.override.yml:73,77`), `seed-providers.json`, `seed-customers.json`. ⚠️ `compose/data/seed-*.json` duplicate `scripts/seed/seed-*.json` — two copies of the same fixtures with no single source of truth.

## What is missing

- No infrastructure-as-code of any kind: no Terraform, no Bicep/ARM, no Helm chart, no Kubernetes manifests, no App Service / Container Apps definition.
- No deploy parameter matrix per environment — there is only one environment (`Development`) that the code can actually run in.
- No secrets layout for deployment (no Key Vault reference, no GitHub environment secrets used by any workflow).
- No container registry push, no image tagging/versioning scheme.
- No smoke test or post-deploy verification.
- No rollback mechanism beyond `git revert` (PDLC's `/rollback`).
