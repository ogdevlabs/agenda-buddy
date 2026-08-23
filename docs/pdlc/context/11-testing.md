# 11 — Testing

> **⚠️ F-016 delta (2026-08-18, `v0.2.0`) — counts and the integration claim refreshed 2026-08-22 at the
> ship gate. Everything else still dates from 2026-08-15.**
>
> **THE INTEGRATION HARNESS NOW EXISTS.** `AgendaBuddy.IntegrationTests` — **99 tests** hosting real services
> over HTTP against a **MongoDB Testcontainer** (container per test class, database per test), with
> `Harness/MongoEndpointGuard.cs` failing the suite **closed** if the resolved endpoint is not this session's
> own container. Every statement below that says "there is no integration test in the solution" describes
> the world before F-016 and is retained only as the reason F-016 built the harness before touching an
> endpoint. Endpoint authz **is** now verifiable end-to-end, which is precisely what the Calendar IDOR
> escaped: 24 unit tests covered `OwnershipGuard` while nothing checked whether a route called it.
>
> **Counts (verified 2026-08-22, on `feat/F-021-identity-hardening`): 623 total, in three suites no single
> command runs.**
> **431** backend across 12 projects (`dotnet test agenda-buddy-backend.slnf`, 0 warnings) · **118**
> integration (`dotnet test AgendaBuddy.IntegrationTests/…csproj`, a **separate command** — the project is
> excluded from the slnf by design, ADR-031, so the unit gate stays Docker-free; **1 m 28 s** against the
> 600 s CI budget) · **74** mobile (67 passing, 7 skipped). Superseded counts, for anyone reading old
> artifacts: 189 → 256 → 305 → 379 → 531 → **623**.
>
> F-021 added **+73** backend and **+19** integration. Two of those are worth knowing about as *kinds* of
> test this suite did not previously have: `TransportSecurityOrderTest` asserts middleware **order** by
> reading the seven `Program.cs` files, because `IApplicationBuilder` exposes no ordered list of registered
> middleware; and `CredentialUpdatePrimitiveTest` exercises `MongoDbRepository<T>`'s **Mongo semantics**
> against the container, which closes for the new primitive the debt F-016 recorded for `GetPagedAsync`.
>
> ⚠️ **`Integration — real services + MongoDB` is not yet a required status check on `main`** — branch
> protection is a GitHub setting, not YAML, so the job can fail and a PR still merge. Until that is set, the
> harness's guarantee rests on habit. §7's Integration checkbox also stays unchecked, gated on 10
> consecutive green runs.
>
> Sources: `docs/pdlc/episodes/EPISODE_secure-public-endpoints_2026-08-18.md`, ADR-030, ADR-031.


**Framework:** xUnit + Moq. **Coverage collector:** `coverlet.collector` 6.0.3 → `XPlat Code Coverage` (Cobertura).
**Gate:** `CONSTITUTION.md` §7 requires unit tests and a security scan; integration, E2E, performance, accessibility, and visual-regression are all unchecked.

**Counts:** 84 test `.cs` files across 11 projects; **256** `[Fact]`/`[Theory]`/`[SkippableFact]` attributes.

> Test **bodies were not read** in this scan — this inventory is derived from file paths, attribute counts, and the production code they exercise. Assertions and mock setups are therefore not characterised; claims about *what* is covered are **Inference** from naming plus the shape of the code under test.

---

## Inventory

| Project | Files | Tests | Targets |
|---|---:|---:|---|
| `Library.Tests` | 16 | **74** | Entities, services, tools, repository, auth extensions |
| `MobileApp.Tests` | 17 | **67** | ViewModels, API services, JWT handler, acceptance |
| `Identity.Tests` | 4 (+4 helpers) | **48** | `IdentityService`, JWT middleware matrix, IDOR, log sanitisation |
| `EventsAndCommands.Tests` | 15 | **15** | One test per command/query handler |
| `Booking.Tests` | 3 | 13 | Appointment lifecycle, `EventsHelper`, Mongo config |
| `Provider.Tests` | 4 | 12 | Onboarding auth, `EventsHelper`, `RequestCollection`, config |
| `Customer.Tests` | 3 | 11 | Onboarding auth, `EventsHelper`, config |
| `Calendar.Tests` | 4 | 8 | Availability schedule, `EventHelper`, `RequestCollection`, config |
| `Profession.Tests` | 2 | 4 | `EventsHelper`, config |
| `Services.Tests` | 3 | 3 | `EventHelper`, `RequestCollection`, config |
| `Kafka.Tests` | 1 | **1** | `KafkaClient` |

