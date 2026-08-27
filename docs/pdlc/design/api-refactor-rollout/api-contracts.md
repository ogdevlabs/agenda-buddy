# API Contracts — API Refactor Rollout (F-020)

## 1. Change summary

**No route, verb, or request-body shape changes**, for any of the 5 in-scope services (Calendar, Customer,
Provider, Services, Profession). Every route keeps its current path, HTTP method, and request payload.
**The only contract change is the response envelope**: every success response body is wrapped in that
service's own `DataResponse<T>` — same shape as Booking's, per service (`ARCHITECTURE.md` §3).

| Aspect | Before | After |
|---|---|---|
| Route paths/verbs | unchanged | unchanged |
| Request bodies | unchanged | unchanged |
| Status codes | unchanged (pinned by each service's `<Service>RouteContractTest.cs`) | unchanged |
| Response body | raw entity/string (varies per route) | `{ "data": <same payload shape>, "errors": [] }` |
| Error body | varies (bare string or `ProblemDetails` via the central handler) | success responses use `DataResponse<T>`; `ForbiddenException`/unhandled-exception responses continue through `AgendaBuddyExceptionHandler`'s existing `ProblemDetails` shape, unwrapped — same carve-out as Booking's |

## 2. `DataResponse<T>` envelope

Identical shape to Booking's, one instance per service (`ARCHITECTURE.md` §3):

```json
{
  "data": { "...": "the same payload the route returns today" },
  "errors": []
}
```

On failure (a `FluentResults.Result` that failed, mapped to a 4xx by the endpoint):

```json
{
  "data": null,
  "errors": ["human-readable reason"]
}
```

**Not applied to:** 403/500 responses that reach `AgendaBuddyExceptionHandler` — unchanged from Booking's
own carve-out, same reasoning (middleware-level failures are outside any individual route's mapping).

## 3. Per-service endpoint detail

This feature does not enumerate every route the way Booking's `api-contracts.md` did for its 10 (each of
Calendar/Provider/Services/Profession has 2–6 routes; Customer has 10) — the change is mechanically
identical across every route on every service: wrap the success payload in `DataResponse<T>`, leave
everything else (path, verb, request body, failure-branch status codes) untouched. Each service's task
list in the Plan documents its own route-by-route confirmation, mirroring Booking's §3.1–3.5 structure, at
Build time rather than predicted here — Booking's own experience (F-019) showed the actual DTO/entity shape
per route is often only fully clear once the handler is actually written.

**Known per-service deviations to confirm, not assume, during Build:**

- **Customer**: `MessageRequest.cs` exists in `Requests/` with no confirmed matching handler file
  (`ARCHITECTURE.md` §9) — its route (if any) needs its actual current behavior confirmed before this
  feature's envelope change is applied to it.
- **Services**: 4 handler files against only 2 routes — confirm which handlers are actually reachable from
  a route before assuming all 4 need migrating as "route handlers" vs. some being internal/unused.

## 4. What does not change

- Authentication/authorization: unchanged. `OwnershipGuard`/`AssertRole` calls stay exactly where they are
  today, in each service's endpoint delegate — no authorization *logic* changes for any of the 5 services.
- Every existing typed-`Results<>` failure branch (`ForbidHttpResult`, `NotFound`, `Conflict<string>`,
  `BadRequest<string>`, etc., wherever a service already uses them) — unchanged shapes, unchanged trigger
  conditions.
- Every entity's persisted shape — see `data-model.md` (no changes).
- **Mapster-based request/response DTOs are not introduced** — entities keep flowing through route
  signatures unchanged, matching Booking's actual delivered shape (not its original, never-built plan).
- **Validot migration is per-route, not blanket** — any route staying on `MiniValidator` (or its current
  inline-check equivalent, for the 2 services with 0 `MiniValidator` calls today — Calendar, Profession) is
  a disclosed choice per PRD Requirement 9, confirmed in each service's own verification notes at Build,
  not silently implied as fully migrated.
