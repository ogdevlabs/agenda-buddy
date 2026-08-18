# MOM — Wave Kickoff Standup (Wave 2)

**Feature:** F-013 aspire-wiring
**Date:** 2026-08-17
**Convened by:** Neo (Architect)
**Participants:** Neo (lead), Bolt (Backend), Echo (QA), Pulse (DevOps) — 4 agents
**Spawn mode:** agent-teams
**Wave:** 2 — entered after F-013-T01 (Wave 1 decision gate) closed

---

## Wave 2 ready queue as convened

| Task | Labels | Scope |
|---|---|---|
| F-013-T02 | infra | Create `AgendaBuddy.ServiceDefaults` |
| F-013-T03 | infra | `Library/Configuration/MongoConnectionResolver` + `Library/Diagnostics/MongoHealthCheck` |
| F-013-T07 | infra | `KafkaClient.BootstrapServers` configuration-driven |
| F-013-T12 | docs | ADR-013 in `DECISIONS.md` |

---

## Round 1 findings

### File-collision analysis — clean

Bolt confirmed the wave is a clean partition: T02 is a new project, T03 is two new files under `Library/` (not `Library/Services/`, so AC-5.3 holds), T07 touches only `Kafka/KafkaClient.cs`, T12 only `DECISIONS.md`. **No two tasks write the same file.** Echo confirmed no runtime interference either — no test project in this solution touches a live Mongo or Kafka, so the three code tasks are safe in true parallel. The only collision risk is human: a `Library.Tests/GlobalUsings.cs` merge conflict if T03 and T04 land concurrently.

### Confirmed defect in the approved design (three independent finds)

`ARCHITECTURE.md:229` and `F-013-T05.md` claim that preserving `IMongoDbConfiguration.MongoClient()`'s return type keeps the six `MongoDbConfigurationTest.cs` files compiling. **False.** The coupling is the *primary constructor* `MongoDbConfiguration(IConfiguration configuration)`, not the interface. Three tests instantiate the concrete class with a mocked `IConfiguration`:

- `Booking.Tests/Configuration/MongoDbConfigurationTest.cs:17`
- `Customer.Tests/Configurations/MongoDbConfigurationTest.cs:17`
- `Profession.Tests/Configurations/MongoDbConfigurationTest.cs:17`

Swapping the ctor to `(IMongoClient client)` is a compile break in all three, which AC-5.2 forbids. Echo further established that Calendar/Provider/Services' equivalents are empty `METHOD(){}` stubs that never instantiate the class — so it is **3 of 6 affected, not 6**. Neo added the second-order trap: a secondary ctor delegating through `MongoConnectionResolver.Resolve` still fails at *runtime*, because those mocks stub only `GetSection("MongoDB")` while the resolver uses indexer lookups. **Resolution: the legacy `IConfiguration` ctor body must survive verbatim; the `IMongoClient` path is additive.** ARCHITECTURE.md §3.3 corrected.

### Hidden ordering dependencies the graph missed

1. **T12 must follow T02/T03/T07** (Bolt). The ADR must record what was built, not what was proposed — T01 already proved the proposal moves under contact. Neo initially argued the reverse from CONSTITUTION §9 ("packages need discussion before adding") and **withdrew**: §9's intent is satisfied by the approved PRD, plan, and ARCHITECTURE.md's verified version table, all of which enumerate every package. Edges added: `T12 → T02, T03, T07`.
2. **T06 must follow T08** (Pulse). T06's CI step builds `AgendaBuddy.AppHost/AgendaBuddy.AppHost.csproj`, a project T08 creates. As sequenced (T06 depends only on T02), T06 could land first and the step would fail on a missing path. Edge added: `T06 → T08`.

`tasks.cjs check` clean after both updates — no cycle.

### Constraints this wave imposes on T04/T05/T08

- **`ResolveSetting`'s signature is the wave's real contract and is currently elided** as `/* … */` at `ARCHITECTURE.md:214` (Echo, Bolt). Bolt showed a single naming convention cannot work: Identity reads `MongoDbSettings:CollectionName` (`Identity/Extensions/ServiceCollectionExtension.cs:12`) while others read per-entity names such as `ProvidersCollection` and `ProfessionsCollection` (`Profession/Extensions/ServiceCollectionExtensions.cs:14`). T03 must accept a per-call `name`, or T05 breaks for Identity and Profession. Pin before T04 writes tests.
- **`MongoHealthCheck`'s 2–5s cache (threat T-002) is specified nowhere and tested by nobody.** `ARCHITECTURE.md` §3.5 shows a bare ping. Bolt flagged that a static/instance field is non-deterministic under xUnit's parallel execution; Echo gave the cheapest honest test: Moq call-count (`Times.Once()` across two calls inside the window), **not** wall-clock, to avoid the timezone-flake pattern already on record (`11-testing.md:105`).
- **T07 is not "add an optional ctor param."** Bolt read the code: the hardcoded `localhost:9092` is a local `AdminClientConfig` initializer inside `CreateTopicIfNotExist` (`Kafka/KafkaClient.cs:8-13`), not a class member. It needs a primary-constructor conversion plus a readable `BootstrapServers`; if that stays `private`, T04 cannot assert the resolved value without a live broker. Needs `internal` + `[InternalsVisibleTo("Kafka.Tests")]`.
- **`Profession`'s seeding blocks T05** (Bolt). `Profession/Extensions/ServiceCollectionExtensions.cs:9-25` calls `SeedDataAsync(database, configuration).Wait()` — sync-over-async at DI-registration time, where no `IServiceProvider` exists to resolve the new singleton client from. T05 must move it to a post-`builder.Build()` step or a hosted service. This is a real design decision hiding inside a task that reads as a mechanical refactor; it appears in neither ARCHITECTURE.md nor T05's text.

