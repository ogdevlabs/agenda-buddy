# PRD: API Refactor Foundations
<!-- pdlc-template-version: 2.4.0 -->

**Date:** 2026-08-18
**Status:** Approved
**Feature slug:** api-refactor-foundations
**Episode:** <!-- assigned after delivery -->

---

## Overview

F-018 is stage 1 of 3 in the API refactor program (F-018 → F-019 → F-020). It builds an integration-test harness, clears two long-deferred mechanical blockers, and records the program's architectural decisions — **without touching a single endpoint**.

It exists because of ordering. The endpoint layer has ten evidenced defects that require rewriting all seven services, and the repository currently has **zero integration tests**. Episode 001 concluded that both of F-013's real defects were invisible to code review and surfaced only by *running* the software. Rewriting every endpoint with only unit tests underneath would repeat that mistake at larger scale.

This serves INTENT.md's "Test coverage across all services > 80%" success measure and makes CONSTITUTION §5's "all integration tests pass" satisfiable for the first time — it has been an unsatisfiable line in the Definition of Done since initialization.

---

## Problem Statement

Two problems, one immediate and one structural.

**The immediate problem: there is no safety net for the refactor that follows.** The repository has 379 tests and **not one of them exercises a real HTTP request against a real database**. `AppHostWiringTest` asserts the Aspire application *model*, not a running system. Worse, **no test asserts that an EventStore audit event is written** — CONSTITUTION §3 mandates an audit event per command and says "do not remove this pattern", yet F-019 and F-020 could delete the audit trail entirely and CI would stay green. A wrong `[BsonElement]` mapping is equally invisible, because every repository is mocked.

**The structural problem: two blockers have been deferred so long their justifications went stale.** CONSTITUTION §9 forbids renaming `EventAndCommands/Persitency/` "until a dedicated refactor is planned", asserting the rename "breaks existing references across all consumers". Measured on 2026-08-18: **11 files, one reference each, and zero coupling in any `.json`, `.yml`, `.csproj` or `.slnf`**. The prohibition outlived its reason. Separately, §1 still describes the project as .NET 8 when it has been `net10.0` since F-011, and §4 mandates `MiniValidator` while the program intends to adopt `Validot`.

The cost of leaving these is compounding: F-014, F-015 and F-016 all have to touch the endpoint layer, and each one that lands before the refactor is work that gets done twice.

---

## Target User

**This repo's developers** — currently one maintainer working with AI agents. This is developer-facing infrastructure; no provider or customer behaviour changes, and nothing ships to either persona.

The concrete downstream consumers are **F-019** and **F-020**, which cannot proceed safely without the harness, and **F-016**, whose authorization work will be the first real beneficiary of a token factory that can mint tokens for arbitrary subjects.

Relative to INTENT.md's personas: no effect on the Independent Service Provider or their customers, except indirectly — F-016's PII fix ships before the rewrite specifically so customer data is not left exposed across a long refactor.

---

## Requirements

**Harness core**

1. The system MUST provide a Testcontainers-backed integration-test harness in a **single** project, `AgendaBuddy.IntegrationTests`. Seven per-service test projects MUST NOT be created — episode 001 already recorded the seven near-identical `ServiceCollectionMongoResolutionTest.cs` files as debt.
2. The harness MUST support three assertion tiers: **route contract** (real HTTP through the real pipeline returns the expected status), **persistence round-trip** (a write followed by a read returns the written data against a real MongoDB), and **audit fired** (the command persisted an EventStore event).
3. The harness MUST start **one container per test class**, with **isolation preserved by a unique database name per test** inside that shared container. *(Amended at Design, 2026-08-18: Discover chose container-per-**test** against an assumed 1–3 s startup; the pre-Design spike measured **4.45 s** (4436/4471/4475 ms), roughly 2–3× the estimate. The decision was reversed on that measurement. One `WebApplicationFactory` per test class follows, since the connection string is then known once.)*
4. The harness MUST start MongoDB for every test class that needs persistence. It MUST **NOT** start Kafka: `IKafkaClient` is substituted with a recording fake via `ConfigureTestServices`, asserting the topic-creation call. *(Amended at Design: Kafka here only creates topics — nothing is produced or consumed — so a real broker adds the slowest container in the suite while proving almost nothing. The wiring is still asserted.)*

