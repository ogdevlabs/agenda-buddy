# API Contracts — Secure Public Endpoints (F-016)

**Feature:** `secure-public-endpoints` (F-016)
**Date:** 2026-08-18
**Owner:** Neo (Architect)

> **No new endpoints.** F-016 changes the **authorization, response shape, and status-code behaviour** of nine existing routes. Every path and verb stays as-is.
>
> ### ⚠️ Revised at the Step 12 approval gate, 2026-08-18
>
> Three threat-model decisions changed this contract after it was first written:
> - **T-003** — `GET /api/v1/customers` now requires the **`Provider` role**, not merely authentication.
> - **T-007** — **`POST /api/v1/professions` is DELETED.** This supersedes PRD requirement 13: there is no route left to role-gate.
> - **T-005** — `Event` gains an `actor` field (no client-visible effect; see `data-model.md` §4a).
>
> ⚠️ **This document is a hand-off artifact.** F-015 `api-gateway-and-mobile-contract` writes the mobile client against §4's paginated shape. PRD AC-16 requires it recorded as an ADR **before the endpoint work closes** — a contract, not documentation hygiene.

---

## 1. Change summary

| Route | Today | After F-016 | Req |
|---|---|---|---|
| `GET /api/v1/providers` | ❌ anonymous · full entity · unpaginated | ✅ **401** if unauthenticated · projected for non-owners · **paginated** | 9, 10, 15 |
| `GET /api/v1/providers/{email}` | ❌ anonymous · full entity | ✅ **401** if unauthenticated · projected for non-owners | 9, 10 |
| `GET /api/v1/customers` | ❌ anonymous · every record · unpaginated | ✅ **401** if unauthenticated · **403 unless `Provider` role** · **paginated** | 9, 15, **T-003** |
| `GET /api/v1/customers/{email}` | ❌ anonymous · enumeration oracle | ✅ **401** if unauthenticated | 9 |
| `GET /api/v1/services/{email}` | ❌ anonymous | ✅ **401** if unauthenticated | 9 |
| `GET /api/v1/calendar/availability/{email}` | ⚠️ auth, **no ownership** | ✅ **403** unless `{email}` is the caller's | 11 |
| `GET /api/v1/calendar/appointments/{email}` | ⚠️ auth, **no ownership** | ✅ **403** unless `{email}` is the caller's | 11 |
| `POST /api/v1/providers` | ⚠️ auth, no role, any email | ✅ **403** unless `Provider` role **and** own email | 12 |
| ~~`POST /api/v1/professions`~~ | ⚠️ auth, no role, any Customer could write global reference data | 🗑️ **DELETED** — route, handler wiring and its `RequestCollection`/`EventsHelper` path removed | **T-007** *(supersedes 13)* |
| `GET /api/v1/professions`, `/{name}` | anonymous | **unchanged** — reference data | 17 |
| *all six domain services* | `ForbiddenException` → **500** unless caught locally | **403** structurally | 14 |

---

## 2. Authentication

Unchanged mechanism: RS256 bearer JWT minted by Identity, validated by `Library.ServerAuth/AuthenticationExtensions.cs`.

```
Authorization: Bearer <RS256 JWT>
```

Claims used by this feature:

| Claim | Used for |
|---|---|
| `sub` (`ClaimTypes.NameIdentifier`) | the email `OwnershipGuard` compares against `{email}`, `OrdinalIgnoreCase` |
| `role` | `AssertRole` — **first call sites in the solution**: `POST /api/v1/providers` (req 12) and `GET /api/v1/customers` (**T-003**). Requirement 13's site no longer exists — `POST /api/v1/professions` is deleted (T-007). |

⚠️ **Inherited property, not changed here:** `ValidateAudience = false` and no `aud` claim is issued, so **all seven services accept any token this issuer minted** (`13-security.md:71`). A token obtained for one service is valid at all of them. Acceptable within a single trust domain; it means these new authorization checks are the *only* thing scoping access, with no audience defence behind them. F-023 revisits it.