Test-project package refs: `Microsoft.NET.Test.Sdk` 17.12.0, `Moq` 4.20.70, `coverlet.collector` 6.0.3, `xunit` 2.8.1–2.9.3 (⚠️ skewed, `07-build.md`). `Identity.Tests` additionally uses `Xunit.SkippableFact` 1.4.13 and `JetBrains.Annotations` 2024.2.0-eap1.

`Identity.Tests` is the only project with purpose-built test infrastructure: `Helpers/InMemoryRepository.cs`, `Helpers/FakeDateTimeProvider.cs`, `Helpers/RsaKeyHelper.cs`, and `Auth/TestCollectionDefinition.cs` (an xUnit collection to serialise tests that mutate the `JWT_*` environment variables).

---

## Conventions

- Naming is inconsistent: `*Test.cs` (singular) in Booking, Calendar, Customer, Profession, Provider, Services, Kafka and most of `Library.Tests`; `*Tests.cs` (plural) in `EventsAndCommands.Tests/ConfigurationLoaderTests.cs`, `Library.Tests/Services/DeviceTokenServiceTests.cs`, and all of `MobileApp.Tests`. No enforced convention.
- Directory structure mirrors the production namespace in every project.
- `GlobalUsings.cs` present in 7 of 11 test projects (absent in `Calendar.Tests`, `Library.Tests`, `MobileApp.Tests`, `Services.Tests`).
- One `[Trait("Category", "Acceptance")]` in the whole solution — `MobileApp.Tests/Acceptance/AuthAcceptanceTests.cs:8`.

---

## Per-area detail

### `Identity.Tests` — the strongest suite (48 tests)

The only area with genuine security-behaviour coverage, and it maps to named threats from the F-001 threat model.

| File | Tests | Covers |
|---|---:|---|
| `Services/IdentityServiceTest.cs` | 21 | Register/login/refresh/logout against `InMemoryRepository` with `FakeDateTimeProvider` for expiry control |
| `Auth/OwnershipGuardIdorTest.cs` | 13 | IDOR — `AssertOwner` / `AssertOwnerAny` / `AssertRole` across the claim matrix |
| `Auth/JwtMiddlewareMatrixTest.cs` | 10 | Token validation matrix: issuer, lifetime, algorithm, signing key |
| `Security/LoginLogSanitizationTest.cs` | 4 | Asserts passwords and bearer tokens never reach log output — the **T-001** mitigation guarded by the comment at `Identity/Program.cs:81-86` |

**Inference:** `FakeDateTimeProvider` exists precisely because `IdentityService` takes `IDateTimeProvider` (`IdentityService.cs:16`) — the only service in the solution designed for time injection. Every other service calls `DateTime.UtcNow` statically, which is why none of them has expiry/timing tests.

~~⚠️ **The `RefreshAsync` delete-then-insert data-loss window is very unlikely to be covered.**~~ **CLOSED by F-021.** It was not covered, and the reason was exactly the one stated: `InMemoryRepository` could not simulate a fault between a read and a write. It now can — `FaultBetweenMatchAndWrite` fires after the filter matches and before any mutation — and the defect it made testable is fixed (`Rotation_WhenTheWriteFaults_LeavesTheCredentialIntact`). **The general lesson stands and is worth keeping: a defect that no test can express is a defect that survives a green suite.** The same gap still applies to any other read-modify-write path in a service whose double lacks a hook.

### `Library.Tests` — broadest, but weighted toward the unreachable (74 tests)

