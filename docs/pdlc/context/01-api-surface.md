# 01 — API Surface

> **⚠️ F-013 delta (2026-08-18, `v0.1.0`) — this file was written 2026-08-15 and has NOT been re-read since.**
>
> Routes themselves are unchanged, but **every service gained two endpoints** not documented here: `GET /health` (readiness — 200 `Healthy` / 503 `Unhealthy`, includes a 5s-cached MongoDB check) and `GET /alive` (liveness — stays 200 when MongoDB is down, by design). Both are anonymous. Ports are now **dynamically assigned by the Aspire AppHost**, so any 603x port numbers here are wrong under the AppHost.
>
> `file:line` anchors below may have shifted. Authoritative sources for the change: `docs/pdlc/archive/design/aspire-wiring/ARCHITECTURE.md`, `docs/pdlc/episodes/EPISODE_aspire-wiring_2026-08-17.md`. A full targeted rehydration is queued as the first step of F-018.


**Source of truth:** the route registrations in each service's `Program.cs`. There is **no committed OpenAPI/Swagger spec file** — Swashbuckle 10.2.3 generates one at runtime, and only in `Development` (`Booking/Program.cs:38-41` and the equivalent block in each service).

**Files:** `Booking/Program.cs`, `Calendar/Program.cs`, `Customer/Program.cs`, `Provider/Program.cs`, `Services/Program.cs`, `Profession/Program.cs`, `Identity/Program.cs`.

---

## Declared servers / ports

Each service pins its own Kestrel endpoints in `appsettings.json` (`"Kestrel": { "Endpoints": … }`). These are **hardcoded to `localhost`**, so the same config cannot serve a container.

| Service | HTTP | gRPC (declared, unused) | appsettings anchor |
|---------|------|-------------------------|--------------------|
| Provider | `http://localhost:6030` | `http://localhost:7030` | `Provider/appsettings.json:12,16` |
| Services | `http://localhost:6031` | `http://localhost:7031` | `Services/appsettings.json:11,15` |
| Calendar | `http://localhost:6032` | `http://localhost:7032` | `Calendar/appsettings.json:12,16` |
| Booking | `http://localhost:6033` | `http://localhost:7033` | `Booking/appsettings.json:12,16` |
| Customer | `http://localhost:6034` | `http://localhost:7034` | `Customer/appsettings.json:12,16` |
| Profession | `http://localhost:6035` | `http://localhost:7035` | `Profession/appsettings.json:12,16` |
| Identity | `http://localhost:6036` | `http://localhost:7036` | `Identity/appsettings.json:12,16` |

⚠️ **A `gRPC` endpoint with `Protocols: Http2` is declared for all seven services but no gRPC service is ever registered** — no `.proto` files, no `Grpc.*` package reference, no `MapGrpcService` call. Dead configuration.

⚠️ **No API gateway or reverse proxy exists** in the repo (no YARP, no nginx config, no Ingress manifest). Any client must know all seven ports. See the contract-drift section below.

---

## Full endpoint inventory

`[Auth]` = `.RequireAuthorization()` is applied. All groups carry `.WithOpenApi()` and, except Identity, `.AddEndpointFilter<ProblemDetailsServiceEndpointFilter>()`.

### Booking — `api/v1/booking` (`Booking/Program.cs:88`)

| Verb | Path | Auth | Handler line | Returns |
|------|------|------|--------------|---------|
| POST | `/appointments` | ✅ `:116` | `:93` | `201 Created<AppointmentEntity>`, `400 ValidationProblem`, `403 Forbid` |
| PUT | `/appointments/` | ✅ `:141` | `:118` | `202 Accepted<AppointmentEntity>`, `400`, `403` |
| DELETE | `/appointments/` | ✅ `:166` | `:143` | `204 NoContent`, `400`, `403` |

- Ownership: `OwnershipGuard.AssertOwnerAny(user, EmailProvider, EmailCustomer)` — either party may act (`:104`, `:128`, `:153`).
- Validation: `MiniValidator.TryValidate(appointmentEntity, out var errors)` before anything else (`:100`, `:125`, `:150`).
- ⚠️ **DELETE takes `[FromBody] AppointmentEntity`** (`:147`) — a request body on DELETE. Many proxies and HTTP clients strip it.
- ⚠️ **There is no `GET` on Booking at all.** No way to read an appointment through this service.
- ⚠️ Error signalling is string-sniffing: success is `!eventResponse.ToLower().StartsWith("exception")` (`:110`, `:134`, `:159`). A legitimate payload beginning with "exception" would be misread as failure.
- ⚠️ Both trailing-slash paths (`/appointments/`) differ from the POST path (`/appointments`), so clients must vary the trailing slash per verb.

### Calendar — `api/v1/calendar` (`Calendar/Program.cs:88`)

| Verb | Path | Auth | Handler line | Returns |
|------|------|------|--------------|---------|
| GET | `/availability/{email}` | ✅ `:119` | `:93` | `200 Ok<List<DateTime>>`, `404` |
| GET | `/appointments/{email}` | ✅ `:141` | `:121` | `200 Ok<List<AppointmentEntity>>`, `404` |