⚠️ **Also inherited:** no token revocation. `jti` is minted and never checked, so an access token remains valid up to 60 minutes after logout (`13-security.md:77`). F-023.

---

## 3. Error responses

### 3.1 The central 403 — requirement 14

**Today** a `ForbiddenException` reaches the client as **403 only where an endpoint hand-wrote `try/catch`** — 8 call sites. Anywhere else it is a **500**, and in `Production` a *bare, empty-bodied* 500, because the exception handler is registered inside `if (app.Environment.IsDevelopment())` in all seven services (`10-error-handling.md:9-34`).

**After F-016**, `AgendaBuddyExceptionHandler : IExceptionHandler` is registered **unconditionally** in the six domain services (ARCHITECTURE AD-1):

```http
HTTP/1.1 403 Forbidden
Content-Type: application/problem+json

{
  "type": "about:blank",
  "title": "Forbidden",
  "status": 403,
  "requestId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

- `requestId` comes from the existing `CustomizeProblemDetails` extension (`Activity.Current?.Id`), preserved.
- ⚠️ **`requestId` is returned but not lookupable** — it is not exported to any sink (`10-error-handling.md:138`). Unchanged by this feature; noted so nobody treats it as a support tool yet.
- The existing local `try/catch` sites keep working and must **not** double-handle (PRD AC-14).
- ⚠️ **Corrected during T08 — there are 7 of those sites, not 8.** `Booking:125,:149,:174`,
  `Customer:154`, `Provider:203`, `Services:143,:167`, verified by grep across every production project.
  T08 removes exactly one (Customer's, to satisfy AC-13), leaving **6**.
- ⚠️ **Corrected during T08 — both 403 paths already return this same body.** The design assumed the
  hand-written `TypedResults.Forbid()` sites returned a *bodyless* 403, so that AC-14's "no changed body"
  meant living with two different 403 contracts. Measured: `app.UseStatusCodePages()`, already registered
  in every domain service, converts a bodyless 403 into ProblemDetails. So the contract is **uniform**
  across both mechanisms — better than predicted, nothing for F-015 to special-case. Verified on both
  sides by `CentralForbiddenTest` and `LocalCatchUnaffectedTest`, which assert the identical property set
  `{type, title, status, traceId, requestId}`.

### 3.2 Full status matrix

| Status | When | Body |
|---|---|---|
| `200` | authorized read | see §4/§5 |
| `201` | created | entity |
| `204` | empty list, or idempotent no-op | none |
| `400` | `MiniValidator` failure | `ValidationProblem` ⚠️ **no `requestId`** — the endpoint filter does not intercept `ValidationProblem` (`10-error-handling.md:162`). Pre-existing, unchanged. |
| **`401`** | missing / expired / invalid-signature token | none (framework) |
| **`403`** | authenticated but not owner, or missing role | ProblemDetails per §3.1 |
| `404` | record not found | none |
| `500` | anything unmapped | ⚠️ ProblemDetails in Development; **bare 500 in Production for non-`ForbiddenException`** — see §3.3 |

### 3.3 What AD-1 does and does not fix

**Fixes:** `ForbiddenException` → 403 in every environment, in all six domain services, whether or not the endpoint remembered a `try/catch`.

**Does not fix** — nine exception types still surfacing as 500 (`10-error-handling.md:91-104`), left out deliberately because no AC covers them and each changes an untouched endpoint's contract:

| Exception | Origin | Surfaces as | Should be |
|---|---|---|---|
| `ArgumentException("Provider not found")` | `ProviderService.cs:23` | 500 | 404 |
| `ArgumentException("Customer Not Found")` | `CustomerService.cs:18` | 500 | 404 |
| `KeyNotFoundException` | `NoteService`, `PaymentService`, `ReportingService` | 500 | 404 |
| `UnauthorizedAccessException` | `NoteService.cs:36,50` | 500 | 403 |
| `InvalidOperationException` | `AppointmentEntity.cs:56,64` | 500 | 409/400 |
| **`FormatException`** | `MongoDbRepository.cs:28` — `new ObjectId(badId)` | **500** | **400** |
| `MongoException` / `TimeoutException` | driver | 500 (503 in Identity) | 503 |

`FormatException` is the most likely live 500: any client passing a non-24-hex id to a path reaching `GetByIdAsync` gets a 500. The handler is built so each mapping is a one-line addition later.

---

## 4. Pagination — requirement 15 · **the F-015 contract**

### Request

```http
GET /api/v1/providers?page=1&pageSize=25
Authorization: Bearer <token>
```

| Parameter | Type | Required | Default | Behaviour |
|---|---|---|---|---|
| `page` | `int` | no | `1` | 1-based. `< 1` clamps to `1`. |
| `pageSize` | `int` | no | `25` | **Clamped server-side to `MaxPageSize`.** `< 1` clamps to the default. |

**`MaxPageSize` = 100.** This is a **security control, not ergonomics** — an uncapped `pageSize` restores exactly the full-dataset dump the feature exists to remove.

**Requests are clamped, never rejected.** `pageSize=100000` returns 200 with 100 items and `pageSize: 100` in the envelope. Rejecting with a 400 would tell an attacker the exact boundary and give a client no way to discover the cap; clamping plus echoing the effective value lets an honest client detect it and paginate correctly. ⚠️ **`pageSize` in the response is the effective value after clamping — not what was requested.**

### Response

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "items": [ /* ProviderSummary[] or ProviderEntity[] — see §5 */ ],
  "totalCount": 143,
  "page": 1,
  "pageSize": 25
}
```

