---
feature: auth-and-identity
date: 2026-07-30
status: inception-complete
last-updated: 2026-07-31T05:05:00Z
approved-by: ogdevlabs
approved-date: 2026-07-30T04:20:00Z
prd: docs/pdlc/prds/PRD_auth-and-identity_2026-07-30.md
---

# Brainstorm Log: Auth and Identity

## Divergent Ideation
_Not run._

## Socratic Discovery

**Completed:** 2026-07-30T00:10:00Z
**Interaction mode:** Sketch

### Round 1 — Problem Statement

**Q1:** What problem does this feature solve?
**A:** The system has no authentication or authorization — all API endpoints across all six microservices are publicly accessible. Any caller can read, create, update, or delete any provider, customer, or appointment without proving identity. A provider could see another provider's client list; a customer could cancel any appointment. The system cannot be safely exposed beyond localhost.

**Q2:** Who will use this feature, and in what context?
**A:** Two actor types — Providers (register, log in, manage their own services/customers/appointments) and Customers (register, log in, view and book appointments with subscribed providers). Both interact via the REST API from a future client app.

**Q3:** What does success look like?
**A:** Every endpoint returns 401 for unauthenticated requests. A valid provider token can only access that provider's own data (403 on cross-provider access). A valid customer token can only access customer-permitted endpoints. Authentication in place before any public exposure.

**Q4:** What are the technical constraints and dependencies?
**A:** Six independent ASP.NET Minimal API microservices, each must enforce auth. No existing identity infrastructure. MongoDB as the datastore. .NET 8. JWTs are the stated direction. The Library shared project is the natural home for auth middleware and token validation so all services inherit it without duplication.

### Round 2 — Future State / Key Capabilities

**Q1:** Where will identity data live?
**A:** A new Identity/Auth microservice backed by MongoDB — stores hashed credentials (email + bcrypt password) for both providers and customers, issues JWTs on login. Keeps auth concerns out of the Provider and Customer domain services.

**Q2:** What should a JWT contain — and what should it NOT contain?
**A:** Claims: sub (email), role (Provider | Customer), and a unique jti. Expiry: short-lived access token (15–60 min) + optional refresh token. Should NOT contain PII beyond email or sensitive fields like password hash, Kafka topic, or subscription lists — those stay in the DB.

**Q3:** How should authorization work across the six services?
**A:** Provider token: full CRUD on their own profile, services, appointments, and calendar; read their own customer list. Customer token: read-only on providers/services; create/read/cancel their own appointments. Neither role can touch the other's data. Shared middleware in Library validates the JWT and injects the actor identity so each endpoint can scope queries.

**Q4:** Does registration live here too, or is that F-002/F-003?
**A:** F-001 owns credential creation (email + password) as part of the auth flow. F-002 and F-003 own the rest of the onboarding profile. The split: F-001 = can you log in; F-002/F-003 = do you have a complete profile.

### Round 3 — Acceptance Criteria

**Q1:** Pass/fail criteria for registration and login endpoints?
**A:** POST /auth/register with valid email + password → 201, returns JWT access token. Duplicate email → 409. POST /auth/login with valid credentials → 200, returns JWT. Wrong password or unknown email → 401. Password stored as bcrypt hash — plaintext never persisted.

**Q2:** Pass/fail criteria for endpoint protection?
**A:** No Authorization header → 401. Expired/invalid JWT → 401. Provider endpoint with Customer token → 403. Provider endpoint with another provider's token (cross-provider) → 403. Customer endpoint with Provider token → 403.

**Q3:** Token lifecycle — expiry, refresh, revocation?
**A:** Access token expires in 60 minutes. Refresh token (24-hour TTL) stored server-side in MongoDB so it can be revoked. POST /auth/refresh exchanges a valid refresh token for a new access token. POST /auth/logout invalidates the refresh token.

