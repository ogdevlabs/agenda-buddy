# PRD: F-027 — carter-route-modules

**Feature ID:** F-027
**Date:** 2026-08-27
**Status:** Approved (self-approved under this session's standing full-autonomy grant)

## Problem

Every one of the 7 services' `Program.cs` files has grown large (207–523 lines) by inlining every route
registration directly in `Program.cs`, alongside builder/DI setup and pipeline middleware configuration.
This makes each file harder to scan for "what routes does this service expose" versus "how is it wired
up." Filed 2026-08-27 (user-suggested mid-F-020), deliberately deferred until F-020 shipped so every
service shares the same `Program.cs`-with-inline-routes shape before a route-organization pass lands
uniformly across all 7 rather than piecemeal.

## Goals

1. Reorganize each service's inline route registrations into [Carter](https://github.com/CarterCommunity/Carter)
   `ICarterModule` classes — one file per logical route group.
2. Preserve exact route paths, verbs, auth requirements, and response shapes. This is a **behavior-preserving
   refactor** — proven by the existing route-contract and OpenAPI-drift tests passing unchanged, not by
   inspection alone.
3. Decide, explicitly, whether Carter's own FluentValidation integration (`Validate<T>`) should be adopted
   alongside or instead of Validot (ADR-049).

## Non-goals

- No change to dispatch (`IMediator`/MediatR stays the sole dispatcher, per ADR-014).
- No change to response shape (each service's own `DataResponse<T>` stays as-is).
- No change to auth/ownership/rate-limiting logic — only where the code that expresses it lives.
- Identity's `AgendaBuddy.Identity.Modules.AuthModule`/`DeviceTokenModule` split mirrors the two pre-existing
  top-level route groups (`/api/v1/auth`, `/device-token`) rather than inventing new groupings.

## Requirements

1. Every one of the 7 services (Booking, Calendar, Customer, Provider, Services, Profession, Identity) gets
   its route registrations extracted into one or more `Modules/*.cs` files implementing `ICarterModule`.
2. Multi-group services split into one module per group, matching this project's own stated intent that
   `/api/v1/messages` and `/api/v1/notifications` are distinct top-level groups from `/api/v1/customers`
   (ADR D-2), not one arbitrary module per service.
3. Each service registers its own modules explicitly via `AddCarter(configurator: c => c.WithModule<T>()...)`
   — **not** Carter's default assembly-scanning discovery. Discovered during Build: `AgendaBuddy.IntegrationTests`
   loads all 7 services' assemblies into one test process, and Carter's default discovery picks up every
   `ICarterModule` implementation across all of them inside any single service's `WebApplicationFactory`
   host, causing cross-service DI/parameter-inference failures at startup. Explicit per-service module
   registration is required regardless of the test-host wrinkle — it is also just clearer than implicit
   scanning.
4. Carter's `Validate<T>` FluentValidation integration is evaluated at Design and NOT adopted — Validot
   remains the sole validation DSL for this project (ADR-049 stands; recorded as ADR-055 below).
5. `docs/api/openapi/*.json` for all 7 services stay byte-identical (proven by `OpenApiSpecDriftTest`
   passing unchanged) — a route reorganization must not change any contract.
6. Backend and integration suites pass unchanged (no new tests strictly required — this is a refactor, not
   new behavior — but the existing route-contract tests are the acceptance criteria).

## Design decisions

- **ADR-055**: Carter's `Validate<T>` integration is not adopted; Validot stays the sole DSL.
- Explicit `CarterConfigurator.WithModule<T>()` registration per service, not assembly scanning — required
  by the shared-test-process discovery, and also the clearer default going forward.

## Acceptance criteria

- AC-1: All 7 services build clean with Carter modules replacing inline route registration.
- AC-2: Backend suite passes with the same test count as pre-refactor baseline (571/571).
- AC-3: Integration suite passes with the same test count as pre-refactor baseline (327/327), including
  every `*RouteContractTest` and `OpenApiSpecDriftTest`.
- AC-4: `dotnet format --verify-no-changes` is clean.
- AC-5: No route path, verb, auth attribute, or response shape changed for any of the 7 services.
