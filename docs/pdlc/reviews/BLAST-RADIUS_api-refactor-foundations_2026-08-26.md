# Blast Radius — api-refactor-foundations (F-018)

**Scope:** large diff (199 files changed against `main`) — narrowed per the scope table to exported/public
symbols whose signature, return type, or error contract changed. The overwhelming majority of the diff is
new test files (additive, no existing consumers to break) or the F-018-T03 whitespace-only reformat
(no symbol change at all).

**Symbols examined:** 3   **Call sites found:** 107   **⚠ At risk:** 0   **Untested paths:** 0

## ⚠ At risk (review these first)

None found.

## Contract changes

| Contract | Consumers named | Verdict |
|---|---|---|
| `AddProviderCommandHandler` constructor — `KafkaClient kafkaClient` → `IKafkaClient kafkaClient` (F-018-T10) | 1 internal: `Provider/Requests/RequestCollection.cs:10` | ✅ updated in the same commit; no other constructor call site exists anywhere in the repo (`grep -rn "new AddProviderCommandHandler\|AddProviderCommandHandler(" .` finds exactly the class definition + this one call site). `Provider.Tests/` never constructs the handler directly — it's exercised only through the real HTTP route, which the new `Support/KafkaClientFakeProviderRegistrationTest.cs` now covers |
| `Provider/Requests/RequestCollection.cs` — dropped the `(kafkaClient as KafkaClient)!` downcast | same call site, same commit | ✅ behavior-preserving for the real `KafkaClient` (an `IKafkaClient` passed straight through, no cast needed); the whole point of the change was to make substitution with `KafkaClientFake` also work, which it now does |
| `ServiceHostFixture.StartService(...)` — added optional 3rd parameter `Action<IServiceCollection>? configureServices = null` (F-018-T10) and a new `Services` property on `ServiceHost` (F-018-T12) | 103 call sites across 53 files in `AgendaBuddy.IntegrationTests/` | ✅ **Unchanged — compatible.** Both additions are backward-compatible by construction: the new parameter defaults to `null` (existing calls omitting it are unaffected), and `Services` is a new property, not a changed one. All 103 pre-existing call sites keep compiling and behaving identically |

No public API route, event schema, or cross-service contract changed. No cross-repo consumers apply — `MobileApp` and the Gateway are the only other consumers in this repo, and neither calls anything F-018 touched.

## Untested changed paths

None — the one real behavior change (`AddProviderCommandHandler`'s constructor) is exercised by
`Support/KafkaClientFakeProviderRegistrationTest.cs`, added in the same commit that made the change.

## Full call-site map

- `AddProviderCommandHandler(...)` → `Provider/Requests/RequestCollection.cs:10` (Updated, same commit)
- `Provider/Requests/RequestCollection.cs`'s `IKafkaClient`-typed call → same site (Updated, same commit)
- `ServiceHostFixture.StartService(...)` → 103 call sites, `AgendaBuddy.IntegrationTests/**/*.cs` (all Unchanged — compatible, optional param)
- `ServiceHost.Services` (new property) → consumed by `Persistence/ConfiguredCollection.cs` (F-018-T12) and `OpenApi/OpenApiSpecGenerator.cs` (F-018-T16) internally; no external consumers since the property didn't exist before this session

## Search method

`grep -rn` across the full repo (not just the diff) for each symbol's name, excluding `bin/`/`obj/`. No
reflection, dynamic dispatch, or config-driven wiring is involved in any of the three changed symbols, so
grep is a complete search here — no hidden-caller caveat applies.