**Q4:** Test coverage required before ship?
**A:** Unit tests: JWT generation/validation, password hashing, role-claim extraction. Integration tests: all four auth endpoints (register, login, refresh, logout). Auth middleware test: full 401/403 matrix (no token / expired / wrong role / cross-actor). All existing service tests continue to pass (no regression).

## Progressive Thinking (Agent Team Meeting)

**MOM:** docs/pdlc/mom/auth-and-identity_progressive-thinking_mom_2026_07_30.md

### Confirmed Facts
- Six Minimal API services with zero auth; Library is the shared extension point
- JWT (access 60 min, refresh 24 hr server-side MongoDB), bcrypt passwords, roles: Provider | Customer
- New Identity microservice owns /auth/register, /auth/login, /auth/refresh, /auth/logout
- AppointmentEntity links actors via EmailProvider/EmailCustomer string fields
- All six existing test suites must remain green post-implementation

### Accepted Inferences
- Shared `AddAgendaBuddyAuthentication()` extension in Library; one-line adoption per service
- New `credentials` MongoDB collection: unique email index + TTL index on refresh token
- `Microsoft.AspNetCore.Authentication.JwtBearer` NuGet added to Library + all six service `.csproj` files
- New `Identity.Tests` project doubles as shared auth test harness
- Role checks in middleware; entity ownership checks in handlers

### Key Consequences
- Six `Program.cs` files require surgical pipeline changes (ordering is correctness-critical)
- Booking handler logic must validate JWT sub against entity email fields (not just role)
- Incomplete-onboarding state (valid JWT, no profile) must be tolerated by F-002/F-003
- RSA asymmetric signing: private key in Identity only; public key distributed to consumers via env var

### Risks & Unknowns
1. HIGH: RSA private key leakage or hardcoding in config files
2. HIGH: Middleware pipeline order regression across six services
3. MEDIUM: No brute-force protection on login (out of scope, document as known gap)
4. MEDIUM: 60-min access token window post-logout (accepted, documented)
5. LOW: Clock abstraction needed for refresh token expiry tests

### Conflicts Resolved
- Logout: 60-min passive expiry accepted (user: A); jti blocklist deferred
- Signing: Asymmetric RSA chosen (user: B); private key in Identity, public key distributed
- Roles: Single role per account for v1 (user: A)
- Test infrastructure: Identity.Tests as shared harness
- Ownership check location: middleware = role, handler = entity ownership

### Design Priorities
1. Identity microservice core (register/login/refresh/logout, MongoDB, bcrypt, RSA JWT)
2. Shared JWT middleware in Library (AddAgendaBuddyAuthentication + RSA public key validation)
3. Six-service auth wiring (correct pipeline order, immediate smoke tests)
4. Handler-level ownership enforcement in Booking (JWT sub vs. EmailProvider/EmailCustomer)
5. Test coverage: middleware matrix + Identity integration tests + clock abstraction

## Adversarial Review

**Completed:** 2026-07-30T00:20:00Z

### Findings
1. [SCOPE LEAK] RSA key generation is unscoped — no acceptance criterion covers key pair generation/injection; blocker for all service startup
2. [ASSUMPTION GAP] Library-as-shared-middleware assumes all six services rebuild in lockstep; monorepo reduces risk but unverified
3. [DEFINITION-OF-DONE GAP] "Provider can only access own data" is not falsifiable — exact endpoint list for sub-vs-email ownership check is undefined
4. [TECHNICAL RISK] Existing `UseAntiforgery()` in all services will reject Bearer-only auth endpoint requests — requires explicit exemption or removal in Identity service
5. [DEPENDENCY BLINDSPOT] EventAndCommands MediatR handlers bypass pipeline middleware — no auth enforcement inside CQRS kernel (deferred, documented)
6. [SUCCESS METRIC FRAGILITY] "All endpoints return 401 unauthenticated" cannot be auto-verified without an explicit endpoint inventory test per service
7. [SCOPE LEAK] Register creates credentials only — downstream services require a provider/customer profile record; F-001→F-002/F-003 handoff contract must be explicit in PRD
8. [REQUIREMENT CONFLICT] Customer service has write endpoints — role→endpoint mapping for Customer service is underspecified
9. [EDGE CASE SILENCE] Refresh token race condition (two simultaneous refreshes from same client) — needs idempotent refresh or brief reuse window
10. [TIMELINE NAIVETY] Six-service auth wiring is six separate integration tasks plus ownership changes — 2–3x larger than "surgical addition" framing suggests
11. [ASSUMPTION GAP] Identity service credentials collection naming may collide if it shares a database name with another service using the same `CollectionName` config key
12. [TECHNICAL RISK] No password complexity/minimum length specified — bcrypt hashing confirmed but 1-character passwords would be accepted