### Test-ownership double-claims (Echo)

- The "named-key failure" exception-message test is claimed by both `F-013-T03.md` and `F-013-T04.md`.
- The KafkaClient AC-5.5 test is claimed by both `F-013-T04.md` and T07. `Kafka.Tests/KafkaClientTest.cs:8-11` is an empty `METHOD(){}` stub, so this is greenfield authoring — and since AC-5.2 forbids editing existing tests, it must be a **new** method, not a filled-in stub.

**Assigned:** T03 owns the resolver/health-check unit tests in `Library.Tests`. T04 owns per-service resolution tests and the new Kafka test file. T07 ships the production change only.

### The test gate is misstated in the plan (Echo, Pulse, Neo)

Measured baseline on this branch: **189 passed, 0 failed across 10 projects.** `MobileApp.Tests` cannot run — `MobileApp` fails to compile under `/p:MobileWorkloads=false` with `error CS0103: The name 'Application' does not exist` at `MobileApp/ViewModels/CustomersViewModel.cs:125`, **pre-existing on main** (this branch has zero code changes). The plan's "all 256 must pass unmodified" and `F-013-T10.md`'s "confirm all 256 tests pass" are therefore unattestable as worded. Restated as **189 runnable, unmodified**, with the MobileApp exclusion named. (Counts for `MobileApp.Tests` disagree between sources — 67 in `11-testing.md:16-27`, 63 in the F-012 handoff — unverifiable while the project does not build.)

Pulse confirmed T06's AppHost build guard **is** safe as written, because it scopes to the AppHost project rather than the solution; the pre-existing solution-wide `build-and-test` step (`.github/workflows/dotnet.yml:83-87`) already passes that flag and already tolerates the MobileApp failure. The CS0103 defect should be filed separately, not absorbed into F-013.

### Other Pulse findings

- `.github/workflows/dotnet.yml:48` already includes `'*.sln'` in the `api` filter, so T08's solution edit flips `api=true` on its own — no gap there.
- `AgendaBuddy.AppHost/**` and `AgendaBuddy.ServiceDefaults/**` are in **no** filter until T06 lands, so until then a change touching only those directories runs zero CI jobs — the same silent-merge class already affecting `global.json` and the Dockerfiles.
- T02's package set needs no `Directory.Build.props` CVE pins (those are Mongo / AspNetCore.OpenApi-only, `:18-28`), so zero new pin surface.
- `AddOtlpExporter` must stay behind a `!IsNullOrWhiteSpace(config["OTEL_EXPORTER_OTLP_ENDPOINT"])` guard so CI (no AppHost, no collector) neither throws nor exports — confirm in T02 review.
- No CI runner needs Docker for any F-013 job; the container runtime is a dev-machine and T10-manual-verification requirement only. T11 already covers documenting the Docker-not-running failure (E-3).

---

## Cross-talk

**Not required.** One genuine contradiction arose (T12 ordering) and was resolved by Neo withdrawing in Bolt's favor. All other findings were complementary, and the wave's headline defect was reached independently by Neo and Echo — consensus on arrival. Exited early per the bounded-loop rule.

---

## Wave Execution Plan

**Confirmed safe in parallel:** F-013-T02, F-013-T03, F-013-T07 — disjoint files, no shared fixtures, no runtime interference.

**Resequenced:** F-013-T12 now blocked behind T02/T03/T07. F-013-T06 now blocked behind T08.

**Recommended order:** T03 first (its public surface is the contract T04 and T05 bind to), then T02, then T07. T12 last in the wave.

**Dependency updates applied:** `T12 → T02`, `T12 → T03`, `T12 → T07`, `T06 → T08`. Store integrity clean.

**Artifact corrections owed before Wave 3:** ARCHITECTURE.md §3.3 ctor claim (done); `ResolveSetting` signature pinned; `MongoHealthCheck` caching design + its test; T07 test-visibility; T05's Profession seeding decision; T10's "256" → "189".

**Filed outside F-013:** the `MobileApp` CS0103 compile failure.
