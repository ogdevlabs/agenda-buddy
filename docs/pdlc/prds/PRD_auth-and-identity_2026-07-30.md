# PRD: Auth and Identity
<!-- pdlc-template-version: 2.1.0 -->

**Date:** 2026-07-30
**Status:** Approved
**Feature slug:** auth-and-identity
**Episode:** <!-- Will be assigned after delivery -->

---

## Overview

Agenda Buddy currently exposes all six microservice API endpoints with no authentication or authorization — any caller can read, create, update, or delete any provider, customer, or appointment record. This feature adds a JWT-based identity layer: a new Identity microservice issues tokens on registration and login, and shared middleware in the Library project enforces authentication and role-based authorization across all six services. Without this layer, the platform cannot be safely exposed beyond localhost, making it a hard blocker for every subsequent feature.

---

## Problem Statement

All API endpoints are publicly accessible today. A provider can read another provider's client list; a customer can cancel any appointment. No caller proves identity before accessing or mutating data. The system cannot support onboarding flows (F-002, F-003), appointment lifecycle (F-004), or any user-facing feature until endpoints are protected. This gap also means the platform cannot be deployed to any environment reachable from the internet.

---

## Target User

Both primary personas defined in INTENT.md are directly affected:

- **Independent Service Provider (Provider):** Needs to log in, manage their own services, appointments, and customer list, and be assured that other providers cannot see or modify their data.
- **Client (Customer):** Needs to log in, view and book appointments with their subscribed provider, and be assured that no one else can cancel or modify their bookings.

All six existing microservices serve one or both personas. This feature is the prerequisite for both to use the platform safely.

---

## Requirements

1. The system MUST provide a new Identity microservice with four endpoints: `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`, and `POST /auth/logout`.
2. The system MUST store credentials as a bcrypt-hashed password alongside the user's email address and role — plaintext passwords MUST never be persisted or logged.
3. The system MUST issue RSA-signed JWTs (RS256) on successful registration and login. The private signing key MUST be injected via the `JWT_PRIVATE_KEY` environment variable only — never in `appsettings.json` or source code.
4. The system MUST distribute the RSA public key to all six consumer services via the `JWT_PUBLIC_KEY` environment variable. Consumer services MUST reject tokens signed with any algorithm other than RS256 (`alg: none` and HS256 MUST be rejected).
5. The system MUST assign exactly one role per account (Provider or Customer). Accounts MUST NOT hold multiple roles in v1.
6. The system MUST add a `AddAgendaBuddyAuthentication()` extension method to the Library shared project. All six existing services MUST adopt it with a single `program.cs` call.
7. The system MUST enforce role-based authorization in middleware (Provider vs. Customer) and entity ownership checks in handlers — specifically: Booking (JWT `sub` vs. `EmailProvider`/`EmailCustomer`), Calendar (JWT `sub` vs. provider email), Provider profile updates (JWT `sub` vs. `Provider.Email`), and Customer profile updates (JWT `sub` vs. `Customer.Email`).
8. The system MUST validate the `[EmailAddress]` format and reject registration with malformed email (400).
9. The system MUST enforce a minimum password length of 8 characters and reject empty or whitespace-only passwords (400).
10. The system MUST return 409 on registration when the email is already in use.
11. The system MUST use single-use refresh token rotation: consuming a refresh token deletes it immediately and issues a new one. A second concurrent request with the same refresh token MUST return 401.
12. `POST /auth/logout` MUST be idempotent — a second logout call with an already-expired or deleted refresh token MUST return 204, not 500.
13. The Identity service MUST fail fast at startup (throw `ApplicationException`) if `JWT_PRIVATE_KEY` is absent. Consumer services MUST do the same for `JWT_PUBLIC_KEY`.
14. The system MUST return 503 with a safe error body (no stack trace, no PII) when MongoDB is unavailable during an auth request.
15. The system MUST include a one-time migration script that seeds stub credentials for all existing ProviderEntity and CustomerEntity records, setting `mustResetPassword: true` and a random bcrypt hash. No plaintext password is generated or stored.
16. The Identity service MUST disable antiforgery globally. The other five services MUST exempt JWT Bearer endpoints from the antiforgery check.
17. Customer profile write endpoints (add, update) MUST require a Provider token. A Customer token MUST be able to read its own profile but MUST NOT create or modify customer records.
18. After a provider changes their email in their profile, their existing access token remains valid until its natural 60-minute expiry; the provider MUST re-login to obtain a token with the updated `sub` claim.

