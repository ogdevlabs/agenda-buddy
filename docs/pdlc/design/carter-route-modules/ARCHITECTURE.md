# Architecture: F-027 carter-route-modules

## Summary

Every service's inline `app.MapGroup(...).MapGet/Post/Put/Delete/Patch(...)` route registrations moved
from `Program.cs` into `Modules/*.cs` files implementing `Carter.ICarterModule`. `Program.cs` keeps
builder/DI setup and pipeline middleware, ending with `app.MapCarter();` in place of the removed route
block.

## Module map

| Service | Module(s) | Route group(s) |
|---|---|---|
| Booking | `BookingModule` | `api/v1/booking` |
| Calendar | `CalendarModule` | `api/v1/calendar` |
| Customer | `CustomerModule`, `MessageModule`, `NotificationModule` | `/api/v1/customers`, `/api/v1/messages`, `/api/v1/notifications` |
| Provider | `ProviderModule` | `/api/v1/providers` |
| Services | `ServicesModule` | `api/v1/services` |
| Profession | `ProfessionModule` | `api/v1/professions` |
| Identity | `AuthModule`, `DeviceTokenModule` | `api/v1/auth`, `/device-token` |

Customer and Identity split into multiple modules because this project's own code comments already
named `/api/v1/messages`/`/api/v1/notifications` and `/device-token` as deliberate separate top-level
groups (ADR D-2) — one module per service would have papered over that distinction.

## Registration: explicit, not assembly-scanned

Carter's default `AddCarter()` discovers `ICarterModule` implementations via assembly scanning. This
project's `AgendaBuddy.IntegrationTests` project references all 7 API projects (for its per-service
anchor types), so all 7 services' assemblies load into one test process. Under that condition, calling
default-discovery `AddCarter()` inside any one service's `WebApplicationFactory` host caused Carter to
also discover and attempt to register every *other* service's modules — e.g. Services.Api's test host
tried to build routing metadata for Identity's `AuthModule`, whose `IdentityService svc` parameter isn't
registered in Services.Api's DI container, crashing `AuthorizationPolicyCache` construction and failing
223 of 327 integration tests on the first attempt.

Fixed by registering each service's modules explicitly:

```csharp
builder.Services.AddCarter(configurator: c => c.WithModule<BookingModule>());
// Customer.Api, three modules:
builder.Services.AddCarter(configurator: c =>
    c.WithModule<CustomerModule>().WithModule<MessageModule>().WithModule<NotificationModule>());
```

This is the correct default regardless of the test-host wrinkle — assembly scanning is implicit and
fragile; explicit registration states exactly which modules a service owns.

## Rejected: Carter's `Validate<T>` FluentValidation integration (ADR-055)

Carter ships an optional `Validate<T>` FluentValidation-based request-validation helper. Not adopted:
this project already standardized on Validot (ADR-049) for its (partial, 3-of-10-routes) validation
migration. Introducing a second validation library for routes that happen to be reorganized in this pass
would fragment validation strategy for no behavioral gain — this feature reorganizes route registration,
it does not touch validation.

## Behavior-preservation proof

- Backend suite: 571/571 (baseline unchanged).
- Integration suite: 327/327 (baseline unchanged), including every `*RouteContractTest` (one per service)
  and `OpenApiSpecDriftTest` (7 cases, one per service) — the latter is the strongest proof: a drift test
  comparing a live regeneration against the committed spec passing unchanged means no route, verb, or
  schema changed.
- `dotnet format --verify-no-changes` clean.

## Rate-limiting note (Identity)

`AuthModule` re-binds `RateLimitingOptions` from `IConfiguration` at route-mapping time (via
`app.ServiceProvider.GetRequiredService<IConfiguration>()`), mirroring `Program.cs`'s own builder-time
bind, rather than relying on the value already bound into a local variable in `Program.cs` — a Carter
module's `AddRoutes` has no access to `Program.cs`'s locals. `Program.cs` itself still separately binds
and uses the flag for `AddAuthRateLimiter`/`UseRateLimiter`, both of which must run before/around
`builder.Build()`/pipeline construction and cannot move into a module. This introduces no behavior change
— both reads see the same configuration at the same point in the startup sequence.
