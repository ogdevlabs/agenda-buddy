# API Contracts — API Refactor Pilot: Booking (F-019)

## 1. Change summary

**No route, verb, or request-body shape changes.** All 10 of Booking's routes keep their current path, HTTP
method, and request payload. **The only contract change is the response envelope**: every response body is
now wrapped in `DataResponse<T>`.

| Aspect | Before | After |
|---|---|---|
| Route paths/verbs | unchanged | unchanged |
| Request bodies | unchanged | unchanged |
| Status codes | unchanged (pinned by `BookingRouteContractTest.cs`) | unchanged |
| Response body | raw entity/string (varies per route — see §2) | `{ "data": <same payload shape>, "errors": [] }` |
| Error body | varies (some routes return a bare string, some `ProblemDetails` via the central handler) | success responses use `DataResponse<T>`; `ForbiddenException`/unhandled-exception responses continue through `AgendaBuddyExceptionHandler`'s existing `ProblemDetails` shape, **unwrapped** — the envelope applies to handler-returned results, not to middleware-level failures |

## 2. `DataResponse<T>` envelope

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

**Not applied to:** 403/500 responses that reach `AgendaBuddyExceptionHandler` (F-016) — those keep their
existing `ProblemDetails` shape, since that handler runs at the middleware level, outside any individual
route's `DataResponse<T>` mapping. Applying the envelope there too is out of scope for F-019 (would touch
the shared handler, a cross-cutting change affecting all 7 services — F-020's call, if ever).

## 3. Endpoint detail

### 3.1 `POST /api/v1/booking/appointments` (Book)

- **Before:** 201 with the raw success/failure string from the handler.
- **After (corrected at F-019-T11, built at T04):** 201 with `DataResponse<AppointmentEntity>`, not a new
  `AppointmentResponse` DTO as originally planned here. Requirement 7 (Mapster-based request/response DTOs,
  keeping `AppointmentEntity` out of route signatures) was never assigned to any F-019 task — T04's own
  requirement list was 2/3/4/5/9/10, deliberately excluding 7 — so the entity keeps flowing through
  unchanged, exactly as it did before this feature. Disclosed as a known gap, not fixed here: a future task
  (F-020, or a dedicated follow-up) owns introducing `AppointmentResponse` if Requirement 7 is ever picked up.
- Status codes unchanged: 201 success, existing failure codes per `BookingRouteContractTest.cs`.

### 3.2 `PUT /api/v1/booking/appointments/` (Update), `DELETE /api/v1/booking/appointments/` (Cancel)

Same shape change as 3.1 — `DataResponse<AppointmentEntity>` on success. Cancel's success path is a further,
separate exception: a 204 No Content cannot carry a JSON body at all, so it stays exactly `NoContent()` with
no envelope — disclosed in Requirement 10's own attestation (§4 below), not silently unmet.

### 3.3 `POST /api/v1/booking/appointments/{id}/status`

- **Before:** typed `Results<Ok<AppointmentStatusResponse>, ForbidHttpResult, NotFound, Conflict<string>, BadRequest<string>>`
  — already a real response DTO (`AppointmentStatusResponse`), not a raw entity.
- **After:** `Ok<DataResponse<AppointmentStatusResponse>>` on the success branch; the `ForbidHttpResult`/
  `NotFound`/`Conflict`/`BadRequest` branches are unchanged (these are the exact carve-outs a `FluentResults`
  failure maps to, not envelope-wrapped bodies).

### 3.4 `GET/POST /api/v1/booking/appointments/{id}/notes`, `PUT/DELETE /api/v1/booking/notes/{id}`

Same treatment as 3.3 — already-typed responses gain the envelope on their success payload only.

### 3.5 `POST/GET /api/v1/booking/appointments/{id}/payment`

Same treatment as 3.3/3.4.

## 4. What does not change

- Authentication/authorization: unchanged. `OwnershipGuard.AssertOwnerAny`/`AssertOwner` calls stay exactly
  where they are today, in the endpoint delegate (built at F-019-T04/T06 — authorization stayed in
  `Booking.Api`, not the `Booking.Core` handler, for every route; no authorization *logic* changed either
  way).
- The 5 status-transition/notes/payment routes' typed-`Results<>` failure branches (`ForbidHttpResult`,
  `NotFound`, `Conflict<string>`, `BadRequest<string>`) — unchanged shapes, unchanged trigger conditions.
- `AppointmentEntity`'s persisted shape — see `data-model.md`.
- **Corrected at F-019-T11, built at T04/T06 — two disclosed exceptions to "every success response gets
  `DataResponse<T>`":** Cancel (`DELETE /appointments/`) and Delete Note (`DELETE /notes/{id}`) both answer
  204 No Content on success, unchanged — a 204 cannot carry a JSON body by HTTP semantics, so there is
  nothing to wrap. Requirement 10's own PRD language ("becomes the response envelope for all 10 routes") is
  read as "every route whose success response has a body," not literally all 10.
- **Also corrected at F-019-T11:** Requirement 6 (Validot replacing `MiniValidator` everywhere) only
  actually happened for `POST /appointments` (Book) in this feature. `PUT`/`DELETE /appointments/`
  (Update/Cancel) still call `MiniValidator.TryValidate` unchanged — no F-019 task was ever assigned this
  conversion for them (T04's own requirement list excluded 6). Filed as a known gap for a follow-up task,
  not silently treated as done.