### Follow-up Q&A

**Q1:** How should the existing antiforgery middleware be handled for auth endpoints?
**A:** Disable antiforgery globally in the Identity service (pure API, no HTML forms). Keep it in other services but exempt JWT-authenticated endpoints from CSRF check — standard practice: CSRF protection is unnecessary when credentials are sent as Bearer tokens, not cookies.

**Q2:** What is the correct role→endpoint mapping for Customer service write operations?
**A:** Customer profile write endpoints (add, update) are Provider-token-only — a Provider registers/updates their own customers. A Customer token can read their own profile but cannot create or modify customer records. Aligns with the domain model: Provider manages their customer list.

**Q3:** Which specific endpoints require the sub-vs-email ownership check?
**A:** Booking (EmailProvider/EmailCustomer on appointments), Calendar (provider availability — JWT sub must match provider email), Provider profile (JWT sub must match Provider.Email for updates), Customer profile (JWT sub must match Customer.Email for updates). Services catalog and Profession are read-heavy — role check sufficient, no ownership check required.

## Edge Case Analysis

**Completed:** 2026-07-30T04:00:00Z
**Triage:** All 14 findings marked in-scope

| # | Category | Scenario | Trigger Condition | Addressed? | Risk if Unhandled | Triage |
|---|----------|----------|------------------|------------|-------------------|--------|
| 1 | Permission & access | Expired access token used mid-request | Token TTL elapses during in-flight request | Partial | In-flight mutations may partially complete then fail auth | In scope |
| 2 | Concurrency & timing | Two simultaneous POST /auth/refresh with same refresh token | Two tabs hit token expiry simultaneously | No | Second refresh gets 401; client locked out with no valid tokens | In scope |
| 3 | Concurrency & timing | Two simultaneous POST /auth/register with identical email | Race before unique index enforces 409 | Partial | One request may get 500 instead of clean 409 | In scope |
| 4 | Invalid input | POST /auth/register with malformed email format | Client sends `notanemail` | No | Invalid identity stored; email-based lookups may fail | In scope |
| 5 | Invalid input | POST /auth/register with empty or whitespace password | Client sends `""` or `"   "` | No | bcrypt of empty string is a valid hash; account secured with empty password | In scope |
| 6 | Invalid input | POST /auth/register with password below minimum length | Client sends 1–7 character password | No | Weak credentials accepted silently | In scope |
| 7 | User flow | POST /auth/logout called with already-expired or deleted refresh token | Client calls logout twice or token already expired | No | May return 500 or silently fail; idempotency undefined | In scope |
| 8 | User flow | POST /auth/refresh called after logout (revoked refresh token) | Client retries refresh after server-side deletion | Partial | Unclear whether client receives 401 or 400; cannot distinguish "logged out" from "expired" | In scope |
| 9 | Permission & access | JWT with valid signature but algorithm confusion attack | Attacker forges role claim via `alg: none` or HS256-downgrade | Partial | Privilege escalation if `alg` header not validated strictly | In scope |
| 10 | Integration failure | MongoDB unavailable when /auth/login is called | DB connection lost during login | No | Unhandled exception; may expose stack trace to caller | In scope |
| 11 | Migration & transition | Existing ProviderEntity/CustomerEntity records with no credentials document | Pre-auth data has no password | No | All pre-existing users locked out; no migration path defined | In scope |
| 12 | Scale | Credentials collection grows large; email lookup has no explicit read index | High user volume | Partial | Login latency degrades at scale | In scope |
| 13 | Partial completion | RSA key pair missing or malformed at startup | Deployment without JWT_PRIVATE_KEY / JWT_PUBLIC_KEY injected | No | Services fail to start or silently accept unsigned tokens | In scope |
| 14 | User flow | Provider changes email — JWT sub no longer matches stored email | Profile update changes email after token issued | No | Ownership checks fail until re-login; or stale JWT grants wrong-profile access | In scope |

