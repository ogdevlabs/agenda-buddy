# Episode 014: Carter Route Modules

**Episode ID:** 014
**Feature name:** Carter Route Modules — every service's inline `Program.cs` route registrations reorganized into `ICarterModule` classes
**Feature slug:** carter-route-modules
**Feature ID:** F-027
**Date built:** 2026-08-27, on `feat/F-027-carter-route-modules`
**Phase delivered in:** Construction
**Date shipped:** 2026-08-27 — merged via the mandated PR path (ADR-050), PR #83, tagged **`v0.14.0`**
**Status:** Final

---

## What Was Built

Every one of the 7 services' `Program.cs` files had grown large (207–523 lines) by inlining all route
registration alongside builder/DI setup and pipeline middleware. Filed mid-F-020 and deliberately
deferred until F-020 shipped, so the reorganization could land uniformly across all 7 services sharing
the same inline-route shape, rather than piecemeal.

**Module map.** Booking → `BookingModule`; Calendar → `CalendarModule`; Customer → `CustomerModule` +
`MessageModule` + `NotificationModule` (three top-level route groups, per this project's own ADR D-2);
Provider → `ProviderModule`; Services → `ServicesModule`; Profession → `ProfessionModule`; Identity →
`AuthModule` + `DeviceTokenModule` (mirroring its two pre-existing top-level groups). Each `Program.cs`
now ends with `app.MapCarter();` in place of the removed route block.

**Real defect caught before merge, not after.** Carter's default `AddCarter()` discovers `ICarterModule`
implementations via assembly scanning. `AgendaBuddy.IntegrationTests` references all 7 API projects (for
its own per-service anchors), so all 7 services' assemblies load into one test process — under that
condition, default-discovery `AddCarter()` inside any one service's `WebApplicationFactory` host also
discovered every *other* service's modules. Services.Api's test host tried to build routing metadata for
Identity's `AuthModule`, whose `IdentityService svc` parameter isn't registered in Services.Api's
container, crashing `AuthorizationPolicyCache` construction and failing 223 of 327 integration tests on
the first attempt. Fixed by registering each service's modules explicitly —
`AddCarter(configurator: c => c.WithModule<T>()...)` — rather than relying on assembly-scan discovery.
This is the correct default regardless of the test-host wrinkle.

**ADR-055**: Carter's own `Validate<T>` FluentValidation integration was evaluated and not adopted —
Validot (ADR-049) remains the sole validation DSL this project is migrating toward. This feature
reorganizes route registration only; it does not touch validation.

**Behavior-preservation proof.** No route path, verb, auth attribute, or response shape changed for any
service. Backend suite 571/571 (baseline unchanged), integration suite 327/327 (baseline unchanged),
including every `*RouteContractTest` and all 7 `OpenApiSpecDriftTest` cases — a live-regeneration-vs-
committed-spec diff passing unchanged is the strongest available proof nothing in any of the 7 OpenAPI
contracts moved. `dotnet format --verify-no-changes` clean.

---

## Links

| Artifact | Path |
|---|---|
| PRD | [`PRD_F-027_carter-route-modules_2026-08-27.md`](../prds/PRD_F-027_carter-route-modules_2026-08-27.md) |
| Architecture | [`docs/pdlc/design/carter-route-modules/ARCHITECTURE.md`](../design/carter-route-modules/ARCHITECTURE.md) |
| Feature record | [`docs/pdlc/tasks/F-027/`](../tasks/F-027/) |
| Decisions | ADR-055 |
