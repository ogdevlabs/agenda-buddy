# Acceptance Criteria Verification — F-013 aspire-wiring

**Attested by:** Claude (Neo, Construction lead) · **Date:** 2026-08-17
**Branch:** `feat/F-013-aspire-wiring` · **SDK:** 10.0.400 · **Aspire:** 13.4.6

> **Environment limitation, stated up front.** This machine has **no container runtime** — neither `docker` nor `podman` is installed. Five criteria depend on the AppHost actually provisioning MongoDB and Kafka, so they **could not be verified here** and are carved into **F-013-T14** for someone with Docker. They are listed as `BLOCKED`, not as passing. Nothing below is attested from inspection alone unless its Verification column says "code review".

## Summary

| Outcome | Count |
|---|---|
| ✅ Verified | 18 |
| ⛔ Blocked on a container runtime (→ F-013-T14) | 5 |

The plan expected AC-1.1/1.2/1.3, AC-3.2/3.3, AC-3.4 and AC-4.1 to be manual (E-7). **Four of those were automated or executed instead** — AC-3.1, AC-3.2, AC-3.3 by probing a live service, and AC-4.1 by starting all seven.

---

## US-1 — One command starts the whole stack

| AC | Outcome | Evidence |
|---|---|---|
| AC-1.1 | ⛔ **BLOCKED** | Needs a container runtime. → F-013-T14 |
| AC-1.2 | ⛔ **BLOCKED** | Dashboard listing needs a running AppHost. → F-013-T14. *Partial:* `AppHostWiringTest.AllNineResourcesAreRegistered` asserts all nine exist in the model. |
| AC-1.3 | ⛔ **BLOCKED** | Needs the dashboard. → F-013-T14. *Partial:* all 7 services verified to start and serve `/health` standalone (see AC-3.2). |
| AC-1.4 | ✅ | `AppHostWiringTest.NoServiceBindsAHardcodedHostPort`. Took **two** fixes — the launch profile *and* `Kestrel:Endpoints` in `appsettings.json` both pin `603x`. The test fails if either route reopens. |
| AC-1.5 | ✅ | `dotnet list AgendaBuddy.AppHost reference` → the 7 services, no MobileApp. Also asserted in-test and guarded in CI. |

## US-2 — No secrets in the repository

| AC | Outcome | Evidence |
|---|---|---|
| AC-2.1 | ✅ | `git grep` for the password literal returns nothing in tracked files. **The criterion was self-defeating as written** — it embedded the password in the PRD, guaranteeing a match. The PRD and brainstorm log were redacted to a placeholder. |
| AC-2.2 | ✅ | `git grep -nE 'mongodb(\+srv)?://[^ "/]+:[^@"]+@'` returns nothing in tracked files. 17 files cleaned, not the 14 estimated — two were `docs/pdlc/context/` files the hydrate backfill had copied the URI into. Guarded in CI. |
| AC-2.3 | ⛔ **BLOCKED** | Depends on AC-1.1. → F-013-T14 |
| AC-2.4 | ✅ | All 7 services started standalone with only `ConnectionStrings__mongodb` set, in both `Staging` and `Development`, each reaching "Now listening on". |
| AC-2.5 | ✅ | `MongoConnectionResolverTest.Resolve_ThrowsNamingEveryKeyTried_WhenNoneResolves` and `Resolve_FailureMessageIsActionable`. Empty and whitespace values are treated as absent, which is what makes the emptied keys fall through rather than reaching `MongoClient`. |

> AC-2.1/2.2 clean the **working tree only**. The credential remains in git history and stays valid until rotated. See "Outstanding" below.

## US-3 — Every service is observable

| AC | Outcome | Evidence |
|---|---|---|
| AC-3.1 | ✅ | `ServiceDefaultsExtensionsTest` covers both endpoints; confirmed live against Booking — `GET /health` and `GET /alive` both respond. |
| AC-3.2 | ✅ **executed, not inspected** | Booking started with no MongoDB reachable: `GET /health` → **HTTP 503 `Unhealthy`**. |
| AC-3.3 | ✅ **executed** | Same instant, same process: `GET /alive` → **HTTP 200 `Healthy`**. This is risk **R-6** disproven in practice — the process is alive while its database is not, so an orchestrator will not restart it. Also covered by `MapDefaultEndpoints_AliveStaysOk_WhenAReadinessCheckFails`. |
| AC-3.4 | ⛔ **BLOCKED** | Traces/metrics/logs in the dashboard need a running AppHost. → F-013-T14. Registration of the OTel providers is asserted by `AddServiceDefaults_RegistersOpenTelemetryTracingAndMetrics`. |
| AC-3.5 | ✅ | `builder.AddServiceDefaults();` present exactly once in all 7 `Program.cs`. |