- Both are cached via `cache.GetOrCreateAsync($"availability-{email}")` / `$"appointments-{email}"` (`:101`, `:129`).
- ⚠️ **Neither calls `OwnershipGuard`.** `RequireAuthorization()` proves the caller holds a valid JWT but *not* that `{email}` is theirs — any authenticated user can read any provider's appointment list. Compare `Provider/Program.cs:182`, which does guard. Cross-reference `13-security.md`.

### Customer — `/api/v1/customers` (`Customer/Program.cs:88`)

| Verb | Path | Auth | Handler line | Returns |
|------|------|------|--------------|---------|
| POST | `/` | ✅ `:122` | `:93` | `201 Created<CustomerEntity>`, `400` |
| PUT | `/{email}` | ✅ `:144` | `:124` | `202 Accepted`, `400`, `403`, `404` |
| GET | `` (group root) | ❌ **anonymous** | `:146` | `200 Ok<List<CustomerEntity>>`, `204` |
| GET | `/{email}` | ❌ **anonymous** | `:160` | `200 Ok<CustomerEntity>`, `404` |

- ⚠️ **The two GETs are unauthenticated.** `GET /api/v1/customers` returns **every customer record including email addresses** to any caller. PII exposure — `CONSTITUTION.md` §4 flags email as PII. Highest-severity route-level finding.
- PUT guards ownership at `:133`.

### Provider — `/api/v1/providers` (`Provider/Program.cs:93`)

| Verb | Path | Auth | Handler line | Returns |
|------|------|------|--------------|---------|
| POST | `/` | ✅ `:129` | `:100` | `201 Created<ProviderEntity>`, `400` |
| GET | `` (group root) | ❌ **anonymous** | `:132` | `200 Ok<List<ProviderEntity>>`, `204` |
| GET | `/{email}` | ❌ **anonymous** | `:150` | `200 Ok<ProviderEntity>`, `404` |
| PUT | `/{email}` | ✅ `:193` | `:171` | `202 Accepted`, `400`, `403`, `404` |

- ⚠️ **`GET /api/v1/providers` is anonymous and returns the full `ProviderEntity`** — which embeds `ServiceEntities`, **`AppointmentEntities`** (with customer emails), and `SubscribedCustomerCollection` (`Library/Entities/ProviderEntity.cs:38-42`). An unauthenticated caller gets every provider's entire appointment book and customer list. This is the most serious data exposure in the API surface.
- ⚠️ Duplicate-check happens before validation ordering matters: `topicName` is computed at `:111` but only used in the *failure* message at `:125` — dead on the success path.
- POST does **not** call `OwnershipGuard`, so an authenticated Customer can create a Provider record for an arbitrary email.

### Services — `api/v1/services` (`Services/Program.cs:89`)

| Verb | Path | Auth | Handler line | Returns |
|------|------|------|--------------|---------|
| GET | `/{email}` | ❌ **anonymous** | `:94` | `200 Ok<List<ServiceEntity>>`, `404` |
| PUT | `/{email}` | ✅ `:135` | `:113` | `200 Ok<ProviderEntity>`, `400`, `403`, `404` |
| PATCH | `/{email}` | ✅ `:159` | `:137` | `200 Ok<ProviderEntity>`, `400`, `403`, `404` |

- PUT **appends** services (`AddServicesToProviderEvent`), PATCH **replaces** them (`UpdateServicesFromProviderEvent`) — the inverse of the usual REST reading of those verbs. ⚠️ Semantic surprise.
- Both mutating routes guard ownership (`:122`, `:146`).
- ⚠️ Both return the **whole `ProviderEntity`** (appointments + customer list) as the success body.

### Profession — `api/v1/professions` (`Profession/Program.cs:88`)

| Verb | Path | Auth | Handler line | Returns |
|------|------|------|--------------|---------|
| POST | `/` | ✅ `:121` | `:93` | `201 Created<ProfessionEntity>`, `400` |
| GET | `` (group root) | ❌ anonymous | `:123` | `200 Ok<List<ProfessionEntity>>`, `204` |
| GET | `/{name}` | ❌ anonymous | `:136` | `200 Ok<ProfessionEntity>`, `404` |

- Professions are reference data (seeded from `Library/Data/ProfessionSeedData.cs`), so anonymous reads here are defensible.
- POST is authenticated but **not role-gated** — any Customer can add a profession to the global catalogue.

### Identity — `api/v1/auth` (`Identity/Program.cs:94`) + one root route

| Verb | Path | Auth | Handler line | Returns |
|------|------|------|--------------|---------|
| POST | `api/v1/auth/register` | ❌ anonymous | `:98` | `201 Created`, `400`, `409 Conflict`, `503` |
| POST | `api/v1/auth/login` | ❌ anonymous | `:118` | `200 Ok`, `401`, `503` |
| POST | `api/v1/auth/refresh` | ❌ anonymous | `:129` | `200 Ok`, `401`, `503` |
| POST | `api/v1/auth/logout` | ❌ anonymous | `:142` | `204 NoContent`, `503` |
| POST | **`/device-token`** | ✅ `:170` | `:154` | `200 Ok`, `400`, `401` |