---

## Assumptions

- The RSA key pair will be generated externally (e.g., `openssl genrsa`) and injected via environment variables before any service starts. Key generation is not part of this feature's implementation but MUST be documented in the deployment runbook.
- All six services share the same monorepo and are rebuilt together — the Library-as-shared-middleware approach is safe because there is no partial-deploy scenario in the current Docker Compose setup.
- The existing MongoDB collections for Provider and Customer contain only development/seed records. The migration script may reset credential state for those records without concern for production data loss.
- Email is the stable unique identifier for all actors. Profile email changes are rare and the 60-minute stale-token window is an acceptable trade-off for v1 (consistent with the passive-logout decision).
- `Microsoft.AspNetCore.Authentication.JwtBearer` is not yet referenced in any `.csproj` file and must be added to Library and all six service projects.
- The EventAndCommands CQRS kernel bypasses HTTP middleware — auth is NOT enforced inside MediatR handlers in this version. This is a known limitation deferred to a future hardening milestone.

---

## Acceptance Criteria

1. `POST /auth/register` with a valid email and password ≥ 8 characters returns 201 and a JWT access token.
2. `POST /auth/register` with a duplicate email returns 409.
3. `POST /auth/register` with a malformed email (e.g., `notanemail`) returns 400.
4. `POST /auth/register` with an empty, whitespace-only, or < 8 character password returns 400.
5. `POST /auth/login` with valid credentials returns 200 and a JWT access token plus a refresh token.
6. `POST /auth/login` with an unknown email or wrong password returns 401.
7. `POST /auth/refresh` with a valid, unused refresh token returns 200 and a new access token; the old refresh token is deleted.
8. `POST /auth/refresh` called twice concurrently with the same refresh token: one call returns 200, the other returns 401.
9. `POST /auth/logout` with a valid refresh token returns 204 and deletes the token.
10. `POST /auth/logout` called a second time (token already deleted or expired) returns 204, not 500.
11. Any protected endpoint called with no Authorization header returns 401.
12. Any protected endpoint called with an expired or invalid JWT returns 401.
13. A JWT with `alg: none` or `alg: HS256` is rejected with 401.
14. A Provider endpoint called with a Customer token returns 403.
15. A Provider endpoint called with another provider's valid token returns 403 (ownership check).
16. A Customer endpoint called with a Provider token returns 403 (where role check applies).
17. Customer profile write endpoint called with a Customer token returns 403.
18. Booking mutation endpoint: JWT `sub` must match `EmailProvider` or `EmailCustomer` on the appointment; mismatch returns 403.
19. MongoDB unavailable during `/auth/login`: response is 503 with a safe error body containing no stack trace and no PII.
20. Identity service fails to start and logs a clear error if `JWT_PRIVATE_KEY` environment variable is absent.
21. Consumer service fails to start and logs a clear error if `JWT_PUBLIC_KEY` environment variable is absent.
22. Migration script runs without error against a database containing existing ProviderEntity and CustomerEntity records; each gains a credentials document with `mustResetPassword: true` and a non-empty bcrypt hash; no plaintext password is present anywhere.
23. All existing unit and integration tests across all six services pass without modification after auth is wired.
24. Password is never written to application logs, error messages, or MongoDB documents in any form other than a bcrypt hash.

---

## User Stories

