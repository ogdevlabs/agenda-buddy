# 01 — API Surface

> **⚠️ F-016 delta (2026-08-18, `v0.2.0`) — refreshed 2026-08-22 at the ship gate. The authz column and
> anchors below ARE current for the seven routes F-016 changed; everything else still dates from 2026-08-15.**
>
> **The five anonymous PII GETs are gone.** `providers`, `providers/{email}`, `customers`,
> `customers/{email}` and `services/{email}` all require authentication and return **401** anonymous —
> verified live at the ship gate, not inferred. `professions*` stays anonymous as reference data.
> **Both Calendar routes are now ownership-guarded** *before* the cache read, closing the IDOR.
> **`POST /api/v1/professions` was deleted** rather than role-gated (ADR-025) — it answers **405**.
> **`204` is retired** (ADR-023): list routes always return a parseable body.
> **Both list routes are paginated** — `{items, totalCount, page, pageSize}` with a clamped, capped
> `pageSize`; the cap is a security control, since an uncapped page size restores the full-dataset dump.
> **Non-owners receive `ProviderSummary`** (`email`, `firstName`, `lastName`, `services`), never the embedded
> appointment book or customer roster.
>
> **A generated OpenAPI spec now exists** — `docs/api/openapi/<Service>.json` plus an index, produced on
> demand by `scripts/generate-openapi.sh`. It supersedes this file's claim that no spec is committed. There
> is also a Bruno collection under `bruno/agenda-buddy/` encoding the expected status codes.
>
> **F-013 delta, still true:** every service also exposes anonymous `GET /health` (readiness, 5s-cached
> MongoDB check) and `GET /alive` (liveness). Ports are **dynamically assigned by the Aspire AppHost**, so
> any 603x port number here is wrong under the AppHost — it is right only for a standalone run.
>
> Sources: `docs/pdlc/episodes/EPISODE_secure-public-endpoints_2026-08-18.md`,
> `docs/pdlc/design/secure-public-endpoints/verification.md`, ADR-022…031.


**Source of truth:** the route registrations in each service's `Program.cs`. Swashbuckle 10.2.3 serves a spec at runtime **only in `Development`** (`Booking/Program.cs:38-41` and the equivalent block in each service) — which is why it is absent under the AppHost, whose services do not run as `Development`. A **generated, committed** copy now lives at `docs/api/openapi/<Service>.json` (regenerate with `scripts/generate-openapi.sh`, which boots each service standalone as `Development` against a throwaway Mongo). Treat it as a build artifact: accurate as of its own timestamp, not hand-maintained.

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

- Both are cached via `cache.GetOrCreateAsync($"availability-{email}")` / `$"appointments-{email}"`.
- ✅ **Both now call `OwnershipGuard`, and they call it *before* the cache read** (F-016). Ordering matters: guarding after the read would still serve a cached body to a non-owner. Previously `RequireAuthorization()` proved only that the caller held a valid JWT, not that `{email}` was theirs — any authenticated user could read any provider's appointment book. Both answer **401** anonymous and **403** for an authenticated non-owner. Cross-reference `13-security.md`.

### Customer — `/api/v1/customers` (`Customer/Program.cs:88`)

| Verb | Path | Auth | Handler line | Returns |
|------|------|------|--------------|---------|
| POST | `/` | ✅ `:122` | `:93` | `201 Created<CustomerEntity>`, `400` |
| PUT | `/{email}` | ✅ `:144` | `:124` | `202 Accepted`, `400`, `403`, `404` |
| GET | `` (group root) | ✅ `:215` *(F-016)* | `:183` | `200 Ok<PagedResponse<CustomerEntity>>`, `401` |
| GET | `/{email}` | ✅ `:232` *(F-016)* | `:217` | `200 Ok<CustomerEntity>`, `401`, `404` |

- ✅ **The two GETs are authenticated as of F-016** and paginated; `204` is retired. Both answer **401** anonymous.
- ⚠️ **Still returns the full `CustomerEntity`** — including `SubscribedProviderCollection`, `AppointmentCollection` and `KafkaTopic` — to *any* Provider-role caller, not just the owner. Owner-scoping was deliberately deferred (ADR-026); quantified as review finding I-2. So authentication closed the anonymous leak here, not the over-sharing.
- PUT guards ownership at `:181`.

### Provider — `/api/v1/providers` (`Provider/Program.cs:93`)

| Verb | Path | Auth | Handler line | Returns |
|------|------|------|--------------|---------|
| POST | `/` | ✅ `:174` — **role + owner** *(F-016)* | `:132` | `201 Created<ProviderEntity>`, `400`, `401`, `403` |
| GET | `` (group root) | ✅ `:221` *(F-016)* | `:177` | `200 Ok<PagedResponse<ProviderSummary>>`, `401` |
| GET | `/{email}` | ✅ `:258` *(F-016)* | `:224` | `200 Ok<ProviderEntity>` **(owner)** / `Ok<ProviderSummary>` **(non-owner)**, `401`, `404` |
| PUT | `/{email}` | ✅ `:284` | `:262` | `202 Accepted`, `400`, `403`, `404` |

