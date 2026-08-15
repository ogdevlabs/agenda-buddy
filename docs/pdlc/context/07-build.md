# 07 — Build

**Files:** `agenda-buddy.sln`, `global.json`, `Directory.Build.props`, 23 `*.csproj`.

---

## SDK and target framework

`global.json`:
```json
{ "sdk": { "version": "10.0.0", "rollForward": "latestMajor", "allowPrerelease": true } }
```

⚠️ **`allowPrerelease: true`** (`:5`) — the build will silently pick up a prerelease .NET SDK if one is installed, so local and CI builds can diverge. CI pins `dotnet-version: 10.0.x` (`.github/workflows/dotnet.yml:73`), which resolves to the latest stable 10.x, but `rollForward: latestMajor` means a machine with .NET 11 installed would use it.

Every production project targets **`net10.0`**. `MobileApp` is the exception — see the conditional TFM section.

⚠️ **`CLAUDE.md` and `CONSTITUTION.md` §1 both say ".NET 8".** The code is .NET 10 (shipped by F-011 `upgrade-to-net10`). Documentation drift.

⚠️ **`Kafka/Kafka.csproj:5` pins `<LangVersion>12</LangVersion>`** — the only project to do so. .NET 10 defaults to C# 14, so this project is held two language versions back for no stated reason.

---

## Solution layout (`agenda-buddy.sln`)

23 projects in solution folders `kafka`, `Common`, `EventDriven`, `Provider`, `Customers`, and others. Note the folder names do not match the project names (solution folder `Customers` contains project `Customer`; solution folder `Provider` contains project `Provider` — a name collision between a folder and a project GUID entry, legal but confusing in tooling).

| Group | Projects |
|---|---|
| Shared libraries (4) | `Library`, `Library.ServerAuth`, `EventAndCommands`, `Kafka` |
| API services (7) | `Booking`, `Calendar`, `Customer`, `Provider`, `Services`, `Profession`, `Identity` |
| Client (1) | `MobileApp` |
| Test projects (11) | `Booking.Tests`, `Calendar.Tests`, `Customer.Tests`, `EventsAndCommands.Tests`, `Identity.Tests`, `Kafka.Tests`, `Library.Tests`, `MobileApp.Tests`, `Profession.Tests`, `Provider.Tests`, `Services.Tests` |

⚠️ **`EventsAndCommands.Tests` (plural "Events") tests `EventAndCommands` (singular "Event").** The mismatch is noted in `CLAUDE.md`; it means the test project does not follow the `<Project>.Tests` convention that the other ten do.

⚠️ **There is no `Directory.Packages.props`** — no central package version management. Versions are repeated per `csproj`, which is how the version skew below arose.

---

## Dependency map

```
Library.ServerAuth ──────────────┐   (JwtBearer only; no project refs)
                                 │
Library ─────────┬───────────────┤
                 │               │
Kafka ───────┬───┤               │
             │   │               │
        EventAndCommands ────────┤
                 │               │
   ┌─────────────┴───────────────┴──────────────┐
   │  Booking   Calendar  Customer  Provider    │  → EventAndCommands + Library + Library.ServerAuth
   │  Services  Profession                      │    (Booking & Customer also → Kafka)
   └────────────────────────────────────────────┘
Identity ──→ Library + Library.ServerAuth        (no EventAndCommands, no Kafka)
MobileApp ─→ Library                             (only)
```

Per-project references:

| Project | Project references |
|---|---|
| `Library` | *(none)* |
| `Library.ServerAuth` | *(none)* |
| `Kafka` | *(none)* |
| `EventAndCommands` | `Kafka`, `Library` |
| `Booking` | `EventAndCommands`, `Kafka`, `Library`, `Library.ServerAuth` |
| `Customer` | `EventAndCommands`, `Kafka`, `Library`, `Library.ServerAuth` |
| `Calendar` | `EventAndCommands`, `Library`, `Library.ServerAuth` |
| `Provider` | `EventAndCommands`, `Library`, `Library.ServerAuth` |
| `Services` | `EventAndCommands`, `Library`, `Library.ServerAuth` |
| `Profession` | `EventAndCommands`, `Library`, `Library.ServerAuth` |
| `Identity` | `Library`, `Library.ServerAuth` |
| `MobileApp` | `Library` |

