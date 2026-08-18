# Blast Radius — aspire-wiring (F-013)

**Scope:** public + signature/behaviour-changed symbols. The diff spans ~90 files, so per the scope table this narrows to exported symbols and anything whose signature, return type, error contract, lifetime, or behaviour contract changed. Purely additive new types are listed once as greenfield.
**Search used:** `grep -rn --include="*.cs"` over the whole repo, `/obj/` excluded.
**Symbols examined:** 9 · **Call sites found:** 61 · **⚠ At risk:** 0 remaining (2 found and closed during Build) · **Untested paths:** 1

> Evidence only — no severity labels here. Reviewers verify and own the findings.

## ⚠ At risk

**None remaining.** Two were found while building and are already closed; both are recorded because "no risk found" and "risk found and fixed" are different claims:

| Symbol | Change | Caller | Was it valid? | Resolution |
|---|---|---|---|---|
| `IRequestCollection` registration | lifetime `Singleton` → consumed a `Scoped` `IEventStore` | 6 × `Program.cs` | **No** — DI validation rejected it; 6 of 7 services could not start in `Development` | F-013-T13 registered it `Scoped`. Verified by starting all 7. |
| `MongoDbConfiguration(IConfiguration)` | built `MongoClient` from `configuration[...]!` | 3 test files | Compiled, but violated AC-4.2 | Guarded with a named-key throw. The 3 tests pass a valid value, unaffected. |

## Contract changes

| Contract | Consumers named | Verdict |
|---|---|---|
| **`EventStore(IConfiguration)` → `EventStore(IMongoClient, IConfiguration)`** | resolved via DI only, from `AddEventStore()` in **6** `Program.cs` (Booking, Calendar, Customer, Provider, Services, Profession). Identity never calls it. | ✅ All 6 also register `AddSingleton<IMongoClient>` — checked one by one, not assumed. Identity needs nothing. |
| **`MongoDbConfiguration`** gains an `IMongoClient` ctor; legacy `IConfiguration` ctor retained | 7 × `Program.cs` (updated to an explicit factory) · 3 × test files still use the legacy ctor | ✅ Two ctors made the container's choice ambiguous — resolved by registering through an explicit factory rather than by type. Had this been left to `AddSingleton<IMongoDbConfiguration, MongoDbConfiguration>()`, it would have failed at first resolution, not at build. |
| **`AddMongoDbRepository`** — signature unchanged, **behaviour changed**: no longer constructs a client, now requires `IMongoClient` in DI and resolves it lazily per registration | 7 × `Program.cs` · 21 new test call sites | ✅ All 7 register the client. The behaviour change is the point (AC-4.3) and is what the 28 new per-service tests pin. |
| **`KafkaClient()` → `KafkaClient(IConfiguration? = null)`** | `AddSingleton<IKafkaClient, KafkaClient>()` in Booking, Customer, Provider · `new KafkaClient()` in Kafka.Tests | ✅ Optional param keeps the parameterless call valid. DI supplies `IConfiguration`, so the registration became configuration-driven with no `Program.cs` edit — confirmed live, since `ValidateOnBuild` in `Development` constructs it and all 7 services started. |
| **`IEventStore`** consumed by 14 command/query handlers in `EventAndCommands/` | handlers are constructed manually (`new …CommandHandler(mediator, …, eventStore)`) and receive the instance from their caller | ✅ Unaffected — they never construct an `EventStore`. Also confirms AC-5.3: no handler file was touched. |
| **Health endpoints `/health`, `/alive`** — new public HTTP surface on all 7 services | anonymous, no consumers yet | ⚠ New unauthenticated surface. Intentional (§7) but it is Phantom's call, not mine. |
| **`Kestrel:Endpoints` / launch-profile port adoption** | AppHost clears `Port`/`TargetPort` on adopted endpoints | ⚠ Mutates annotations Aspire produced. Behaviour verified by test, but it leans on Aspire's model shape — a future Aspire upgrade could change it. |

**Not verifiable by grep:** `MobileApp` consumes these services over HTTP, not by project reference. Its hardcoded base URLs are unaffected by the AppHost's dynamic ports **only because MobileApp does not run under the AppHost**. External or cross-repo consumers of the `603x` ports (scripts, Postman collections, a teammate's local config) cannot be found from this repo — flagged, not cleared.

## Untested changed paths

| Symbol | Test found? |
|---|---|
| `MongoDbConfiguration(IConfiguration)` guarded throw | ⚠ **No test** asserts the new named-key throw on the legacy ctor. The 3 existing tests only exercise the happy path. Echo's call. |
| `ProfessionSeedHostedService.StartAsync` | ⚠ No unit test; verified only by observing Profession start with no database reachable. |
| `AppHostWiring.Configure` | ✅ 28 tests |
| `MongoConnectionResolver` / `MongoHealthCheck` | ✅ 22 tests |
| `AddServiceDefaults` / `MapDefaultEndpoints` | ✅ 9 tests |
| 7 × `AddMongoDbRepository` | ✅ 28 tests |
| `KafkaClient.BootstrapServers` | ✅ 6 tests |

## Full call-site map

- `EventStore` ctor → 6 `AddEventStore()` sites, all with `IMongoClient` registered; 14 handler consumers unaffected (manual construction).
- `MongoDbConfiguration` → 7 factory registrations (updated) + 3 legacy-ctor test sites (unchanged, compatible).
- `IMongoDbConfiguration` → 7 registrations; **no production consumer resolves it** — it was already effectively dead before this change and remains so. Candidate deletion, flagged for Neo's YAGNI lens.
- `AddMongoDbRepository` → 7 production + 21 test sites.
- `IKafkaClient`/`KafkaClient` → 3 registrations + 1 test site + `RequestCollection` injection in Booking/Customer/Provider.
- `IRequestCollection` → 6 registrations (now Scoped), injected only into endpoint handlers; no root-provider resolution anywhere.
- Greenfield, no prior callers: `MongoConnectionResolver`, `MongoHealthCheck`, `AgendaBuddy.ServiceDefaults.Extensions`, `AppHostWiring`, `ProfessionSeedHostedService`.
- **Catalog vs code:** `docs/pdlc/context/` still describes the pre-Aspire wiring (per-request `MongoClient`, `MongoDB:ConnectionString`-only). Expected — it refreshes at Ship Reflect 16c-bis. Where they disagree, the code wins.