**The three hard prerequisites** *(nothing runs until all three work)*

5. All seven service projects MUST expose their entry point to the test project via `<InternalsVisibleTo Include="AgendaBuddy.IntegrationTests" />`. `Program` is currently internal because the services use top-level statements, and no service `.csproj` has this.
6. The harness MUST generate an **RSA keypair once per test session, in memory**, set `JWT_PUBLIC_KEY` before any host is built, and **never write key material to disk**. `AddAgendaBuddyAuthentication()` throws `ApplicationException` without it.
7. The harness MUST inject the Testcontainer MongoDB connection string via configuration as `ConnectionStrings:mongodb`, with no change to production code.

**Token capabilities**

8. The harness MUST provide a token factory that mints RS256 tokens with issuer `agenda-buddy-identity`, and that can produce **(a)** a valid token for a given subject, **(b)** an **expired** token, and **(c)** a token for an **arbitrary** subject. Validation uses `ClockSkew = TimeSpan.Zero`, so expiry is testable only if the factory can backdate.

**Diagnostics and robustness**

9. When the Docker daemon is unreachable, the harness MUST fail with an actionable message naming Rancher Desktop and the `~/.rd/bin` PATH requirement — not a raw Testcontainers stack trace.
10. Container image pull failures MUST be surfaced as **infrastructure** errors, distinct from assertion failures.
11. Container images MUST be **pinned by tag** so a pull is reproducible.
12. Containers MUST be reaped automatically after an abnormal exit (Ctrl-C, CI timeout), verified by killing a run mid-flight.
13. The harness SHOULD detect an already-running Aspire AppHost (or its containers) and warn before competing for a 2-CPU / 4.1 GB VM.

**Guarding the §3 invariant**

14. A **permanent guard test** MUST assert that the EventStore write is reachable on the command path, so a forgotten mutation-check restore cannot silently delete the invariant. The manual red/green mutation check is performed **once** as recorded evidence; the guard test is what survives.

**The rename**

15. `EventAndCommands/Persitency/` MUST be renamed to `Persistence/`, with the namespace and all 11 references updated, as its **own commit**, and **before any integration test is authored** — so no test is written against the misspelled namespace.

**OpenAPI contract baseline**

16. The system MUST generate an OpenAPI spec per service through a **deterministic, environment-independent** mechanism. **Amended at the Step 12 gate: the specs are generated and drift-checked in CI but NOT committed** until F-016 closes the anonymous full-record `GET /api/v1/providers` endpoint — threat **T-003**. Committing them becomes an **F-016 exit criterion** so the deferral cannot be forgotten. *(Mechanism spike-proven: `ISwaggerProvider` resolved from host DI — no HTTP, no Development override, no sixth package.)*
17. Spec generation MUST **fail loudly** if any service cannot boot, and MUST NOT write an empty or partial spec over a good one.
18. CI MUST regenerate the spec and **fail on any diff** against the committed copy.

**CI**

19. Integration tests MUST run on **every PR** as a **separate CI job** from the backend unit job.
20. A red integration job MUST **block the PR from its first run**. The "10 consecutive green runs" count governs only *when CONSTITUTION §7 is formally amended*, never whether failures may be ignored.
21. The integration job MUST report its wall-clock duration and fail or warn explicitly above **10 minutes**.
22. The three existing mobile CI jobs (`build-android`, `build-ios`, `build-mobile-tests`) MUST be confirmed passing on a real run, and the project's headline test count MUST be reported as **379** (305 backend + 74 mobile).
23. The "10 consecutive green runs" milestone MUST have a **named owner and a durable counter**, not just a definition. F-018 creates a beads issue — *"Amend CONSTITUTION §7 to require the integration gate (after 10 consecutive green runs)"* — assigned to the maintainer, whose notes field records the running count and the run URLs. Without this, "stable" is as unenforceable as the undefined version it replaced, which is the specific defect this requirement exists to close.

**Governance**

