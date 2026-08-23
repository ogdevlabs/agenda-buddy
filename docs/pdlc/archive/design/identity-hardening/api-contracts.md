# API Contracts — Identity Hardening (F-021)

**Date:** 2026-08-22 · **PRD:** [`PRD_F-021_identity-hardening_2026-08-22.md`](../../prds/PRD_F-021_identity-hardening_2026-08-22.md)

---

## Summary

**No new endpoints. No changed request or response bodies. No breaking change.**

F-021 adds one new **status code** (`429`) to two existing routes and changes the *conditions* under which two existing codes are returned. Every success shape is byte-identical to today, so F-015's mobile-contract work inherits nothing new to bind.

Current shapes below are taken from the generated spec (`docs/api/openapi/Identity.json`) and the route handlers (`Identity/Program.cs:114-165`), not from memory.

| Route | Today | After F-021 |
|---|---|---|
| `POST /api/v1/auth/login` | `200`, `401`, `503` | **+ `429`**; `401` additionally covers a locked account |
| `POST /api/v1/auth/register` | `201`, `400`, `409`, `503` | **+ `429`** |
| `POST /api/v1/auth/refresh` | `200`, `401`, `503` | `401` additionally covers a locked account |
| `POST /api/v1/auth/logout` | `204`, `503` | unchanged |
| `POST /device-token` | `200` | unchanged |

⚠️ The generated spec currently advertises only `200` for these routes because the handlers return `IResult` without `Produces` metadata. That under-documentation is **pre-existing** and outside F-021's scope, but it means the regenerated spec will still not show `429`. Noted so nobody reads the spec as evidence that the limiter is absent. (`scripts/generate-openapi.sh` regenerates it; F-018's T16/T17 own spec-drift.)

---

## 1. `POST /api/v1/auth/login`

**Auth:** anonymous. **Rate limited:** yes, per-IP — 10 requests/minute, sliding window, when `Security:RateLimiting:Enabled` is `true`.

### Request

```json
{ "email": "ada@example.com", "password": "correct horse battery staple" }
```

| Field | Type | Required | Validation |
|---|---|---|---|
| `email` | string | yes | Not validated as an email here — an invalid address simply fails to match a credential (deliberate: it keeps the response identical for malformed and unknown addresses) |
| `password` | string | yes | No format check on login |

### Responses

**`200 OK`** — unchanged shape:

```json
{ "accessToken": "eyJhbGciOiJSUzI1NiIs…", "refreshToken": "b64-opaque-32-bytes" }
```

Side effect, new: if `failed_attempts` was non-zero it is reset to `0` and `lock_until` is unset (AC-10). No write occurs when the counter is already `0`.

**`401 Unauthorized`** — empty body, as today (`Results.Unauthorized()`). Returned for **all** of:

- unknown email — still costing a constant-time dummy-hash verify (threat T-005, `IdentityService.cs:96`)
- wrong password — side effect, new: `$inc failed_attempts`, and `$set lock_until` when the threshold is reached
- **a locked account** — new condition. Checked **before** `BCrypt.Verify`, so a locked account costs no CPU

> **The three are deliberately indistinguishable** (AC-7, PRD requirement 12). A distinct code or body for "locked" would tell an attacker which addresses exist and which they have successfully locked. This is the same reasoning that produced the existing dummy-hash timing mitigation, extended to the new state.

**`429 Too Many Requests`** — **new**. Emitted by the rate-limiter middleware *before* the handler runs, so no BCrypt work and no database access occur.

```
HTTP/1.1 429 Too Many Requests
Retry-After: 42
```

Body: empty, or the middleware's default. `Retry-After` in seconds is required (PRD requirement 12) so an honest client can back off correctly.

**`503 Service Unavailable`** — unchanged: ProblemDetails with `title: "service_unavailable"` when Mongo is unreachable.

---

## 2. `POST /api/v1/auth/register`

**Auth:** anonymous. **Rate limited:** yes — **same per-IP policy as login**, and for the same reason: `RegisterAsync` hashes at work factor 12 (`IdentityService.cs:50`), so an unauthenticated request buys **262 ms of server CPU** exactly as login does. Limiting login alone would leave an equal-cost amplification vector open (`ARCHITECTURE.md` §2, D-4).

