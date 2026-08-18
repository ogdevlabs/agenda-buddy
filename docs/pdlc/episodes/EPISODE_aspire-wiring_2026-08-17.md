# Episode 001 — aspire-wiring (F-013)

**Phase:** Operation Complete (Inception → Construction → Ship) · **Date delivered:** 2026-08-18 · **Version:** `v0.1.0`
**Branch:** `feat/F-013-aspire-wiring` (merged, PR #35) · **Status:** Approved
**Commits:** 24 · **Tests:** 189 → **305 passing**, 0 failing, 0 warnings · **Tasks:** 14 of 14 done

> **Header reconciled at Ship (2026-08-18).** The draft above originally read "20 commits · 286 passing · 13 of 14 done" and was written mid-Construction, before ISSUE-001 was root-caused. The final numbers are 24 commits and 305 tests, and all 14 tasks are done. The body below is the Construction-time record and is left as written, except where a later section explicitly corrects it — the point of an episode is what we believed and when, not a tidied-up version.

## What shipped

`AgendaBuddy.AppHost` (MongoDB + Kafka + 7 services), `AgendaBuddy.ServiceDefaults` (OpenTelemetry, health checks, service discovery, HTTP resilience), `MongoConnectionResolver` + `MongoHealthCheck` in `Library`, one shared `IMongoClient` across all 7 services and `EventStore`, a configuration-driven Kafka broker, the committed Atlas credential removed from 17 tracked files, CI path filters plus AppHost build and two guard assertions, README and ADR-013.

## What the plan got wrong, and how we found out

Four approved-plan claims were false. Each was caught by *executing* something rather than reading it — which is the transferable lesson.

1. **The spike earned its place.** T-01 was a decision gate, and it fired: `Aspire.MongoDB.Driver` requires driver ≥ 3.9.0 against a pinned 2.25.0, failing restore with `NU1605`. Had we built T-02…T-08 first and discovered this at integration, the escape hatch would have been a rewrite instead of a branch. **Front-load the empirical gate whenever a plan rests on "X should be compatible."**
2. **"The existing tests keep compiling" was wrong** (ARCHITECTURE §3.3). The coupling was the *primary constructor*, not the interface everyone was looking at. Three test files construct the concrete class directly. **When a design claims backward compatibility, grep the actual construction sites — not the interface.**
3. **AC-1.4 assumed dynamic ports come free.** Aspire pins them by *two* independent routes: the launch profile *and* `Kestrel:Endpoints` in `appsettings.json`. Fixing only the first left `booking` on 6033, and only a test caught it.
4. **AC-2.1 was self-defeating.** "`git grep '<password>'` returns zero matches" embedded the password in the PRD, guaranteeing a match forever. **An acceptance criterion that quotes the secret it forbids can never pass.**

## The two defects verification found that review would not have

Both were pre-existing on `main` and invisible to inspection:

- **Six of seven services could not start in `Development`** — `AddSingleton<IRequestCollection>` consuming a `Scoped` `IEventStore`, rejected by DI validation, which is enabled only in `Development` — precisely the environment Aspire uses. This is almost certainly *the* concrete reason the solution "could not be started", the premise the whole feature was written against. It would have broken AC-1.1 on first run.
- **`Profession` seeded synchronously at DI-registration time** (`.Wait()` on a network call). Its tests took 30 s; after relocating to a hosted service, 168 ms.

**Lesson: "verify the acceptance criteria" must mean run the thing.** Every criterion marked *code review* passed by inspection. Both real defects sat behind criteria that required starting a process.

## Connection-pool behaviour change (call it out, it is not a refactor)

`EventStore` was `Scoped` and built a `MongoClient` per request scope, while every command and query handler writes an audit event — so the process created a client, pool, and monitoring threads **per HTTP request**. It now receives the process-wide singleton. This is the intended fix (AC-4.3) and the highest-value line in the feature, but it is a runtime behaviour change, not a cosmetic one.

## Reviewer gap — recorded, not smoothed over

**Echo did not report.** Spawned with full context, went idle, ignored a follow-up. The round continued with 3 of 4 per the spawn-failure rule. **Consequence: no independent test-coverage verdict exists.** Coverage rests on my own attestation. Phantom found zero Critical and one Important (the CI credential guard exempted `docs/pdlc` — the one tree that had already ingested the credential); Jarvis found the health endpoints undocumented in the README. Both fixed inline.

## Tech debt

| Item | Repayment condition |
|---|---|
| 7 `MongoDbConfiguration` classes + 7 interfaces now kept alive solely by 3 tests | Delete with the tests, or convert those tests to the new path |
| 7 near-identical `ServiceCollectionMongoResolutionTest.cs` (~150 lines each) | Collapse to a shared theory when one of them next needs editing |
| `AppHostWiring` mutates Aspire-produced `EndpointAnnotation`s | Revisit on any Aspire major upgrade |
| `docs/pdlc/context/` describes pre-Aspire wiring | Refreshes at Ship (Reflect 16c-bis) |

## Outstanding — NOT closed by merging

1. **⚠️ Rotate the `agenda_buddy` Atlas credential and review the cluster access log.** Removed from the working tree; still in git history and still valid. Threat T-001 / OQ-1. *(Re-graded **MEDIUM** on 2026-08-18: the maintainer confirmed the cluster holds only synthetic/development data, so this is a dev-data-integrity and Atlas-resource-abuse risk, **not** a personal-data breach — there is no GDPR clock. Rotation is still required; the credential is publicly recoverable from `origin/main` history and there are no backups.)*
2. ~~**F-013-T14 open** — the AppHost end-to-end run is unproven. Containers and dashboard came up; the 7 services never launched. Leading hypothesis: untrusted dev certificate, needing an interactive `dotnet dev-certs https --trust`. **AC-1.1 and AC-1.3 are unproven, so ship is gated on this.**~~
   **RESOLVED 2026-08-18 (T-14 closed, ISSUE-001).** The dev-certificate hypothesis was wrong. Root cause was a missing `AgendaBuddy.AppHost/Properties/launchSettings.json`. AC-1.1, AC-1.2 and AC-1.3 are now executed and verified, and were re-confirmed live at the Ship/Verify gate: all seven services `/health` = `Healthy`, `/alive` = 200. The three remaining dashboard visual checks were also completed by human inspection at that gate — **nothing in F-013 is now recorded as unverified** (`agenda-buddy-e7e` closed).
3. **CONSTITUTION §7 security scan** still unimplemented — CI has one credential pattern, not a scanner. Deferred to F-017.
4. **`agenda-buddy-prr`** — MobileApp CS0103; also breaks the `build-mobile-tests` CI job.
5. **Nordstrom standards gate (Step 12.6) did not run** — the six `.nordstrom-standards/*` source repos do not resolve under the current `gh` auth. Not an override; the inputs were unavailable.

---

## Deployment Record

| Field | Value |
|---|---|
| **Deployed to** | `local` only, at `v0.1.0`. **No remote environment.** |
| **CI/CD method** | None triggered. Detection succeeded (`.github/workflows/deploy.yml` exists) but the deploy was deliberately skipped. |
| **Custom deploy artifact used** | No — default pipeline. |
| **Deployment Review Party** | Not convened — no custom artifact was supplied. |
| **Overrides used** | **None.** No `/override` was invoked. Two guardrail *warnings* were logged (see below) — these are logged warnings, not Tier-1 override ceremonies. |
| **Config changes introduced** | Three AppHost secret parameters replace the old committed connection string: `Parameters:mongodb-password`, `Parameters:jwt-public-key`, `Parameters:jwt-private-key` (user secrets, Development only). CI gained path filters, an AppHost build step, two guard assertions, and in-step JWT keypair generation. `azure.yaml` and `.github/workflows/deploy.yml` added but never executed. |
| **New tags recorded** | `cloud` environment registered in DEPLOYMENTS.md with `tier: dev` (provisional) and `cloud-provider: azure`. `local` re-described for the Aspire AppHost. |
| **Rollback tested** | No. Local rollback is stopping the process; there is no remote deployment to roll back. |
| **DEPLOYMENTS.md updated** | Yes — `local` rewritten for the AppHost, `cloud` registered as known-but-never-deployed, and the deploy-skip recorded with its reasons. |

**Why no deploy — recorded so this doesn't read as an oversight:**

1. The unrotated `agenda_buddy` Atlas credential is a hard prerequisite. It is out of the working tree but **9 commits still carry it in git history and it remains valid**. Deploying against it means the deployment and whoever else holds that credential share a database containing client names, emails, phone numbers and appointment records.
2. No Azure subscription is wired to this machine.
3. The first `azd up` must be interactive, because azd discovers parameter names through prompts; only then can `AZD_ENV_VARS` be populated for the workflow.

**Guardrail warnings logged at this ship** (both in STATE.md's Guardrail Log):
- `ship_phase_mismatch` — `/ship` started with Current Phase `Construction`, not `Construction Complete`. The branch was already merged and 14/14 tasks done; the phase marker was simply never advanced after the ISSUE-001 fix.
- `required_gate_unmet` — CONSTITUTION §7's security scan is marked always-required and un-uncheckable but is not implemented as an automated gate. Ran by hand at this ship instead (results below). F-017 owns making it real.

---

## Reflect Notes

### Per-agent contributions

| Agent | Contribution |
|---|---|
| **Neo** (Architect) | Designed the AppHost / ServiceDefaults split and the `MongoConnectionResolver` resolution order; wrote the AC attestation in `verification.md`; found and fixed the `IRequestCollection` captive dependency and the DI-registration-time `.Wait()` in Profession seeding. Also authored I-3 (the dead `IMongoDbConfiguration` abstraction) against his own design. |
| **Echo** (Test Engineer) | **Reported late — after the review file was written and the approval gate had already been answered — and changed the outcome.** Rejected T-004's "reasoned, not observed" status and the "needs Docker" deferral as a false constraint. Writing the test proved the reasoning wrong: `url.path` was exporting real email addresses. Also caught that the Singleton→Scoped fix had no regression guard. Two advisory coverage gaps logged as debt. |
| **Phantom** (Security) | Threat-modelled the design (T-001…T-004). Found I-1: the CI credential guard excluded `docs/pdlc`, the one tree with a proven record of ingesting the credential. Cleared T-002 (probe amplification) and T-003 (dashboard/secret exposure) with asserting tests. **Missed T-004** — passed it on the same citation-instead-of-code reasoning Echo rejected. |
| **Jarvis** (Tech Writer) | Found I-2: `/health` and `/alive` documented in the design docs but absent from the README, where an ops engineer actually looks. Authored ADR-013, the README AppHost workflow, the secrets-provisioning guide, and the v0.1.0 CHANGELOG. |
| **Pulse** (DevOps) | Ship: tagged `v0.1.0`, ran the §7 scan by hand, drove the live AppHost verification of the three outstanding dashboard checks, and refused the deploy with written reasons rather than skipping it silently. |
| **Muse** (UX) | Did not participate — no UI surface. Step 10.6 correctly triaged to Skip, so there is no `ux-review.md` and no UX scorecard row. |

### What went well

1. **The spike earned its place, and this is the transferable lesson.** T-01 was a real decision gate and it fired: `Aspire.MongoDB.Driver` requires driver ≥ 3.9.0 against a pinned 2.25.0, failing restore with `NU1605`. Discovered at integration instead, the escape hatch would have been a rewrite rather than a branch.
2. **A late reviewer was allowed to change the outcome.** The approval gate had already been answered when Echo reported. The finding was accepted anyway, the Critical was fixed, and the review file was rewritten to say so — including the original "Echo did not report" note, left visible. A silent reviewer was not treated as a clean bill of health.
3. **Two pre-existing defects were found that no amount of reading would have caught** — six of seven services unable to start in `Development`, and Profession blocking DI registration on a network call. Both required starting a process.
4. **The connection-pool fix is the highest-value line in the feature and was called out as behaviour, not cosmetics.** `EventStore` was building a `MongoClient`, pool, and monitoring threads *per HTTP request*, on every request, because every handler writes an audit event.
5. **Test count grew 189 → 305 with zero regressions** and zero warnings, and no existing test source was modified or deleted.

### What broke or slowed us down

1. **The plan contained four false claims**, each caught by executing rather than reading: the compatibility assumption (above); "existing tests keep compiling" (the coupling was the primary constructor, not the interface); AC-1.4's assumption that dynamic ports come free (Aspire pins them by *two* routes — launch profile *and* `Kestrel:Endpoints`); and AC-2.1, which **embedded the password it forbade**, guaranteeing a permanent match.
2. **ISSUE-001 cost a full debugging cycle on two wrong hypotheses.** `AddProject<TProject>`, endpoint annotations, and the dev certificate were all investigated and disproven. The actual cause was a missing `Properties/launchSettings.json`: no `DOTNET_ENVIRONMENT` → `Production` → user secrets never load → every parameter `ValueMissing` → all seven services park in `Waiting` **with nothing logged above Debug**. The silence was the expensive part.
3. **Two CI jobs had never actually run** and only surfaced here: `build-mobile-tests` was failing outright on a `MobileApp` compile error, so 67 tests had never executed; and the new `Assert every service starts in Development` guard consumed `secrets.CI_JWT_*` that were never created, so it first ran — and first failed — on PR #35.
4. **AC verification was initially satisfied by inspection.** Every criterion marked *code review* passed by reading. Both real defects sat behind criteria that required starting a process.

### What to improve next time

1. **"Verify the acceptance criteria" must mean run the thing.** Any criterion whose evidence is "code review" on a startup, wiring, or configuration concern should be re-classified as executable before the plan is approved.
2. **Never let a security mitigation be asserted by citation.** T-004 was marked mitigated because instrumentation *should* record templates. A security-relevant claim with no asserting test is Critical. Corollary: "it needs a container runtime" is a hypothesis to test, not an excuse to accept — an in-memory exporter sees exactly what a collector sees.
3. **Write acceptance criteria that don't quote the secret they forbid**, and add a CI job to the pipeline in the same PR that adds the secret it depends on — an unrun guard is worse than no guard, because it reads as coverage.

### Metrics snapshot

| Metric | Value |
|---|---|
| Cycle time | **3 days** (roadmap claim 2026-08-15 → shipped 2026-08-18) |
| Test pass rate | **100%** (305 / 305, 0 failing, 0 warnings) |
| Tasks completed | **14 of 14** |
| Review findings | 1 Critical (fixed) · 3 Important (2 fixed in review, I-3 fixed after) · 9 Advisory · 3 over-engineering |
| Security findings | **2** — 1 Critical (C-1, PII in exported spans — Echo) + 1 Important (I-1, CI guard exempted `docs/pdlc` — Phantom) |
| Review rounds | 1 (plus one late single-reviewer report that reopened it) |
| Strike escalations | 0 |
| Tier-1 overrides | 0 |

### Security scan run at Ship (CONSTITUTION §7, by hand)

| Half | Result |
|---|---|
| Dependency audit — `dotnet list package --vulnerable --include-transitive` | **0 vulnerable packages** across all 25 projects |
| Secret scan — working tree | **Clean.** All `mongodb+srv://` matches are placeholders or explicit `<REDACTED-ROTATE-THIS>` markers |
| Secret scan — git history | **9 commits still carry the live credential.** Confirms ISSUE-002; no new exposure |

Neither `gitleaks` nor `trufflehog` is installed, so the secret half was pattern greps, not a scanner, and it ran manually rather than in CI. **This does not discharge the gate** — F-017 still owns making it automated. It is recorded here because running it and reporting the result is more useful than deferring it entirely.

### Post-ship housekeeping

`dotnet format` surfaced **69 pre-existing `WHITESPACE` findings** across 19 projects at the ship gate (0 analyzer/style findings). Fixed and committed as a separate `style:` commit **after** the `v0.1.0` tag, so the release diff stays readable. 305 tests pass before and after, and `--verify-no-changes` now returns clean. The repo still has **no `.editorconfig`**, so this drift will return — adopting one is filed as follow-up work.

---

## Approval

**Status:** Approved
**Approved by:** oscargarcia@ogdevlabs.onmicrosoft.com
**Approved date:** 2026-08-18
**Version shipped:** `v0.1.0` (tag at `c86bca9`)
**Links:** PR [#35](https://github.com/ogdevlabs/agenda-buddy/pull/35) (feature) · PR [#36](https://github.com/ogdevlabs/agenda-buddy/pull/36) (AppHost secrets doc)