| File | Tests | Note |
|---|---:|---|
| `Entities/CredentialEntityTest.cs` | 13 | |
| `Tools/OwnershipGuardTest.cs` | 11 | ⚠️ duplicates `Identity.Tests/Auth/OwnershipGuardIdorTest.cs` (13) — 24 tests for one 26-line class |
| `Services/ReportingServiceTest.cs` | 8 | ⚠️ see below |
| `Services/NoteServiceTest.cs` | 7 | Unreachable service |
| `Services/PaymentServiceTest.cs` | 7 | Unreachable service |
| `Extensions/AuthenticationExtensionsTest.cs` | 6 | |
| `Services/MessageServiceTest.cs` | 6 | Unreachable service |
| `Services/NotificationServiceTest.cs` | 5 | Unreachable service |
| `Services/DeviceTokenServiceTests.cs` | 4 | |
| `Services/{Booking,Calendar,Provider,Service}ServiceTest.cs` | 1 each | ⚠️ **one test apiece for the four load-bearing services** |
| `Repositories/MongoDbRepositoryTest.cs` | 1 | ⚠️ one test for the class every read and write flows through |
| `Tools/{EnumHelper,SupportTools}Test.cs` | 1 each | ⚠️ one test for `SupportTools`, which contains the availability algorithm |

⚠️ **Coverage is inversely proportional to reachability.** 25 tests cover `NoteService`, `PaymentService`, `MessageService`, and `NotificationService` — four services that are **not registered anywhere and have no HTTP route** (`03-services.md`). Meanwhile `BookingService`, `CalendarService`, `ProviderService`, `MongoDbRepository`, and `SupportTools` — the entire live write and read path — have **one test each**.

⚠️ **`ReportingServiceTest.cs` has 8 tests yet `ReportingService` has two defects that tests should have caught:** `CancelledAppointments` counts `Confirmed` appointments as cancelled (`ReportingService.cs:37-38`), and `EstimatedRevenue` multiplies completed count by the sum of *all* service fees (`:27-30`). **Inference:** the tests assert the implemented formula rather than the intended business rule — they encode the bug.

⚠️ **`ServiceServiceTest.cs` has 1 test for a class whose two methods both `throw new NotImplementedException()`** (`ServiceService.cs:7,12`). The test presumably asserts the throw.

⚠️ **`CacheAside` has no test at all.** No `Library.Tests/Tools/CacheAsideTest.cs` exists, despite `CONSTITUTION.md` §3 mandating it for all read-heavy queries and despite it holding three defects including the timeout-returns-null correctness bug (`04-data-access.md`). This is the most consequential coverage gap in the solution.

### `EventsAndCommands.Tests` — 15 tests, exactly one per handler

Nine command handlers and five query handlers plus `ConfigurationLoaderTests.cs`. **Inference:** one `[Fact]` per handler means the happy path only — no test for the failure branch that writes a `"Failed"` audit event, and none for the string-sentinel `"exception"` prefix contract that six endpoints depend on (`10-error-handling.md`).

⚠️ **`BookCalendarCommandHandlerTest.cs` has 1 test for a handler that is `throw new NotImplementedException()`** (`BookCalendarCommandHandler.cs:7`).

⚠️ **`ConfigurationLoaderTests.cs` is the only reference to `ConfigurationLoader`** — the class is otherwise dead code (`06-configuration.md`). A test keeps a dead class alive and makes it look load-bearing.

⚠️ **No handler test can cover MediatR dispatch**, because dispatch never happens — handlers are constructed by hand (`15-cqrs-and-messaging.md`). The tests construct them the same way, so they faithfully mirror the production path but also cement it.

### Per-service test projects — thin, and mostly one shape

The six domain services plus Kafka contribute 52 tests. The dominant pattern is a `MongoDbConfigurationTest.cs` with exactly **1** test in each of Booking, Calendar, Customer, Profession, Provider, Services — six near-identical tests for six near-identical 9-line classes.

Genuine coverage clusters:
- `Booking.Tests/Lifecycle/AppointmentLifecycleTest.cs` — 9 tests, F-004's lifecycle rules.
- `Provider.Tests/Auth/ProviderOnboardingAuthTest.cs` — 9 tests, F-002.
- `Customer.Tests/Auth/CustomerOnboardingAuthTest.cs` — 6 tests, F-003.
- `Calendar.Tests/Availability/AvailabilityScheduleTest.cs` — 5 tests, F-005.