⚠️ **`Calendar`, `Provider`, `Services`, `Profession` reach `Kafka` transitively through `EventAndCommands`** but do not reference it directly. `Provider/Program.cs:21` nonetheless registers `AddSingleton<IKafkaClient, KafkaClient>()` — relying on a transitive reference for a type it names directly. Fragile: removing `Kafka` from `EventAndCommands` would break four services that never declared the dependency.

⚠️ **`MobileApp` references `Library`** (`MobileApp.csproj:54`), pulling `MongoDB.Driver`, `MongoDB.Bson`, `Stripe.net`, and `BCrypt.Net-Next` into a **mobile app bundle**. The client only needs `AppointmentStatus` and a few entity shapes (`MobileApp/Services/SeedDataProvider.cs:1`, `MobileApp/Services/BookingApiService.cs:3`). This inflates the app size and ships a payment SDK and a password hasher to end-user devices. A shared DTO/contracts project would be the right seam.

---

## Package version inventory

| Package | Version | Used by |
|---|---|---|
| `MediatR` | 12.3.0 | Booking, Calendar, Provider, Services, Profession, Identity, EventAndCommands, Kafka |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.0 | all 7 services + `Library.ServerAuth` |
| `Microsoft.AspNetCore.OpenApi` | 10.0.0 | all 7 services |
| `Swashbuckle.AspNetCore` | 10.2.3 | all 7 services |
| `MongoDB.Driver` | 2.25.0 | Booking, Calendar, Provider, Services, Profession, Identity, EventAndCommands, Library |
| `MongoDB.Bson` | 2.25.0 | Services, EventAndCommands, Library |
| `MiniValidation` | 0.9.1 | Booking, Customer, Services, Profession, Identity |
| `Microsoft.Extensions.Caching.Abstractions` / `.Memory` | 10.0.0 | Library |
| `BCrypt.Net-Next` | 4.0.3 | Library, Identity |
| `Stripe.net` | 45.0.0 | Library |
| `KafkaFlow` + 3 satellites | 3.0.7 | Kafka |
| `CommunityToolkit.Mvvm` | 8.3.2 | MobileApp |
| `CommunityToolkit.Maui` | 9.1.1 | MobileApp (MAUI TFMs only) |
| `Plugin.Firebase.CloudMessaging` | 3.0.0 | MobileApp (Android only) |
| `Microsoft.Extensions.Http` | 10.0.0 | MobileApp |

### ⚠️ Missing and unused package references

- ⚠️ **`Kafka` uses `Confluent.Kafka` but does not reference it.** `Kafka/KafkaClient.cs:1-2` has `using Confluent.Kafka;` / `using Confluent.Kafka.Admin;`, and `Kafka/Support/KafkaHelper.cs:1` has `using Confluent.Kafka;`, yet `Kafka.csproj` declares **no `Confluent.Kafka` package**. It resolves **transitively through `KafkaFlow` 3.0.7**. Meanwhile `KafkaFlow` itself is entirely unused (grep: zero references). So the project depends on four KafkaFlow packages solely as an accidental delivery vehicle for `Confluent.Kafka`. A `KafkaFlow` major bump that drops or renames the Confluent dependency breaks the build for a reason nothing in the project file explains. **`CLAUDE.md` describes this as "`KafkaClient` for topic creation (Confluent.Kafka)"** — the intent is Confluent; the declaration is missing.
- ⚠️ **`Provider/Provider.csproj:17` references `MongoDB.Entities` 23.1.0** — a complete alternative ODM. Grep: zero usages. Dead dependency.
- ⚠️ **`Provider/Provider.csproj:15` references `MinimalApis.Extensions` 0.11.0.** No `MinimalApis.*` type is used directly, but `Provider/Program.cs:106` calls `MiniValidator.TryValidate` while `Provider.csproj` declares **no `MiniValidation` package** — so `MinimalApis.Extensions` is functioning purely as the transitive source of `MiniValidation`. The same undeclared-dependency pattern as `Confluent.Kafka` above: remove the unused-looking package and the build breaks.
- ⚠️ **`Kafka/Kafka.csproj:20` references `MediatR`** but no `Kafka` type touches MediatR. Dead.
- ⚠️ **`MongoDB.Driver` 2.25.0 on .NET 10.** The 2.x driver line predates .NET 10; the 3.x line is current. Pinning 2.25.0 is what forces the `Snappier` and `SharpCompress` CVE pins below.