### Follow-up Q&A

**Q1 — Refresh token race (EC-2) + email change (EC-14):**
Single-use token rotation: consuming a refresh token deletes it immediately and issues a new one. Second concurrent request finds no document → 401. No grace window.
For EC-14: existing tokens remain valid until natural 60-min expiry after an email change (consistent with passive-logout decision). Provider must re-login to get a token with the updated sub claim.

**Q2 — Pre-auth migration (EC-11) + startup validation (EC-13):**
F-001 includes a one-time migration script that seeds stub credentials for all existing ProviderEntity/CustomerEntity records (random bcrypt hash, `mustResetPassword: true` flag, no plaintext stored).
Identity service must fail fast at startup with a clear `ApplicationException` if `JWT_PRIVATE_KEY` is absent. Consumer services do the same for `JWT_PUBLIC_KEY`.

## Design Discovery (Bloom's Taxonomy)

### Round 1 — Mechanics

**Q1:** Consumer services validate JWTs locally using the RSA public key — no per-request call back to Identity. Refresh and logout still go through Identity.
**A:** Accepted.

**Q2:** Migration script derives role from entity type: ProviderEntity → Provider, CustomerEntity → Customer.
**A:** Accepted.

**Q3:** Booking ownership check: JWT sub must match EmailProvider OR EmailCustomer — either actor may update their own booking.
**A:** Accepted.

### Round 2 — Apply

**Q1:** `AddAgendaBuddyAuthentication()` lives in Library, configures JWT Bearer with RSA public key from `JWT_PUBLIC_KEY` env var, validates env var presence at startup (throws `ApplicationException` if missing). All six services call this one method — no per-service JWT config.
**A:** Accepted.

**Q2:** Identity service uses its own MongoDB database config (`IOptions<MongoDbSettings>`). `CredentialEntity` lives in `Library/Entities/`. `MongoDbRepository<CredentialEntity>` for all DB access — no raw driver calls.
**A:** Accepted.

**Q3:** Refresh token stored in MongoDB as SHA-256 hash of the opaque token sent to the client. On refresh, incoming token is hashed and looked up. Rotation: `FindOneAndDeleteAsync` + `InsertOneAsync` (old deleted, new inserted).
**A:** Accepted.

### Round 3 — Trade-offs and Judgments

**Q1:** MongoDB unavailable during login/refresh: catch `MongoException`/`TimeoutException` at service layer, return 503 with `{ "error": "service_unavailable", "message": "Authentication service temporarily unavailable." }`. No retry — client is responsible. No circuit breaker in v1.
**A:** Accepted.

**Q2:** Concurrent refresh race: `FindOneAndDeleteAsync` optimistic delete by `{ refreshTokenHash, expiry > now }`. Null result → 401 immediately. Winning request gets new token pair; losing request must re-login.
**A:** Accepted.

**Q3:** Refresh token stored as embedded sub-document in credentials document (one document per user). Supports one active session per account. Multi-session (separate collection) deferred — not in scope for v1.
**A:** Accepted.

### Synthesis