⚠️ **`Calendar.Tests/Availability/AvailabilityScheduleTest.cs` (5 tests) does not catch the availability defects.** `SupportTools.GetThirtyDaysCalendarAvailability` mixes `DateTime.Today`/`DateTime.Now` (local) with UTC-persisted appointments, and its `aux = 19 - aux` today-offset arithmetic conflates a duration with a clock hour (`04-data-access.md`). Both are timezone/clock-dependent, so **these tests will pass or fail depending on the machine's timezone and the wall-clock time when CI runs** — a latent flake. Nothing injects a clock here.

⚠️ **`Booking.Tests/Lifecycle/AppointmentLifecycleTest.cs` cannot be testing the shipped behaviour of cancel.** `AppointmentEntity.Book()`/`Complete()` are never called by production code (`05-data-model.md`), and cancel is a hard delete. **Inference:** these 9 tests exercise the entity's domain methods in isolation, which no production path uses.

⚠️ **`Kafka.Tests` has 1 test for `KafkaClient`.** Since `KafkaClient.CreateTopicIfNotExist` hardcodes `localhost:9092` and constructs a real `AdminClientBuilder` (`KafkaClient.cs:12,15`), the single test either requires a live broker or asserts the connection-failure string. Either way, the topic-name collision defect in `KafkaHelper` (`09-integrations.md`) has **no test** — there is no `KafkaHelperTest.cs`.

### `MobileApp.Tests` — 67 tests against a different assembly shape

Runs with `/p:MobileWorkloads=false` (`.github/workflows/dotnet.yml:181`), so it references the `net10.0` slice of `MobileApp`.

| Area | Files | Tests |
|---|---|---:|
| ViewModels | 8 | 39 |
| API services | 6 | 23 |
| `JwtDelegatingHandler` | 1 | 2 |
| Acceptance | 1 | 7 |

Reaches `internal` members via `<InternalsVisibleTo Include="MobileApp.Tests" />` (`MobileApp.csproj:58`) — e.g. `AuthService.RefreshTokenKey` is asserted at `MobileApp.Tests/Services/AuthServiceTests.cs:44,75`.

⚠️ **`MauiProgram.cs` and `AppShell.xaml.cs` are `#if MOBILE` and therefore excluded from the tested assembly** (`07-build.md`). All DI registration, the named-`HttpClient` base-address configuration, and the Shell routing table are **structurally untestable** in this setup. This is why the `ApiBaseUrl`/port defect and the `http://localhost:6036/` fallback (`MauiProgram.cs:32,38`) were never caught.

⚠️ **The 23 API-service tests do not catch the route-prefix defect.** `BookingApiServiceTests.cs` (6 tests) exercises `BookingApiService`, which calls `GET booking?date=…` — a path no backend route serves (`01-api-surface.md`). **Inference:** the tests mock `HttpMessageHandler` and assert against the *client's own* URL expectation, so client and test agree while both disagree with the server. A contract test against the real route table would have caught it; there is none.

⚠️ **`PushNotificationServiceTests.cs` (3 tests) can only reach the early-return path.** `FIREBASE` is defined only for `net10.0-android` (`MobileApp.csproj:32`), and the test assembly is `net10.0`, so `RegisterTokenAsync` compiles to `return;` at `PushNotificationService.cs:48`. The Firebase token-acquisition flow that actually ships on Android is untested. The 3 tests presumably cover `PostTokenAsync` directly (it is `internal`).

⚠️ **The 7 acceptance tests never run.** Both test jobs pass `--filter "Category!=Acceptance"` (`.github/workflows/dotnet.yml:94,182`), and no other job runs them. They are permanently excluded from CI and from the documented `dotnet test` command in `CLAUDE.md`.