### ⚠️ Version skew across test projects

| Package | `Library.Tests` | `Identity.Tests` |
|---|---|---|
| `xunit` | 2.9.3 | **2.8.1** |
| `xunit.runner.visualstudio` | 2.8.2 | **2.8.1** |

Three different xunit/runner version pairs across 11 test projects. `Identity.Tests` also uniquely adds `Xunit.SkippableFact` 1.4.13 and `JetBrains.Annotations` 2024.2.0-**eap1** (a prerelease annotation package in a test project).

---

## `Directory.Build.props` — solution-wide overrides

Applies to **every** project including test projects.

### Warning suppressions (`:16`)

```xml
<NoWarn>$(NoWarn);SYSLIB0014;ASPDEPR002</NoWarn>
```

| Code | Meaning | Comment says |
|---|---|---|
| `SYSLIB0014` | `ServicePointManager` / `WebRequest` obsolete | "used for TLS configuration across all services. Removal requires migrating to `SocketsHttpHandler` — deferred" (`:11-12`) |
| `ASPDEPR002` | `WithOpenApi` deprecated in ASP.NET 10 | "still functional. Migration to new OPENAPI registration pattern is a future task" (`:13-14`) |

⚠️ Both are **real deprecations being suppressed globally rather than tracked**. `WithOpenApi` is called in all seven services (`Booking/Program.cs:90` etc.); `ServicePointManager` is called in five `Program.cs` files plus `ConfigurationLoader.cs:7` — and per `02-entry-points.md` the `ServicePointManager` calls have no effect on .NET Core `HttpClient` at all, so the suppression protects dead code.

⚠️ `CONSTITUTION.md` §5 Definition of Done includes "No compiler warnings promoted to errors" — but the project has no `TreatWarningsAsErrors` anywhere, so warnings are simply tolerated rather than gated.

### Transitive CVE pins (`:18-28`)

Explicit direct references added purely to force vulnerable transitive packages forward:

| Package | Pinned to | Fixes | Pulled in by |
|---|---|---|---|
| `Snappier` | 1.3.1 | GHSA-pggp-6c3x-2xmx (was 1.0.0) | `MongoDB.Driver` |
| `SharpCompress` | 0.50.1 | GHSA-6c8g-7p36-r338 (was 0.30.1) | `MongoDB.Driver` |
| `Newtonsoft.Json` | 13.0.4 | GHSA-5crp-9r3c-p9vr (was 12.0.3) | `MongoDB.Driver` / ASP.NET |
| `Microsoft.OpenApi` | 2.11.0 | GHSA-v5pm-xwqc-g5wc (was 2.0.0) | `Microsoft.AspNetCore.OpenApi` |

`:25-26` notes `Microsoft.OpenApi` "must stay in the 2.x range — 3.x has breaking API changes that break the source generator."

⚠️ **Three of four pins exist because `MongoDB.Driver` is held at 2.25.0.** Upgrading the driver to 3.x would likely retire the `Snappier`, `SharpCompress`, and `Newtonsoft.Json` pins. The pins are the symptom; the pinned driver is the cause.

⚠️ These references are added to **all 23 projects**, including `Library.ServerAuth` and every test project, none of which need them.

---

## MAUI conditional multi-targeting (`MobileApp/MobileApp.csproj`)

The most complex build logic in the repo. Two switches drive it:

| Property | Values | Effect |
|---|---|---|
| `MobileWorkloads` | `true` (default, `:9`) / `false` | `false` ⇒ TFM is plain `net10.0`, `UseMaui=false` |
| `MobilePlatform` | `''` / `android` / `ios` | Restricts the build to one mobile TFM |

Resulting TFM matrix (`:17-20`):

| `MobileWorkloads` | `MobilePlatform` | `TargetFrameworks` |
|---|---|---|
| `false` | any | `net10.0` |
| `true` | `android` | `net10.0-android` |
| `true` | `ios` | `net10.0-ios` |
| `true` | `''` | `net10.0-android;net10.0-ios;net10.0` |