## US-4 — The stack starts outside Development

| AC | Outcome | Evidence |
|---|---|---|
| AC-4.1 | ✅ **executed, all seven** | `ASPNETCORE_ENVIRONMENT=Staging` + `ConnectionStrings__mongodb`, `--no-launch-profile`: Booking, Calendar, Customer, Provider, Services, Profession, Identity each reached "Now listening on". |
| AC-4.2 | ✅ | No `new MongoClient` is reachable with a possibly-null argument. The production path resolves through `MongoConnectionResolver.Resolve`, which never returns null; the seven retained legacy constructors had a null-forgiving `!` and are now **guarded with a named-key throw**, which closed the last gap. |
| AC-4.3 | ✅ | `public EventStore(IMongoClient client, IConfiguration configuration)` — injected, no longer constructing a client per request scope. |

## US-5 — Nothing existing breaks

| AC | Outcome | Evidence |
|---|---|---|
| AC-5.1 | ✅ *restated* | **286 passing, 0 failing.** The plan's "all 256 pass" is unattestable: `MobileApp` does not compile under `/p:MobileWorkloads=false` (`agenda-buddy-prr`, pre-existing on main), so `MobileApp.Tests` never runs. Measured baseline before any change was **189 across 10 projects**; this feature adds **97** and breaks none. |
| AC-5.2 | ✅ | No existing test source modified or deleted. `git diff main` on `*.Tests/*.cs` shows only new files; the only existing-file changes are `.csproj` package-version bumps. |
| AC-5.3 | ✅ | `git diff main --name-only` touches nothing under `EventAndCommands/Commands/`, `/Queries/`, `/Events/`, `Library/Services/`, `Library/Entities/`, `Library/Repositories/` or `Library/Tools/`. The only `EventAndCommands` change is `Persitency/EventStore.cs`. |

## Threat-model verification

| Threat | Outcome |
|---|---|
| **T-002** — probe amplification | ✅ `MongoHealthCheck` caches for 5s behind a double-checked semaphore; asserted by call count (`Times.Once` across two in-window probes), and unhealthy results expire on the same schedule so recovery is reported. |
| **T-003** — dashboard/secret exposure | ✅ Both JWT keys are `secret: true` parameters, asserted by `JwtKeyIsASecretParameter`; only Identity receives the private key. Dashboard sensitivity documented in the README. |
| **T-004** — PII in exported spans | ✅ **Now mitigated and tested — the earlier entry here was wrong.** It claimed the threat was covered because instrumentation records `http.route` templates, and deferred observation to a manual AppHost run. Echo challenged both halves at Party Review and was right on both. An in-memory exporter observes exactly what an OTLP collector receives with **no container runtime**, and when that test was finally written it **failed**: `http.route` is indeed the template, but `url.path` carries the literal path — `url.path=/api/v1/providers/customer.pii@example.com`. Since this system puts email addresses in paths, every such request was exporting PII. Fixed by `PiiRedactingProcessor` in ServiceDefaults, which redacts email patterns from `url.path`/`url.query`/`url.full`/`http.url`/`http.target` and the display name before export, preserving path shape for debugging. 4 tests in `TelemetryPiiTest` assert it from the exporter's side (path case, query-string case, template preserved, shape preserved). |
| **T-001** — committed credential | ⛔ **Not remediated, by design.** See below. |

## Outstanding — not closed by merging this feature

1. **Rotate the `agenda_buddy` Atlas credential and review the cluster access log.** The value is out of the working tree but remains in git history and stays valid until rotated. PRD **OQ-1** / threat **T-001**. This is an operational action for a human.
2. **`CONSTITUTION.md` §7 security scan** (dependency audit + secret scan) is still not implemented. CI gained a single-pattern credential assertion, which is not a scanner. Deferred to **F-017**.
3. **`agenda-buddy-prr`** — `MobileApp` compile failure; also breaks the `build-mobile-tests` CI job.
4. **`scripts/seed/seed-mongo.sh`** is stale: it hardcodes `mongo:27017` and targets `ProviderDb`/`CustomerDb`, which no service reads. Documented, not fixed (E-8).
5. **No integration-test harness** exists (E-7). The five blocked criteria are blocked because of this plus the missing container runtime.