⚠️ **No UI tests.** 11 `.xaml` views and their code-behind have zero coverage — no Appium, no `.NET MAUI UITest`, no snapshot testing. `CONSTITUTION.md` §7 leaves visual-regression and a11y unchecked, so this is consistent with the constitution but means the entire F-012 UX redesign (PRs #31–#34) shipped without automated verification.

---

## Coverage pattern

- **Collector:** `--collect:"XPlat Code Coverage"` produces `coverage.cobertura.xml` per test project; CI uploads them as an artifact (`.github/workflows/dotnet.yml:107-113`).
- ⚠️ **No threshold, no report, no trend.** No ReportGenerator, no Codecov, no PR annotation, no minimum. `INTENT.md` targets ">80% unit test pass rate" and nothing measures it. `if-no-files-found: warn` (`:113`) means total collection failure is a warning.
- ⚠️ **58 production files carry `[ExcludeFromCodeCoverage]`** — including every entity, every `RequestCollection`, every `ProblemDetailsServiceEndpointFilter`, every `HttpContextExtensions`, and every command/query DTO. Reported coverage is therefore computed over a deliberately narrowed denominator and **overstates** true coverage. Notably excluded-and-untested: the six copies of `ProblemDetailsServiceEndpointFilter` (the whole error-envelope mechanism, `10-error-handling.md`) and all six `RequestCollection` classes (the entire CQRS dispatch path, `15-cqrs-and-messaging.md`).
- ⚠️ **`Program.cs` is still not covered by the *unit* suite** — top-level statements, no coverage collection over the route tables. ✅ **But it is now exercised over HTTP** by `AgendaBuddy.IntegrationTests` (F-016), which hosts each real service against a MongoDB Testcontainer, so auth attributes, validation calls, ownership guards and status-code mappings *are* verified end-to-end. Note `WebApplicationFactory<Program>` is **unusable here** — `Program` is ambiguous across all seven assemblies; the harness uses per-service anchor aliases instead (`Harness/EntryPoints.cs`, `GlobalUsings.cs`). The sentence this bullet replaced — "there is no integration test in the solution" — was the single most consequential line in this catalog: it is why F-016 absorbed eight of F-018's tasks and built the harness first.

---

## Build hooks

```bash
# CLAUDE.md (documented) — ⚠️ fails without MAUI workloads installed
dotnet test --collect:"XPlat Code Coverage"

# CI, backend (.github/workflows/dotnet.yml:89-97)
dotnet test --no-build --configuration Release \
  --filter "Category!=Acceptance" \
  --collect:"XPlat Code Coverage" \
  --logger "trx;LogFileName=test-results.trx" --results-directory ./TestResults

# CI, mobile (:178-184)
dotnet test MobileApp.Tests/MobileApp.Tests.csproj /p:MobileWorkloads=false \
  --filter "Category!=Acceptance" --collect:"XPlat Code Coverage"
```

⚠️ The mobile job omits `--logger trx` and the `test-reporter`/coverage-upload steps that the backend job has, so mobile results appear only in raw logs (`08-cicd-deploy.md`).

---

## What is missing

- ~~**No integration tests.**~~ **RESOLVED by F-016** — 99 tests invoke real endpoints over HTTP against a MongoDB Testcontainer, so `CONSTITUTION.md` §5's "All integration tests pass" finally has something to pass. Two caveats: the CI job is **not a required status check** on `main` yet, and §7's Integration checkbox stays unchecked pending 10 consecutive green runs.
- **No contract tests** between `MobileApp` and the services. This is the single gap that would have caught the product's most serious functional defect (`01-api-surface.md`).
- **No `CacheAside` test** — the mandated caching primitive, with three known defects.
- **No `KafkaHelper` test** — topic-name collision across email domains.
- **No test for `EventStore`** (`EventAndCommands/Persitency/`) — including the `GetEventsAsync` design flaw that makes event-stream reads impossible (`05-data-model.md`).
- **No test for any of the seven `ServiceCollectionExtension` classes**, so the root-vs-`LibrarySettings` config-section defect that makes the backend Development-only is uncovered (`06-configuration.md`).
- **No security scan** in CI despite `CONSTITUTION.md` §7 marking it mandatory and un-uncheckable — no `dotnet list package --vulnerable`, no CodeQL, no secret scanner (which would have found the committed Atlas credential).
- **No performance, load, accessibility, or visual-regression tests** (consistent with `CONSTITUTION.md` §7 being unchecked).
- **No mutation testing, no flake detection, no test-retry policy.**
- **No clock injection outside Identity**, making the Calendar availability tests timezone- and time-of-day-dependent.
