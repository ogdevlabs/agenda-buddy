# API Contracts: auth-and-identity

**Feature:** F-001
**Date:** 2026-07-30
**PRD:** docs/pdlc/prds/PRD_auth-and-identity_2026-07-30.md

---

## Base URL

`/auth` — all endpoints served by the Identity microservice.

## Authentication

All four endpoints are **unauthenticated** — they are the entry points for obtaining tokens. No `Authorization` header is required or expected.

---

## POST /auth/register

Creates a new credential record and issues a token pair.

### Request

```
POST /auth/register
Content-Type: application/json
```

```json
{
  "email": "provider@example.com",
  "password": "securepass123",
  "role": "Provider"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `email` | string | Yes | Valid email format (`[EmailAddress]`); normalized to lowercase |
| `password` | string | Yes | Minimum 8 characters; must not be empty or whitespace |
| `role` | string | Yes | Must be `"Provider"` or `"Customer"` |

### Response — 201 Created

```json
{
  "accessToken": "eyJhbGci...",
  "refreshToken": "d4f8a1b2c3e4..."
}
```

| Field | Type | Notes |
|-------|------|-------|
| `accessToken` | string | RS256-signed JWT; TTL 60 min |
| `refreshToken` | string | Opaque token; TTL 24 hr; single-use |

**JWT claims:**

| Claim | Value |
|-------|-------|
| `sub` | User's email (lowercase) |
| `role` | `"Provider"` or `"Customer"` |
| `jti` | Unique token ID (GUID) |
| `iss` | `"agenda-buddy-identity"` |
| `exp` | UTC timestamp: now + 60 min |

### Error Responses

| Status | Condition | Body |
|--------|-----------|------|
| 400 | Malformed email | `{ "error": "validation_error", "message": "Invalid email format." }` |
| 400 | Password empty, whitespace, or < 8 chars | `{ "error": "validation_error", "message": "Password must be at least 8 characters." }` |
| 400 | Invalid role value | `{ "error": "validation_error", "message": "Role must be 'Provider' or 'Customer'." }` |
| 409 | Email already registered | `{ "error": "conflict", "message": "An account with this email already exists." }` |
| 503 | MongoDB unavailable | `{ "error": "service_unavailable", "message": "Authentication service temporarily unavailable." }` |

---

## POST /auth/login

Validates credentials and issues a token pair.

### Request

```
POST /auth/login
Content-Type: application/json
```

```json
{
  "email": "provider@example.com",
  "password": "securepass123"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `email` | string | Yes | Valid email format |
| `password` | string | Yes | Non-empty |

### Response — 200 OK

```json
{
  "accessToken": "eyJhbGci...",
  "refreshToken": "d4f8a1b2c3e4..."
}
```

Same structure as register. A new refresh token is issued on each login — any prior active session is overwritten.

### Error Responses

| Status | Condition | Body |
|--------|-----------|------|
| 401 | Unknown email or wrong password | `{ "error": "unauthorized", "message": "Invalid credentials." }` |
| 503 | MongoDB unavailable | `{ "error": "service_unavailable", "message": "Authentication service temporarily unavailable." }` |

> **Security note:** Unknown email and wrong password return identical 401 responses to prevent user enumeration.

---

## POST /auth/refresh

Exchanges a valid refresh token for a new access token. Single-use: the submitted token is deleted and a new one is issued.

### Request

```
POST /auth/refresh
Content-Type: application/json
```

```json
{
  "refreshToken": "d4f8a1b2c3e4..."
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `refreshToken` | string | Yes | Non-empty |

### Response — 200 OK

```json
{
  "accessToken": "eyJhbGci...",
  "refreshToken": "a9b8c7d6e5f4..."
}
```

New token pair. Old refresh token is immediately deleted (single-use rotation).

### Error Responses

| Status | Condition | Body |
|--------|-----------|------|
| 401 | Token not found, already used, or expired | `{ "error": "unauthorized", "message": "Refresh token is invalid or expired." }` |
| 503 | MongoDB unavailable | `{ "error": "service_unavailable", "message": "Authentication service temporarily unavailable." }` |

> **Concurrent refresh behavior:** If two requests arrive with the same refresh token, `FindOneAndDeleteAsync` ensures only one succeeds. The second finds no document → 401. The losing client must re-authenticate via `/auth/login`.

---

## POST /auth/logout

Invalidates the refresh token. Idempotent — calling it on an already-deleted or expired token still returns 204.

### Request

```
POST /auth/logout
Content-Type: application/json
```

```json
{
  "refreshToken": "d4f8a1b2c3e4..."
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `refreshToken` | string | Yes | Non-empty |

### Response — 204 No Content

Empty body.

### Error Responses

| Status | Condition | Body |
|--------|-----------|------|
| 503 | MongoDB unavailable | `{ "error": "service_unavailable", "message": "Authentication service temporarily unavailable." }` |

> **Note:** After logout the access token remains valid until its natural 60-minute expiry. This is the accepted passive-expiry trade-off (v1, no jti blocklist).

---

## Consumer Service Auth Behavior

No new endpoints are added to the six consumer services. The following behavior is enforced via middleware and handlers.

### Middleware (all protected endpoints)

| Condition | Response |
|-----------|----------|
| No `Authorization` header | 401 `{ "error": "unauthorized", "message": "Authentication required." }` |
| Malformed or expired JWT | 401 `{ "error": "unauthorized", "message": "Token is invalid or expired." }` |
| `alg` header is not `RS256` | 401 (rejected by `ValidAlgorithms` check before claim extraction) |
| JWT valid but role insufficient | 403 `{ "error": "forbidden", "message": "You do not have permission to perform this action." }` |

### Handler Ownership Checks

Applied after middleware passes. Returns 403 with the same forbidden body if `HttpContext.User` sub does not match the entity's email field.

| Service | Endpoints requiring ownership check | Check |
|---------|-------------------------------------|-------|
| Booking | Mutating appointment endpoints | `sub == EmailProvider` OR `sub == EmailCustomer` |
| Calendar | Provider availability mutations | `sub == provider email on the calendar record` |
| Provider | Profile update endpoints | `sub == ProviderEntity.Email` |
| Customer | Profile update endpoints | `sub == CustomerEntity.Email` |

**Customer service write endpoints** (add/update customer records) require a Provider token — a Customer token receives 403 regardless of ownership match.

---

## JWT Validation Parameters (consumer services)

Configured by `AddAgendaBuddyAuthentication()` in Library:

```csharp
new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = "agenda-buddy-identity",
    ValidateAudience = false,
    ValidateLifetime = true,
    ClockSkew = TimeSpan.Zero,          // No tolerance — expired means expired
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = rsaSecurityKey,  // RSA public key from JWT_PUBLIC_KEY env var
    ValidAlgorithms = new[] { "RS256" } // Reject alg:none and HS256
}
```

---

## Rate Limiting

None in v1. Documented as a known gap — brute-force protection on `/auth/login` is deferred to a future security-hardening feature.