Supporting mechanics:
- `OutputType=Exe` only for the mobile TFMs (`:24`); `net10.0` stays a Library so `MobileApp.Tests` can reference it.
- `DefineConstants`: `MOBILE` for both mobile TFMs (`:31`), `FIREBASE` for Android only (`:32`).
- `:72-74` — when `UseMaui != true`, `<Compile Remove="Platforms\**\*.cs" />`, with a comment (`:69-71`) explaining that the MAUI SDK's automatic platform-folder exclusion only activates under `UseMaui=true`.
- `:58` `<InternalsVisibleTo Include="MobileApp.Tests" />` — the tests reach `internal` members (`AuthService.RefreshTokenKey`, `PushNotificationService.RegisterTokenAsync`).
- `:42` `<ValidateXcodeVersion>false</ValidateXcodeVersion>` — "Allow minor Xcode version mismatches (e.g. 26.5 vs required 26.4)".
- `:34` `SkipValidateMauiImplicitPackageReferences` — suppresses MA002 because `Directory.Build.props` injects explicit refs.

Source files are guarded to match: `MauiProgram.cs:1`/`:81` and `AppShell.xaml.cs:1`/`:35` are wrapped in `#if MOBILE`.

⚠️ **The `net10.0` slice compiles the app with `MauiProgram` and `AppShell` removed**, so what `MobileApp.Tests` exercises is a *different assembly shape* than what ships. DI registration (`MauiProgram.cs`) is therefore **never covered by a test** — see `11-testing.md`.

⚠️ **`PushNotificationService.RegisterTokenAsync` becomes `return;` at line 48 in the non-Firebase slice** (`#else return; #endif`), followed by `#pragma warning disable CS0162` (unreachable code) at `:51`. The tests can only ever exercise the early-return path, so the token-registration flow that ships on Android is untested.

⚠️ **`SupportedOSPlatformVersion` for iOS is 18.0** (`:39`) and Android 21 (`:40`) — iOS 18 is aggressive (drops iPhone X and earlier); Android 21 is very permissive. `[unknown — outside repo]` whether that matches the target market.

---

## Codegen, formatter, lint

- **No source generators** beyond the framework's (`Microsoft.AspNetCore.OpenApi` uses one — hence the `Microsoft.OpenApi` 2.x constraint at `Directory.Build.props:26`).
- **No `.editorconfig`**, no StyleCop, no analyzer package, no `dotnet format` invocation in CI. `CONSTITUTION.md` §2 records both linter and formatter as unconfigured — still true.
- **No pre-commit hook** (`CONSTITUTION.md` §2: "none").
- `GlobalUsings.cs` per project instead of `<Using Include="..."/>` items. ⚠️ `Booking/GlobalUsings.cs:25` has **two directives on one line**: `global using MongoDB.Driver;global using EventAndCommands;` — valid C# but a formatting defect that an `.editorconfig`/formatter would catch.

---

## Build output and local recipe

Per `CLAUDE.md`:

```bash
dotnet restore
dotnet build --no-restore
dotnet test --collect:"XPlat Code Coverage"
```

⚠️ **A bare `dotnet build` at the solution root requires the MAUI workloads**, because `MobileApp` defaults to `MobileWorkloads=true` (`:9`) and will try to resolve `net10.0-android` + `net10.0-ios`. Without `dotnet workload install maui`, the solution build fails. The working invocation is what CI uses:

```bash
dotnet restore /p:MobileWorkloads=false
dotnet build --no-restore --configuration Release /p:MobileWorkloads=false
```

⚠️ **`CLAUDE.md`'s documented build/test commands omit `/p:MobileWorkloads=false`** and therefore do not work on a machine without MAUI workloads. Documentation drift against `.github/workflows/dotnet.yml:84,87`.

Output layout: default per-project `bin/<Config>/net10.0/`. `EventAndCommands`, `Calendar`, and `Customer` force `appsettings*.json` to `CopyToOutputDirectory: Always` (`EventAndCommands.csproj:26-28`, `Calendar.csproj:23-28`, `Customer.csproj:22-27`); the other four services rely on the Web SDK's default copy behaviour. ⚠️ `Customer.csproj:8` and `Provider.csproj:8` set `ErrorOnDuplicatePublishOutputFiles=false` — suppressing a real publish-time collision rather than resolving it.