**US-001: Provider registration**
*Acceptance criteria: 1, 2, 3, 4*
Given an unregistered user who wants to act as a Provider
When they POST `/auth/register` with a valid email, a password of at least 8 characters, and role `Provider`
Then the system returns 201 with a JWT access token and a refresh token
And a credentials document is created in MongoDB with a bcrypt-hashed password and role `Provider`
And the plaintext password is never stored or logged

**US-002: Customer registration**
*Acceptance criteria: 1, 2, 3, 4*
Given an unregistered user who wants to act as a Customer
When they POST `/auth/register` with a valid email, a password of at least 8 characters, and role `Customer`
Then the system returns 201 with a JWT access token and a refresh token
And a credentials document is created in MongoDB with a bcrypt-hashed password and role `Customer`

**US-003: Login and token issuance**
*Acceptance criteria: 5, 6*
Given a registered user (Provider or Customer)
When they POST `/auth/login` with their correct email and password
Then the system returns 200 with a JWT access token (60-min TTL) and a refresh token (24-hr TTL stored server-side)
When they POST `/auth/login` with the wrong password or an unknown email
Then the system returns 401

**US-004: Token refresh**
*Acceptance criteria: 7, 8*
Given a user holding a valid refresh token
When they POST `/auth/refresh` with that token
Then the system returns 200 with a new access token and deletes the old refresh token (single-use rotation)
When two concurrent requests arrive with the same refresh token
Then one returns 200 and the other returns 401

**US-005: Logout**
*Acceptance criteria: 9, 10*
Given a logged-in user
When they POST `/auth/logout` with their refresh token
Then the system returns 204 and the refresh token is deleted from MongoDB
When they POST `/auth/logout` again (token already gone)
Then the system returns 204 (idempotent — no 500)

**US-006: Endpoint protection — unauthenticated and invalid tokens**
*Acceptance criteria: 11, 12, 13*
Given any protected endpoint across the six services
When a request arrives with no Authorization header, an expired JWT, or a JWT using `alg: none` or HS256
Then the system returns 401

**US-007: Role-based authorization**
*Acceptance criteria: 14, 15, 16, 17*
Given a valid JWT
When a Customer token is used on a Provider-only endpoint
Then the system returns 403
When a Provider token is used on a Customer-only endpoint
Then the system returns 403
When a Provider token is used on another provider's resource (cross-provider ownership check)
Then the system returns 403
When a Customer token is used on a customer-profile write endpoint
Then the system returns 403

**US-008: Ownership enforcement in Booking**
*Acceptance criteria: 18*
Given a valid Provider JWT
When the provider attempts to modify a Booking whose `EmailProvider` does not match their JWT `sub`
Then the system returns 403

**US-009: Operational resilience and security**
*Acceptance criteria: 19, 20, 21, 24*
Given the Identity service is starting up
When `JWT_PRIVATE_KEY` is not set
Then the service refuses to start and logs a clear error message
Given MongoDB becomes unavailable
When a login request arrives
Then the system returns 503 with a safe error body containing no stack trace and no PII

**US-010: Pre-existing user migration**
*Acceptance criteria: 22, 23*
Given a database containing existing ProviderEntity and CustomerEntity records with no credentials document
When the migration script is run
Then each record gains a credentials document with `mustResetPassword: true` and a bcrypt hash
And no plaintext password is generated or stored at any point

---

## Non-Functional Requirements