24. CONSTITUTION MUST be amended: §1 (.NET 8 → `net10.0`), §4 (`MiniValidator` → `Validot`), §9 (the five packages approved; the stale `Persitency` prohibition removed).
25. ADR-014 through **ADR-020** MUST be recorded: programme shape + MediatR retention (014), the five packages (015), Validot replacing MiniValidator (016), the Testcontainers harness incl. the container-per-class reversal (017), tag-not-digest pinning (018, accepted risk), `InternalsVisibleTo` (019, accepted risk), and OpenAPI generated-not-committed (020). *(Grew from four to seven at the Step 12 gate — the threat model produced three deferral/acceptance decisions, each of which the skill requires be recorded as an ADR.)*
26. The system SHOULD adopt an `.editorconfig` and enforce it in CI. The v0.1.0 ship fixed 69 whitespace findings with nothing preventing their return, and F-019/F-020 rewrite every endpoint file — formatting drift during a large refactor is noise that hides real changes in review. *(Promoted from MAY: a MAY with no acceptance criterion is a requirement that never happens.)*
27. `Identity/Program.cs`'s comment claiming the process-wide Mongo client is "shared by the repositories and the EventStore" MUST be corrected — Identity registers no EventStore. Comment-only, no behaviour change; found while verifying AC-7.

---

## Assumptions

- **Testcontainers can be made to work against Rancher Desktop.** This is the load-bearing assumption of the entire feature and is currently **unverified** — Rancher's socket is `~/.rd/docker.sock`, not the Docker Desktop default. If false, F-018's approach needs rethinking, not patching.
- **`WebApplicationFactory` can host these services once `InternalsVisibleTo` is added.** The services use top-level statements with local functions; nothing else is assumed to block hosting.
- **GitHub `ubuntu-latest` runners provide a usable Docker daemon** for Testcontainers with no additional runner setup.
- ~~**Query handlers persist audit events, not only command handlers.**~~ **VERIFIED 2026-08-18 — no longer an assumption.** `CheckCalendarAppointmentsQueryHandler.cs:28,40` and `CheckCalendarAvailabilityQueryHandler.cs:28,42` both call `eventStore.SaveAsync` on the success *and* failure paths, as do the Customer and Provider query handlers. This is what makes tier 3 applicable to the read-only `Calendar` service.
- ~~**All seven services have an audit trail.**~~ **DISPROVEN 2026-08-18.** `Identity` registers `AddEventStore` **zero** times (each of the other six registers it once) and uses its own `IdentityDb`. Tier 3 is inapplicable to Identity — see AC-7. This corrected a factual error in the first draft of this PRD, which claimed tier 3 applied "for each service".
- **A deterministic OpenAPI generation path exists** without adding a sixth dependency. If `Microsoft.Extensions.ApiDescription.Server` turns out to be required, that is a new package needing its own decision.
- **The five approved packages are compatible with `net10.0` and with `MongoDB.Driver` pinned at 2.25.0.** F-013 was bitten by exactly this class of assumption (`Aspire.MongoDB.Driver` required driver ≥ 3.9.0 and failed restore with `NU1605`).
- **No test currently depends on the `Persitency` spelling** beyond the 11 measured references.

---

## Acceptance Criteria

