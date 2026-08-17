# Episode — aspire-wiring (F-013)

**Phase:** Construction (Build → Review) · **Date:** 2026-08-17 · **Branch:** `feat/F-013-aspire-wiring`
**Commits:** 20 · **Tests:** 189 → **286 passing**, 0 failing, 0 warnings · **Tasks:** 13 of 14 done

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

1. **⚠️ Rotate the `agenda_buddy` Atlas credential and review the cluster access log.** Removed from the working tree; still in git history and still valid. Threat T-001 / OQ-1.
2. **F-013-T14 open** — the AppHost end-to-end run is unproven. Containers and dashboard came up; the 7 services never launched. Leading hypothesis: untrusted dev certificate, needing an interactive `dotnet dev-certs https --trust`. **AC-1.1 and AC-1.3 are unproven, so ship is gated on this.**
3. **CONSTITUTION §7 security scan** still unimplemented — CI has one credential pattern, not a scanner. Deferred to F-017.
4. **`agenda-buddy-prr`** — MobileApp CS0103; also breaks the `build-mobile-tests` CI job.
5. **Nordstrom standards gate (Step 12.6) did not run** — the six `.nordstrom-standards/*` source repos do not resolve under the current `gh` auth. Not an override; the inputs were unavailable.