- Register/login/refresh all return `{ accessToken, refreshToken }`.
- Register validates inline (`:100-106`): email format, password ≥ 8 chars, role ∈ {`Provider`, `Customer`}.
- ⚠️ **`POST /device-token` is mapped on `app`, not on the `auth` group** (`:154`) — so its path is `/device-token`, *not* `/api/v1/auth/device-token`. It is the only route in the solution outside the `api/v1/` convention. The security comment at `:81-86` refers to it as `POST /api/v1/auth/device-token`, which does not match the actual route. Documentation/code drift.
- Identity is the only service that **skips antiforgery** (`:87`, deliberate — API-only) and that makes HTTPS redirection conditional (`:91-92`).

---

## Cross-cutting API conventions

- **Route groups:** every domain uses `app.MapGroup("api/v1/<domain>")` with `.WithTags("<Domain>API")`.
- **Validation:** `MiniValidation` (`MiniValidator.TryValidate`) in Booking, Customer, Provider, Services, Profession. Calendar has no request bodies. Identity hand-rolls its validation instead (`Identity/Program.cs:100-106`).
- **Error envelope:** RFC 7807 ProblemDetails, with `requestId` injected from `Activity.Current?.Id` — see `10-error-handling.md`.
- **Named endpoints:** every route carries `.WithName(...)`, so link generation is available even though nothing uses it.
- ⚠️ **Inconsistent group prefixes:** Booking/Calendar/Services/Profession use `"api/v1/…"` (no leading slash); Customer/Provider use `"/api/v1/…"`. ASP.NET normalises both, but the inconsistency is visible in generated Swagger.

---

## ⚠️ Contract drift: mobile client vs backend

The only client is `MobileApp`. Its API services build URLs relative to a single `ApiBaseUrl` (`MobileApp/MauiProgram.cs:32,38`). Every path is wrong:

| Mobile call | Anchor | Actual backend route | Verdict |
|---|---|---|---|
| `GET booking?date=yyyy-MM-dd` | `MobileApp/Services/BookingApiService.cs:23` | *(none — Booking exposes no GET)* | ❌ 404 |
| `GET booking/{id}` | `BookingApiService.cs:38` | *(none)* | ❌ 404 |
| `PUT booking/{id}` | `BookingApiService.cs:53` | `PUT api/v1/booking/appointments/` | ❌ 404 (path + shape) |
| `GET calendar?from=…&days=…` | `CalendarApiService.cs:23` | `GET api/v1/calendar/availability/{email}` | ❌ 404 |
| `POST api/v1/auth/login` | `AuthService.cs:31` | `POST api/v1/auth/login` | ✅ matches |
| `POST api/v1/auth/register` | `AuthService.cs:57` | `POST api/v1/auth/register` | ✅ matches |
| `POST device-token` | `PushNotificationService.cs:64` | `POST /device-token` | ✅ matches |

**Two independent defects compound here:**
1. **Path prefix:** all domain calls omit `api/v1/` and use singular collection names (`booking`, `calendar`) that no route group declares.
2. **Base address:** a single `ApiBaseUrl` cannot address seven processes on seven ports. `MobileApp/appsettings.json:2` is `https://localhost` (port 443) and `appsettings.Development.json:2` is `https://localhost:5001` — **neither matches any service port** (6030–6036), and no service is configured for HTTPS on those ports.

**Consequence:** only the three Identity routes are reachable, and only if `ApiBaseUrl` is corrected to `:6036`. Every domain read fails, which is why the ViewModels fall back to `MobileApp/Services/SeedDataProvider.cs`. See `16-mobile-client.md`.

---

## Deviations from what the design documents claim

- `CONSTITUTION.md` §4 requires "input validation via `MiniValidator` … at every API endpoint". Identity validates by hand; Calendar validates nothing (no bodies).
- `CONSTITUTION.md` §4 requires HTTPS redirection in all services — Identity makes it conditional on non-Development (`Identity/Program.cs:91`).
- F-004 `appointment-lifecycle` shipped "book, confirm, update, cancel, complete", but the HTTP surface offers only POST/PUT/DELETE on Booking and no explicit confirm/complete transition route; status changes ride inside the PUT body. See `03-services.md` and `15-cqrs-and-messaging.md` for the enum-transition gap.

## What is missing

- No `GET` route for a single appointment anywhere in the solution.
- No pagination, filtering, or sorting on any list endpoint — `GET /api/v1/providers` and `GET /api/v1/customers` return unbounded collections.
- No rate limiting (`AddRateLimiter` appears nowhere).
- No CORS policy registered in any service.
- No API versioning package — `v1` is a literal string in the route, so there is no version negotiation.
- No `/health` or `/ready` endpoint on any service (see `12-observability.md`).