- ✅ **The anonymous full-entity dump is closed** (F-016, the feature's central claim). Reads now require a JWT, and a non-owner receives `ProviderSummary` — `email`, `firstName`, `lastName`, `services` — so the embedded `AppointmentEntities` (carrying customer emails) and `SubscribedCustomerCollection` (`Library/Entities/ProviderEntity.cs:38-42`) never leave the service for anyone but the owner. Verified live at the ship gate: owner got 9 fields, a second authenticated user got 4.
- ⚠️ **The list is homogeneous** — every element is a `ProviderSummary`, *including the caller's own record*. `api-contracts.md` §5.1 describes owner-gets-full for the list route too; that was rejected during construction because a mixed `items` array is not deserialisable into a typed list. Owner-gets-full applies to `/{email}` only.
- ⚠️ The list is served through a 5-minute cache with **no invalidation on write** (`agenda-buddy-xrw`), so a newly created provider is missing from it for up to five minutes. The projection is applied *after* the cache read, so the cache holds unprojected entities — correct today, a trap for F-019/F-020 (review finding I-1).
- ✅ POST now requires **both** the Provider role and ownership of the email, so an authenticated Customer can no longer create a Provider record for an arbitrary address.
- ⚠️ `topicName` is still computed and used only in the *failure* message — dead on the success path.

### Services — `api/v1/services` (`Services/Program.cs:89`)

| Verb | Path | Auth | Handler line | Returns |
|------|------|------|--------------|---------|
| GET | `/{email}` | ✅ `:147` *(F-016)* | `:125` | `200 Ok<List<ServiceEntity>>`, `401`, `404` |
| PUT | `/{email}` | ✅ `:171` | `:149` | `200 Ok<ProviderEntity>`, `400`, `403`, `404` |
| PATCH | `/{email}` | ✅ `:195` | `:173` | `200 Ok<ProviderEntity>`, `400`, `403`, `404` |

- PUT **appends** services (`AddServicesToProviderEvent`), PATCH **replaces** them (`UpdateServicesFromProviderEvent`) — the inverse of the usual REST reading of those verbs. ⚠️ Semantic surprise.
- Both mutating routes guard ownership (`:122`, `:146`).
- ⚠️ Both return the **whole `ProviderEntity`** (appointments + customer list) as the success body.

### Profession — `api/v1/professions` (`Profession/Program.cs:88`)

| Verb | Path | Auth | Handler line | Returns |
|------|------|------|--------------|---------|
| ~~POST~~ | ~~`/`~~ | **route deleted** *(F-016, ADR-025)* | — | `405 Method Not Allowed` |
| GET | `` (group root) | ❌ anonymous — **by design** | `:136` | `200 Ok<List<ProfessionEntity>>` |
| GET | `/{name}` | ❌ anonymous — **by design** | `:149` | `200 Ok<ProfessionEntity>`, `404` |

- Professions are reference data (seeded from `Library/Data/ProfessionSeedData.cs`), so anonymous reads here are deliberate, and F-016 left them open on purpose.
- ✅ **`POST` was deleted, not role-gated** (ADR-025): the catalogue is seeded, and no product requirement asked for runtime additions, so the route was removed rather than defended. It now answers **405** — confirmed at the ship gate.

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
2. **Base address:** a single `ApiBaseUrl` cannot address seven processes on seven ports. `MobileApp/appsettings.json:2` is `https://localhost` (port 443) and `appsettings.Development.json:2` is `https://localhost:5001` — neither matches any service port, and no service serves HTTPS on those ports.
3. **Neither settings file is ever loaded.** *(Verified 2026-08-22.)* `MauiApp.CreateBuilder()` registers no configuration source, nothing in `MobileApp` calls `AddJsonFile`/`AddJsonStream`, and the two files are not embedded resources — so `Configuration["ApiBaseUrl"]` is always null and **both clients always use the hardcoded fallback `http://localhost:6036/`** (`MauiProgram.cs:32,38`). The configured values above are therefore dead text, not competing candidates.
4. **There are no fixed ports left to be right about.** F-013 made the AppHost assign ports dynamically (`AppHostWiring.cs` nulls each endpoint's `Port`/`TargetPort`), so `6036` addresses nothing under the AppHost. The 603x numbers survive only for a standalone or Compose run.

**Consequence:** every domain read fails and the ViewModels fall back to `MobileApp/Services/SeedDataProvider.cs`, so the app shows canned data. Only the Identity routes could ever match, and only in a standalone run. This is **F-015**'s scope, and it is why F-016 could change five route contracts at zero consumer cost. See `16-mobile-client.md`.

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