1. `AgendaBuddy.IntegrationTests` exists as a single project and runs at least one passing test that issues a real HTTP request through a real service host. 🧪 test-first
2. All seven service projects declare `<InternalsVisibleTo Include="AgendaBuddy.IntegrationTests" />`, and a `WebApplicationFactory`-hosted test boots each of the seven without throwing. 🧪 test-first
3. An RSA keypair is generated once per test session in memory; `grep` finds no PEM or private-key material in any tracked file. 🧪 test-first
4. The MongoDB connection string reaches each host as `ConnectionStrings:mongodb` with **zero changes to production source**. 🧪 test-first
5. **Tier 1 — route contract:** each of the seven services has a test asserting a real request returns the expected HTTP status. 🧪 test-first
6. **Tier 2 — persistence round-trip:** for each of the six services with write endpoints (Booking, Customer, Provider, Services, Profession, Identity), a write followed by a read returns the written data from a real MongoDB. `Calendar` satisfies this by seeding then reading. 🧪 test-first
7. **Tier 3 — audit fired:** for each of the **six** services that register `AddEventStore` (Booking, Calendar, Customer, Provider, Services, Profession), a test asserts the expected EventStore event was persisted. **`Identity` is excluded — it has no audit trail** (0 occurrences of `AddEventStore` versus 1 in each of the other six, and it uses its own `IdentityDb`). Tier 3 is inapplicable to it, not merely unwritten. 🧪 test-first
8. `Identity` has **tier 1 and tier 2** coverage across all five of its write endpoints (`/register`, `/login`, `/refresh`, `/logout`, `/device-token`) — route contract and persistence round-trip, not route-contract alone. Tier 3 is excluded per AC-7. 🧪 test-first
9. The token factory produces a valid token, an **expired** token that yields 401, and a **foreign-subject** token that yields 403 on an ownership-guarded route. 🧪 test-first
10. With the Docker daemon stopped, the suite fails with a message naming Rancher Desktop and the `~/.rd/bin` PATH requirement. 🧪 test-first
11. A simulated image-pull failure is reported as an infrastructure error, distinguishable from an assertion failure. 🧪 test-first
12. Every container image is pinned by explicit tag; no `:latest` appears in the harness. 🧪 test-first
13. A run killed mid-flight leaves **zero** orphan containers, verified by `docker ps` after a deliberate kill. 🧪 test-first
13b. Tests within a class are isolated from one another by a **unique database name per test**, demonstrated by two tests in the same class writing the same document id without collision. 🧪 test-first
14. The harness warns when an Aspire AppHost is already running. 🧪 test-first
15. A permanent guard test fails when the EventStore write is removed from the command path, and the one-time mutation red/green is recorded as evidence in the verification document. 🧪 test-first
16. `EventAndCommands/Persistence/` exists, `git grep Persitency` returns **zero** matches in tracked source, all 379 tests pass, and the rename is an isolated commit that precedes the first integration test commit. 🧪 test-first
17. An OpenAPI spec is **generated** for each of the seven services by a documented command that does not require manually starting the app in `Development`, and is published as a **CI artifact**. It is **not** committed to the repository in F-018 (threat T-003; committing is an F-016 exit criterion). 🧪 test-first
18. Spec generation exits non-zero and produces **no** spec artifact when a service cannot boot — it never emits an empty or partial spec. 🧪 test-first
19. CI fails when a route changes without the spec being regenerated — demonstrated by deliberately changing one route and observing the failure. Because the spec is not committed in F-018, the drift baseline is the **previous run's artifact** (or a checked-in hash manifest rather than the spec body). **Verified on a throwaway branch (see the CI-verification note below), not on `main`.** 🧪 test-first
20. A separate CI job runs the integration tests on every PR, and a deliberately failing integration test blocks the PR on its first run. **Verified on the same throwaway branch.** 🧪 test-first
21. The integration job prints its wall-clock duration, and exceeding 10 minutes produces an explicit failure or warning rather than passing silently. 🧪 test-first
22. `build-android`, `build-ios` and `build-mobile-tests` are confirmed green on a real CI run, and the headline test count is reported as 379. **Verified on the same throwaway branch.** 🧪 test-first
23. CONSTITUTION §1, §4 and §9 are amended, and **ADR-014 through ADR-020** exist in DECISIONS.md. 🧪 test-first
24. All 379 pre-existing tests still pass, and **no test file has been deleted**. Test files **may be modified only** by the `Persistence` namespace rename (which touches six `GlobalUsings.cs` files); no test body, assertion, or `[Fact]`/`[Theory]` is changed, removed, or skipped. 🧪 test-first
25. `Identity/Program.cs`'s comment claiming the shared Mongo client is "shared by the repositories and the EventStore" is corrected — Identity has no EventStore. Comment-only; no behaviour change. 🧪 test-first
26. An `.editorconfig` exists at the repo root, and `dotnet format agenda-buddy-backend.slnf --verify-no-changes` passes against it in CI. 🧪 test-first
27. A beads issue exists titled *"Amend CONSTITUTION §7 to require the integration gate (after 10 consecutive green runs)"*, assigned to the maintainer, with a notes field recording the running green-run count. The count is therefore tracked somewhere durable rather than in someone's memory. 🧪 test-first