| Field | Type | Notes |
|---|---|---|
| `items` | `T[]` | Empty array on a page past the end — **200 with `[]`, not 404.** |
| `totalCount` | `long` | Total matching documents. `long` because `CountDocumentsAsync` returns `long`. |
| `page` | `int` | Echoed, post-clamp. |
| `pageSize` | `int` | **Effective** size, post-clamp. |

⚠️ **Breaking change.** Both list endpoints currently return a bare JSON array. They will return this envelope. This is safe **only** because no client can reach them today (`01-api-surface.md:158` — the mobile client's paths and base URL are both wrong). Doing it now costs nothing; doing it after F-015 means writing the mobile client twice.

⚠️ **`204 No Content` behaviour changes.** Both endpoints currently return `204` for an empty collection. With an envelope, `200` with `items: []` and `totalCount: 0` is more useful — a client always gets a parseable body. **Decided: return `200`, retire the `204`.** Recorded because it is a contract change beyond what requirement 15 literally asks for.

⚠️ **Accepted debt with a named trigger.** `skip`/`limit` degrades linearly with offset. Immaterial at current volumes (synthetic data only); the fix at scale is keyset pagination, which **would change this contract**. Revisit *before* real user data lands, not after — by then F-015 will depend on this shape.

---

## 5. Endpoint detail

### 5.1 `GET /api/v1/providers`

**Auth:** required. **Paginated:** yes.

**Response — caller is NOT the owning provider** (`ProviderSummary`):

```json
{
  "items": [
    {
      "email": "sarah.coach@example.com",
      "firstName": "Sarah",
      "lastName": "Nakamura",
      "profession": "Fitness Coach",
      "services": [ { "name": "60-min PT session", "fee": 65.00, "duration": 60 } ]
    }
  ],
  "totalCount": 143, "page": 1, "pageSize": 25
}
```

**Absent, and this is the whole point:** `appointmentEntities`, `subscribedCustomerCollection`, `kafkaTopic`, `_id`.

**Response — caller IS the owning provider:** their own full `ProviderEntity`, unchanged, including embedded appointments.

| Status | When |
|---|---|
| `200` | authorized (possibly `items: []`) |
| `401` | no/invalid token |

### 5.2 `GET /api/v1/providers/{email}`

**Auth:** required. Same projection rule as §5.1, single object rather than a page (**not** wrapped in the envelope — it is not a list).

| Status | When |
|---|---|
| `200` | found; full entity if owner, `ProviderSummary` otherwise |
| `401` | no/invalid token |
| `404` | no such provider |

⚠️ **Deliberately *not* 403 for a non-owned provider.** Requirement 10 makes it safe to *read* another provider's summary — that is the discovery flow F-003 defines. Only the embedded data is withheld.

### 5.3 `GET /api/v1/customers` · `GET /api/v1/customers/{email}`

**Auth:** required. **`GET /api/v1/customers` (the list) additionally requires the `Provider` role — threat T-003.** List is paginated. `CustomerEntity` has no embedded third-party data, so no projection is applied.

| Status | When |
|---|---|
| `200` | authorized |
| `401` | no/invalid token |
| **`403`** | **list only** — caller's roles do not include `Provider` |
| `404` | (single) no such customer |

> #### ⚠️ Why the list needs a role and not just a token — T-003
>
> Authentication alone is nearly worthless on this route. `POST /api/v1/auth/register` is **anonymous, unverified and unrate-limited**, so an attacker self-registers as a `Customer`, obtains a valid token, and pages through the entire customer table exactly as before — `totalCount` even tells them how many pages to fetch. **Pagination bounds each response; it does not bound extraction.**
>
> Atlas's product question settled it: *who is this endpoint for?* F-003 defines discovery as customers finding **providers**, not each other. There is no flow in which a user lists every customer. The only defensible caller is a provider.
>
> **Deferred, not rejected:** scoping results to the calling provider's own `SubscribedCustomerCollection` is the stronger fix and was considered at the gate. It is a genuine behaviour change and more work; the role check blocks the actual attack path now. Recorded so the stronger option is a known follow-up rather than a forgotten one.

⚠️ **`GET /api/v1/customers/{email}` keeps the 200-vs-404 enumeration oracle**, narrowed but not closed: any authenticated caller can still probe which emails are registered. **Deliberate** — ARCHITECTURE J1 records keeping 404 for consistency with the eight existing call sites. Note the single-record route is **not** role-gated: a customer legitimately reads their own record through it.

### 5.4 `GET /api/v1/services/{email}`

**Auth:** required. Returns the provider's `ServiceEntity[]` — catalogue and fees. Not paginated: bounded by a provider's own catalogue size.

| Status | When |
|---|---|
| `200` / `401` / `404` | as above |

### 5.5 `GET /api/v1/calendar/availability/{email}` · `GET /api/v1/calendar/appointments/{email}`

**Auth:** required (already). **New: `OwnershipGuard` — `{email}` must be the caller's.**

| Status | When |
|---|---|
| `200` | `{email}` is the caller's |
| **`403`** | authenticated but `{email}` belongs to someone else — **the IDOR fix** |
| `401` | no/invalid token |
| `404` | no such provider |

> ### ⚠️ Design invariant — guard before cache
>
> Both routes cache under a key derived **only from `{email}`** — the request *subject*, not the *caller* (`Calendar/Program.cs:101,129`). Today that is safe by accident: with no ownership guard, every authenticated caller is entitled to every entry.
>
> **The guard MUST execute before the cache read.** In this design it does, so the cache stays safe: an unauthorized caller is rejected before any cached value is touched.
>
> **Anyone who later moves the guard after the cache read, or caches the *response* instead of the *data*, creates a cross-tenant leak with no test to catch it.** This ordering is a design invariant, not an implementation detail.
>
> Related, and a likely source of confusing Build failures: `CacheAside` has **no test at all** and returns `default!` on a 500 ms lock timeout, surfacing as a spurious 404/204 (`11-testing.md:90`). An integration test asserting 200-with-appointments can flake on cache timeout. F-016 does not fix that.

### 5.6 `POST /api/v1/providers`

**Auth:** required (already). **New: `AssertRole(user, "Provider")` AND ownership of the target email.**

Both arms are needed. A role check alone still lets one Provider create a record for another provider's email. The endpoint currently has **neither** (`Provider/Program.cs:100-129`).

| Status | When |
|---|---|
| `201` | created |
| `400` | `MiniValidator` failure |
| **`403`** | caller lacks `Provider` role, **or** `providerEntity.email` ≠ caller's `sub` |
| `401` | no/invalid token |

⚠️ Pre-existing oddity left alone: a duplicate email returns **400 with a Kafka error string**, because `KafkaClient` reports an already-existing topic as a failure (`15-cqrs-and-messaging.md:236`).

### 5.7 ~~`POST /api/v1/professions`~~ — 🗑️ **DELETED** (threat T-007)

**The route is removed**, along with its handler wiring and its `RequestCollection` / `EventsHelper` path. After F-016, `POST /api/v1/professions` returns **404/405**. No profession can be created through the API by any role.

| Status | When |
|---|---|
| `404` / `405` | the route no longer exists |

> #### Why deletion rather than a role check
>
> PRD requirement 13 asked for `AssertRole` here. **Bolt found there was no role to check for**: the allow-list minted by Identity is exactly `{Provider, Customer}` (`Identity/Program.cs:100-106`) — **there is no admin tier.** So the only implementable check was `AssertRole(user, "Provider")`, which lets *any* self-registered provider write to shared reference data read by every user. Given registration is open and unthrottled, that raises the bar from "any account" to "any account that picked `Provider` at signup" — marginal.
>
> Professions are **seeded** from `Library/Data/ProfessionSeedData.cs`, and no shipped flow creates one. Removing the surface is strictly stronger than guarding it, needs less code, and avoids inventing an `Admin` role inside a feature that deliberately excludes Identity (ARCHITECTURE §7).
>
> **Rejected alternatives:** introduce an `Admin` role (correct, but touches Identity's allow-list, token minting and seeding — real scope creep); accept `Provider`-only with an ADR (Atlas's preference; defensible pre-launch, but carries the risk in writing for no benefit once deletion is on the table).
>
> ⚠️ **If professions ever need to be user-creatable**, that is a feature with a real authorization model behind it — not a route restored quietly.

**Still anonymous and unchanged:** the two profession **read** routes (§5.8). Deleting the write path does not affect them.

### 5.8 `GET /api/v1/professions` · `/{name}` — unchanged

**Anonymous by design** (requirement 17, AC-18). Reference data seeded from `ProfessionSeedData.cs`; no PII. Listed so the audit trail shows this was decided, not missed.

---

## 6. Not changed

- **Identity's routes and error envelope.** Identity returns ad-hoc `{ error, message }` for 400/409 and is the only service without `ProblemDetailsServiceEndpointFilter` (`10-error-handling.md:146,208`). AD-1 is **not** registered there — two error schemes in one service would be worse than the inconsistency. F-021 touches Identity next.
- **`POST /device-token`** — still mapped on `app`, not the `auth` group, so its real path is `/device-token`, the only route outside the `api/v1/` convention, and the security comment at `Identity/Program.cs:81-86` names a path that does not exist (`01-api-surface.md:125`). F-015's contract work.
- **Booking's routes.** No `GET` exists on Booking at all; `DELETE` takes a `[FromBody]` entity. Untouched.
- **`Services` PUT/PATCH inverted semantics** (PUT appends, PATCH replaces) and both returning the whole `ProviderEntity`. ⚠️ **Note:** these are authenticated and ownership-guarded already, so a provider only ever receives their *own* embedded data — no exposure. Left alone.
- **The string-sentinel error convention** — endpoints branching on `!result.ToLower().StartsWith("exception")`, with `null`, `""` and `"exception…"` as three encodings of failure. Out of scope; touching it would put every write path in this feature's blast radius.
- **Rate limiting** (F-021) · **HTTPS listeners / HSTS / CORS** (none exist today) · **API versioning** (`v1` is a literal string).