### Request

```json
{ "email": "ada@example.com", "password": "at least 8 chars", "role": "Provider" }
```

Validation is unchanged (`Identity/Program.cs:116-122`): email format via `EmailAddressAttribute`, password ≥ 8 characters, role exactly `Provider` or `Customer`.

### Responses

Unchanged — `201 Created` with `{ accessToken, refreshToken }`; `400` with `{ error: "validation_error", message }`; `409` with `{ error: "conflict", message }`; `503` ProblemDetails.

**`429 Too Many Requests`** — **new**, as §1.

> **Note on ordering:** the limiter runs before validation, so a malformed body from a throttled IP gets `429` rather than `400`. That is correct — rejecting cheaply is the point — but it means a client cannot distinguish "my payload is wrong" from "I am throttled" while throttled. Acceptable for an anonymous route.

---

## 3. `POST /api/v1/auth/refresh`

**Auth:** anonymous (the refresh token *is* the credential). **Rate limited:** **no** — deliberately. Refresh spends no BCrypt, so it is not a CPU-amplification vector, and throttling it would risk breaking a legitimate client's hourly rotation.

### Request

```json
{ "refreshToken": "b64-opaque-32-bytes" }
```

### Responses

**`200 OK`** — unchanged shape `{ accessToken, refreshToken }`.

**What changed is underneath:** rotation is now a single `FindOneAndUpdateAsync` that matches on the presented hash, a future expiry, **and** the account not being locked, then `$set`s the new token document. The credential is **never deleted**, so no fault can destroy the account (AC-1, AC-2). Single-use semantics are preserved by the old hash being part of the *filter* — a replayed token matches nothing (AC-3).

**`401 Unauthorized`** — empty body, as today. Now covers:

- an unknown, expired, or already-used token (as today)
- **a locked account** — new (AC-4). Without this, lockout would be bypassable by any client holding a live refresh token, and the mobile client holds one for 24 hours

**`503 Service Unavailable`** — unchanged.

---

## 4. Cross-cutting: response headers on all 7 services

When `Security:Hsts:Enabled` is `true`, every response served **over TLS** carries:

```
Strict-Transport-Security: max-age=<configured>
```

Conservative defaults, per `ARCHITECTURE.md` §8: **no `includeSubDomains`, no `preload`** unless the deployment opts in — both are difficult to reverse. The header is **not** emitted over plain HTTP (AC-13), which is what keeps a local run from poisoning `localhost` in a browser's HSTS cache.

`UseHttpsRedirection` continues to issue `307` upgrades, but now runs **before** `UseAuthentication`, so an unauthenticated redirect no longer parses a bearer token first (PRD requirement 13). Both are registered via one shared `UseAgendaBuddyTransportSecurity()` extension in `ServiceDefaults`, called from each service's pipeline.

---

## 5. Configuration surface

Not an HTTP contract, but it is the contract between this feature and its operators.

| Key | Type | Default | Effect |
|---|---|---|---|
| `Security:RateLimiting:Enabled` | bool | `false` | Registers the limiter on `login` + `register` |
| `Security:RateLimiting:PermitPerMinute` | int | `10` | Per-IP sliding-window allowance |
| `Security:Lockout:MaxFailedAttempts` | int | `10` | Consecutive failures before a lock |
| `Security:Lockout:WindowMinutes` | int | `15` | How long a lock lasts before it self-clears |
| `Security:Hsts:Enabled` | bool | `false` | Emits `Strict-Transport-Security` over TLS |
| `Security:Hsts:MaxAgeDays` | int | `30` | `max-age`. Conservative; raise deliberately |

**Both `Enabled` flags default to `false`** so a local AppHost run is unobstructed (AC-14) — services run as **Production** locally, so environment cannot carry this distinction (`ARCHITECTURE.md` D-6). The cloud configuration sets both `true`, the integration harness sets them `true` deliberately to assert `429` and the HSTS header (AC-15), and **each service warns loudly at startup** when a flag is off while it is not running locally (D-7).

Thresholds come from measurement, not convention: at 262 ms per attempt, 10 requests/minute is ≈ 2.6 s of CPU per minute per IP, while a legitimate user needs 2–3 attempts — roughly a 3× margin.