> ### CI-verification note (AC-19, AC-20, AC-22)
>
> These three cannot be proven without a real CI run, and `main` is PR-protected with direct pushes disallowed. They are verified on a **short-lived throwaway branch pushed by the maintainer on request**: PDLC prepares the exact commits and commands, the maintainer pushes the branch and opens a PR, the three behaviours are observed on that PR, evidence is recorded in the verification document, and the branch is deleted. **Nothing lands on `main` outside a reviewed PR.**
>
> This is deliberately *not* downgraded to "the commands pass locally" — running the command locally proves the command works, not that CI is wired to run it. That distinction is precisely what let F-013's CI credential guard sit unexecuted until it first failed on PR #35.

---

## User Stories

**F-018-US-01: A service boots under test**
*Acceptance criteria: 1, 2, 3, 4*
Given the seven services expose their entry point to the test project
And an RSA keypair has been generated in memory for this test session
When a `WebApplicationFactory` builds any one of the seven service hosts
Then the host starts without throwing
And it resolves its MongoDB connection from the Testcontainer
And no key material has been written to disk

**F-018-US-02: A regression in persistence mapping is caught**
*Acceptance criteria: 5, 6*
Given a real MongoDB container is running for this test
When a test posts an appointment and then reads it back over HTTP
Then the returned data matches what was written
And a wrong `[BsonElement]` mapping would fail this test

**F-018-US-03: The audit trail cannot be silently removed**
*Acceptance criteria: 7, 15*
Given CONSTITUTION §3 requires an audit event for every command
When the EventStore write is removed from the command path
Then the guard test fails
And a developer who forgets to restore a mutation cannot merge the deletion

**F-018-US-04: Auth failure paths are testable**
*Acceptance criteria: 8, 9*
Given the token factory can mint expired and foreign-subject tokens
When a request presents an expired token
Then the service responds 401
And when a request presents a token for a different user against an ownership-guarded route
Then the service responds 403

**F-018-US-05: Infrastructure failure is never mistaken for a test failure**
*Acceptance criteria: 10, 11, 12, 13, 14*
Given a developer runs the integration suite with Rancher Desktop stopped
When the harness cannot reach the Docker daemon
Then it fails with a message naming Rancher Desktop and the `~/.rd/bin` PATH requirement
And when a run is killed mid-flight
Then no orphan containers remain

**F-018-US-06: The API contract is visible in review**
*Acceptance criteria: 17, 18, 19*
Given an OpenAPI spec is committed for each service
When a developer changes a route without regenerating the spec
Then CI fails on the spec diff
And when a service cannot boot during generation
Then generation exits non-zero and leaves the committed spec untouched

**F-018-US-07: The net is enforced, and its cost is visible**
*Acceptance criteria: 20, 21, 24*
Given integration tests run as a separate CI job on every PR
When an integration test fails
Then the PR is blocked from the first run, not after ten green runs
And the job reports its wall-clock duration
And exceeding ten minutes surfaces explicitly rather than passing silently

**F-018-US-08: The deferred cleanups land safely**
*Acceptance criteria: 16, 22, 23, 24, 25, 26*
Given `Persitency` is a known typo whose rename prohibition has expired
When the rename lands as an isolated commit before any integration test is authored
Then `git grep Persitency` returns zero matches in tracked source
And all 379 tests still pass with no test file deleted and no test body altered
And CONSTITUTION §1, §4 and §9 reflect reality
And an `.editorconfig` prevents the whitespace drift returning during F-019/F-020
And `Identity/Program.cs`'s comment no longer claims an EventStore that does not exist

**F-018-US-09: The gate promise is trackable**
*Acceptance criteria: 27*
Given "stable" was defined as 10 consecutive green integration runs
When F-018 ships
Then a beads issue owned by the maintainer records the running count and run URLs
And the §7 amendment is triggered by a tracked number rather than by someone remembering

---

## Testing Approach: Test-Driven Development (TDD)

**Tests are written first.** During Construction (`/build`), for **every acceptance criterion above**, a **failing test is written and run before any implementation code** — the Red → Green → Refactor cycle:

1. **Red** — write the smallest failing test that pins the acceptance criterion, named with the Given/When/Then language from the matching user story. Run it; confirm it fails for the right reason (logic not implemented — not a syntax/import error).
2. **Green** — write the minimum implementation that makes the test pass. Run the test and the full suite; no regressions.
3. **Refactor** — clean up without changing behavior; suite stays green.

The build loop enforces this at a mandatory **TDD gate** (build Step 9a-bis): implementation code for a criterion may not be written until a failing test for it exists. The only exceptions are pure scaffolding, config-only, and infrastructure-only work — and even those require an **explicit human TDD override**. There is no silent skip. (TDD can be disabled only by editing `CONSTITUTION.md` § Test Gates — the Constitution always wins.)

**Security acceptance criteria are enforced mechanically (issue #55).** Any `[security]`-tagged criterion above (threat-derived, materialized on its task via `tasks.cjs ac add`) is not just governed by the prose gate: `node scripts/tasks.cjs done` **structurally refuses** to close a task whose `[security]` AC has no linked test. Name each security test after its threat id (`test_TNNN_…`) and link it with `tasks.cjs ac link-test`. This makes it impossible to close a threat mitigation on a citation alone.

> **A note specific to this feature.** F-018 is unusually infrastructure-heavy, which is exactly the category the TDD gate allows an override for. That exemption should be used sparingly here: several acceptance criteria (10, 11, 13, 15, 18, 19, 21) are *themselves* tests of failure behaviour, and writing them after the fact would defeat their purpose. AC-15 in particular is a red-then-green proof by construction — it is meaningless unless the red half is observed first.

**Test layers** for this feature (per CONSTITUTION §7): **Unit** (existing gate) + **Integration** (introduced by this feature; becomes a §7 gate after 10 consecutive green runs) + **Security scan** (§7 standing requirement, still owned by F-017 — F-018 does not discharge it).

---

## Non-Functional Requirements

- The integration CI job MUST complete in **under 10 minutes**. *(Now expected to be comfortable rather than marginal: the container-per-class reversal cuts startup cost from ~4.45 s per test to ~4.45 s per test class. The threshold stays as a tripwire — AC-21 — because F-019 adds substantially more tests.)*
- The harness MUST NOT write private key material to disk at any point. The Atlas credential incident is still unremediated; a committed test keypair would be a second secret-shaped artifact and would trip F-017's future scanner.
- The harness MUST run on a **2 CPU / 4.1 GB** VM that is already running a Kubernetes cluster. This is the real local constraint, not a theoretical one, and it is what made the measured 4.45 s container startup decisive rather than academic.
- **Tier 3 assertions MUST read the persisted document directly with `MongoDB.Driver`**, not through `IEventStore`. F-019/F-020 refactor that abstraction, so an assertion routed through it could pass while the persisted data is wrong.
- **`AgendaBuddy.IntegrationTests` MUST NOT be added to `agenda-buddy-backend.slnf`.** It is invoked by path in its own CI job, so the unit job stays fast and container-free by structure rather than by remembering a `--filter` flag.
- Diagnostic messages for infrastructure failure MUST name the specific remedy (start Rancher Desktop; export `PATH="$HOME/.rd/bin:$PATH"`), because a developer hitting this will otherwise read it as a broken test.
- The rename MUST be behaviour-preserving: no collection name, configuration key, or serialized document changes.
- Generated OpenAPI specs MUST be deterministic — regenerating without source changes MUST produce a byte-identical file, or the CI drift check (AC-19) produces false failures. **The spike verified stable path *sets* only, not full-document byte stability; that still needs its own verification** and is the same "reasoned, not observed" trap that made threat T-004 wrong in F-013.
- No production code path may change behaviour. F-018 adds `InternalsVisibleTo`, renames a namespace, and adds tests, CI and docs. Any behavioural change is out of scope and belongs to F-019.

---

## Out of Scope

- **All endpoint-layer changes** — the handler abstraction, `FluentResults`, DTOs, `Mapster`, `Validot` wiring, `SmallApiToolkit`'s `DataResponse<T>` and `ExceptionMiddleware`, and removing `RequestCollection`. These are **F-019** (pilot: `Booking`) and **F-020** (remaining six). F-018 *approves* the packages in §9 and records the ADRs; it does not use them in production code.
- **Broad endpoint test coverage.** F-018 delivers roughly one test per tier per service — a smoke test proving the harness works, explicitly **not** a regression net. Building the net is F-019's job, and F-019's plan must size it against the container-startup arithmetic.
- **Extracting shared abstractions into a common project** — an F-020 decision, once F-019 shows what actually generalises.
- **Container/CD hardening and the §7 security-scan gate** — remains **F-017**. F-018's manual scan at the v0.1.0 ship did not discharge it.
- **Testing a 401 by manipulating system time.** Handled by minting a backdated token instead (AC-9); no clock abstraction is introduced.
- **Docker image caching beyond tag pinning** — only revisited if the 10-minute budget is approached.
- **Fixing the six unreachable capabilities, the mobile contract, or the PII exposure** — F-014, F-015, F-016 respectively.

---

## Known Risks

- ~~⚠️ **Testcontainers on Rancher Desktop is unverified**~~ — **RESOLVED by spike, 2026-08-18.** Testcontainers 4.14.0 discovers the active `rancher-desktop` docker context with **zero configuration**, despite `DOCKER_HOST` being unset, no `~/.testcontainers.properties`, and `/var/run/docker.sock` being absent. A real insert-and-read round-trip through MongoDB.Driver 2.25.0 passed. **However the spike surfaced a worse-than-assumed cost:** warm MongoDB container startup is **4.45 s** (4436/4471/4475 ms, σ≈20 ms), not the 1–3 s assumed. At F-018's ~20 tests that is ~89 s — comfortable. At F-019's 60–100 tests it is **4.5–7.4 minutes of pure container startup** before test logic, on two contended CPUs, with Kafka slower still. Container-per-test therefore stands for F-018 and will very likely breach the budget at F-019, where AC-21's tripwire fires.
- ~~⚠️ **No OpenAPI generation mechanism has been identified.**~~ — **RESOLVED by spike, 2026-08-18, with no new dependency.** `AddSwaggerGen()` is registered **unconditionally** (`Booking/Program.cs:48` — only `UseSwagger()` is Development-gated), so `ISwaggerProvider` is always in DI and the document can be resolved straight from a booted host: **no HTTP call, no Development override, and no `Microsoft.Extensions.ApiDescription.Server`.** Output is deterministic across consecutive generations, and complete (Booking yields 1 path / 3 operations with operation IDs). **Spec generation needs no containers**, so AC-17 and AC-18 are decoupled from the harness and can proceed in parallel rather than behind it. Note `/health` and `/alive` are absent from the spec — health-check endpoints are not API-explorer visible, which is expected.
- **F-018 has no rollback story.** It edits seven production `.csproj` files and renames a namespace. If the Testcontainers spike fails after those have merged, the cleanup is manual. Deferred because both changes are individually trivial to revert and are landing as isolated commits.
- **The cache-aside invariant gets no guard while the audit invariant gets two.** §3 protects both equally ("do not bypass"), and F-019/F-020 can break cache-aside just as silently. Deferred because the audit trail is the higher-consequence invariant (it is the system's only audit record) and because cache-aside failure degrades performance rather than losing data. **Should be revisited in F-019.**
- **Seven MobileApp tests are skipped and nobody knows why** — 372 of 379 actually execute. Deferred because it is a pre-existing condition, but a skipped test inside a regression baseline is a silent gap. Cheap to investigate; should not reach Construction unexamined.
- **Container-per-test may prove incompatible with the 10-minute budget at useful coverage.** At F-018's thin coverage (~20 tests) the arithmetic is comfortable; at F-019's real coverage (60–100 tests) it is 2–4 minutes of pure container startup on two contended CPUs. Accepted knowingly, with AC-21 as the tripwire.
- **Conflict B's resolution is softer than it sounds.** "Assert behaviour, not envelopes" assumes we know what F-019 preserves. If F-019 changes `Created` → `Ok` with an envelope, the status-code assertions break too — and F-019's design does not exist yet.
- **The five packages are unproven against `net10.0` + `MongoDB.Driver` 2.25.0.** F-013 lost a task to exactly this class of assumption. F-019 should front-load a restore check before building on them. *(Partially de-risked: `Testcontainers.MongoDb` 4.14.0 restores and runs cleanly against `net10.0` + driver 2.25.0. Its transitive SharpCompress/Snappier CVEs are **already pinned** solution-wide in `Directory.Build.props`, so no new pins are needed — an initial reading of the spike warnings as a new finding was a false alarm from a throwaway project that lacked those pins.)*

---

## Standards Alignment

**MUST (enforced):** none identified — no analysis ran.

**SHOULD (noted):** not assessed.

**Reference:** none produced. The `nordstrom-standards-readiness` plugin is installed, but its six `.nordstrom-standards/*` source repositories do not resolve under this machine's auth (probed 2026-08-18: `engineering`, `security`, `privacy` all unreachable) and no local checkout exists. Step 6.5's enforcement tier is `advisory`, so this **skipped with notice — it is not an override**. The Plan gate (Step 18.5, `--design`) will re-attempt and is expected to skip identically.

**Bodies assessed:** none.

> Separately worth settling: Agenda Buddy is a personal project under `fererelabs`, not a Nordstrom system, so these six standards bodies may not apply at all. Deciding that deliberately would be better than letting the gate fail silently on every feature. Tracked as a `/diagnose` follow-up.

---

## Design Docs

- Architecture: [ARCHITECTURE.md](../design/api-refactor-foundations/ARCHITECTURE.md)
- Data model: [data-model.md](../design/api-refactor-foundations/data-model.md) — *no schema changes; records the schema the harness depends on*
- API contracts: [api-contracts.md](../design/api-refactor-foundations/api-contracts.md) — *no new endpoints; captures the existing surface as a committed artifact*
- Threat model: [threat-model.md](../design/api-refactor-foundations/threat-model.md) — **triage: Full (3/3)** · 1 CRITICAL, 3 HIGH, 3 MEDIUM · **2 open questions for the human**
- UX review: [ux-review.md](../design/api-refactor-foundations/ux-review.md) — **triage: Skip** (no user-facing surface)
- Threat-model MOM: [MOM_threat-model_api-refactor-foundations_2026-08-18.md](../mom/MOM_threat-model_api-refactor-foundations_2026-08-18.md)
- Progressive-thinking MOM: [api-refactor-foundations_progressive-thinking_mom_2026_08_18.md](../mom/api-refactor-foundations_progressive-thinking_mom_2026_08_18.md)

> ⚠️ **The threat model materially changed the risk picture and must be read before approval.** The repository was verified **PUBLIC**, and the Atlas credential is recoverable from **published** history (9 commits from `origin/main`, earliest `ddb23ba`). That raised T-001 to CRITICAL and T-004 from MEDIUM to HIGH. Three threats are "mitigate now" and will be back-written as `[security]`-tagged acceptance criteria at Plan Step 14.5.

---

## Related Episodes

- [Episode 001: aspire-wiring (F-013)](../episodes/EPISODE_aspire-wiring_2026-08-17.md) — the direct motivation for F-018's existence and ordering. Its central conclusion, that "verify the acceptance criteria" must mean *run the thing*, is why the harness precedes the rewrite. It also supplies the environment constraints this feature must live inside (Rancher's off-PATH `docker`, the 2 CPU / 4.1 GB VM, the AppHost's `launchSettings.json` trap) and the still-open Atlas credential risk that makes on-disk test keys unacceptable.

---

## Approval

**Approved by:** ogdevlabs
**Date approved:** 2026-08-18
**Notes:** Approved after a walkthrough that found and fixed five defects in the draft — most importantly AC-7, which claimed the EventStore audit tier applied to all seven services when `Identity` has no audit trail at all. Two conditions attached: (a) AC-19, AC-20 and AC-22 are verified on a short-lived throwaway branch pushed by the maintainer on request — nothing lands on `main` outside a reviewed PR; (b) the Testcontainers-on-Rancher spike and the OpenAPI-generation spike are the first two tasks, because every other acceptance criterion depends on them and neither approach is proven.