- **Security:** RSA private key (`JWT_PRIVATE_KEY`) MUST be injected via environment variable only — never in `appsettings.json`, Dockerfile, or any checked-in file. Violation is a hard build block.
- **Security:** PII (email, names) MUST NOT appear in application logs. Password MUST never appear in logs in any form.
- **Security:** JWT validation MUST pin the algorithm to RS256. `alg: none` and HS256 MUST be explicitly rejected — not merely unhandled.
- **Security:** bcrypt cost factor MUST be ≥ 12 to resist offline brute-force attacks.
- **Performance:** Login and registration endpoints MUST respond within 500ms at p95 under normal load (bcrypt is the expected bottleneck; cost factor 12 is the accepted trade-off).
- **Reliability:** Auth endpoints MUST return structured error responses (no raw exception stack traces) for all 4xx and 5xx conditions.
- **Testability:** `DateTime.UtcNow` in refresh token expiry logic MUST be abstracted (injectable clock) so tests can control token expiry without real time passing.
- **Compatibility:** All six existing service test suites MUST remain green after auth middleware is added. Auth is additive — no existing handler logic may be broken.
- **Observability:** Service startup MUST emit a clear, human-readable log line confirming RSA key loaded successfully (key fingerprint only — never the key material).

---

## Known Risks

- **60-minute post-logout access token window:** After `POST /auth/logout`, the access token remains cryptographically valid for up to 60 minutes. A jti blocklist would close this window but adds a per-request Redis/MongoDB lookup on every authenticated request. Deferred to a future hardening milestone. Risk level: MEDIUM. Documented as known limitation.
- **No brute-force protection on `/auth/login`:** Rate limiting and account lockout are not in scope for v1. An attacker can attempt unlimited password guesses. Risk level: MEDIUM. Deferred — document as known gap; address in a future security-hardening feature.
- **EventAndCommands CQRS kernel bypasses HTTP middleware:** MediatR command/query handlers are invoked inside the HTTP pipeline but after middleware. Internal handler calls that bypass the HTTP stack would not be subject to JWT validation. This is a theoretical risk in the current architecture (no internal calls exist today); deferred with documentation. Risk level: LOW.
- **Email-as-sub identity coupling:** JWT `sub` equals the user's email. If a provider changes their email in their profile (F-002), existing tokens carry the old `sub` until natural expiry (up to 60 min). Ownership checks will fail for that window unless the provider re-logs in. Accepted trade-off for v1. Risk level: LOW.

---

## Out of Scope

- **Full provider/customer profile creation:** F-001 creates credentials only (email + password + role). Profile data (profession, services, bio, etc.) is F-002 (provider) and F-003 (customer). The Identity service returns a valid JWT on register; downstream services MUST tolerate a valid-JWT-but-no-profile state.
- **Multi-role accounts:** A single account cannot hold both Provider and Customer roles in v1. Deferred to a future milestone.
- **jti blocklist / immediate access token revocation:** The 60-minute passive expiry window is accepted for v1. A blocklist requires per-request DB or cache lookup and adds latency to every authenticated call.
- **Brute-force protection / rate limiting on auth endpoints:** Not in scope for v1. Will be addressed in a dedicated security-hardening feature.
- **OAuth / SSO / external identity providers:** Only email + password credentials in v1.
- **Password reset flow:** Out of scope for v1. `mustResetPassword` flag is set by the migration script but the reset endpoint is a separate feature.
- **Auth enforcement inside the EventAndCommands CQRS kernel:** MediatR handlers are not auth-gated in this version.
- **Mobile client support:** API only; no device-specific token management, push notification tokens, or mobile OAuth flows.

---

## Design Docs

- Architecture: [ARCHITECTURE.md](../design/auth-and-identity/ARCHITECTURE.md)
- Data model: [data-model.md](../design/auth-and-identity/data-model.md)
- API contracts: [api-contracts.md](../design/auth-and-identity/api-contracts.md)
- Threat model: [threat-model.md](../design/auth-and-identity/threat-model.md) *(triage: Full — 8 threats, 5 mitigate now, 3 accept/mitigate later)*
- UX review: [ux-review.md](../design/auth-and-identity/ux-review.md) *(triage: Skip — pure backend API, no UI surface)*

---

## Related Episodes

<!-- None yet. -->

---

## Approval

**Approved by:** ogdevlabs
**Date approved:** 2026-07-30
**Notes:**