**Neo's design sketch confirmed:**
- Identity microservice owns `/auth/*`, credentials collection, bcrypt, RSA JWT issuance
- Library gains `AddAgendaBuddyAuthentication()`, `CredentialEntity`, `MongoDbRepository<CredentialEntity>`
- Six services: one `Program.cs` call + antiforgery exemption for Bearer endpoints
- Single credentials document per user with embedded refresh token sub-document
- Four Identity endpoints; no new consumer-service endpoints — auth is middleware + handler ownership checks only
- Local JWT validation, optimistic delete for refresh race, 503 on MongoDB failure, RS256 pinned, startup fail-fast

## External Context
_None ingested._

## Discovery Summary

**Completed:** 2026-07-30T04:10:00Z

### What We're Building
A new Identity microservice (ASP.NET Minimal API, MongoDB) that owns credential creation and JWT issuance, plus a shared `AddAgendaBuddyAuthentication()` extension in Library that all six existing services adopt with one line. The result: every endpoint is protected; providers and customers each see only their own data.

### Core Design (locked)
- **Endpoints:** POST /auth/register (201+JWT), /auth/login (200+JWT), /auth/refresh, /auth/logout
- **Tokens:** RSA asymmetric JWT — private key in Identity only (env var: `JWT_PRIVATE_KEY`); public key distributed to consumers (env var: `JWT_PUBLIC_KEY`). Access token 60 min; refresh token 24 hr, server-side in MongoDB, single-use rotation on refresh.
- **Roles:** Provider | Customer — one role per account. Role enforced in middleware; entity ownership (JWT sub vs. email fields) enforced in handlers (Booking, Calendar, Provider profile, Customer profile).
- **Credentials collection:** `email` (unique index), `passwordHash` (bcrypt), `role`, `refreshToken` (hashed), `refreshTokenExpiry` (TTL index), `mustResetPassword` (bool).
- **Antiforgery:** disabled globally in Identity; Bearer endpoints exempted in the other five services.

### Key Decisions
| Decision | Resolution |
|----------|-----------|
| Post-logout token window | 60-min passive expiry; no jti blocklist |
| JWT signing | Asymmetric RSA; private key Identity-only |
| Role model | Single role per account (v1) |
| Refresh race | Single-use rotation; second concurrent request → 401 |
| Email change | Existing tokens remain valid until natural expiry; re-login required for updated sub |
| Pre-auth user migration | One-time migration script seeds stub credentials + `mustResetPassword: true` for all existing records |
| Startup validation | Identity fails fast (ApplicationException) if JWT_PRIVATE_KEY missing; consumers same for JWT_PUBLIC_KEY |

### Acceptance Criteria (summary)
1. Register: 201+JWT on success, 409 on duplicate email, 400 on invalid email format, 400 on password < 8 chars or empty
2. Login: 200+JWT on success, 401 on wrong password or unknown email
3. Refresh: 200+new-access-token on valid unused refresh token, 401 on expired/missing/already-used token
4. Logout: 204 on success (idempotent — second call also 204)
5. All endpoints: 401 with no Authorization header, 401 with expired/invalid JWT, 403 on wrong role, 403 on cross-actor ownership violation
6. MongoDB unavailable: 503 with safe error body (no stack trace)
7. Missing RSA key at startup: service refuses to start, logs clear error message
8. alg header must be RS256 — `alg: none` and HS256 rejected at middleware

### Open Risks (accepted, carry to PRD)
- 60-min window post-logout where access token remains technically valid (documented, no blocklist in v1)
- No brute-force / rate-limit on /auth/login (out of scope v1, document as known gap)
- EventAndCommands CQRS kernel bypasses HTTP middleware — auth not enforced inside MediatR handlers (deferred)
- Email-as-sub means a provider email change invalidates ownership checks until re-login

### Scope Boundary
F-001 = credentials + JWT only. Full provider/customer profile creation is F-002/F-003. F-001 register creates a credentials document and returns a JWT; downstream services tolerate valid-JWT-but-no-profile state.
