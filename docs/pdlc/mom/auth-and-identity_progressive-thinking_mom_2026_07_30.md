# MOM — Progressive Thinking: auth-and-identity
**Date:** 2026-07-30
**Called by:** Atlas (Product Manager)
**Participants:** Neo, Echo, Phantom, Bolt, Friday, Muse, Pulse, Jarvis
**Feature:** F-001 auth-and-identity

---

## Discussion

### Round 1 — Concrete (Confirmed Facts)

- Six Minimal API services exist with zero `UseAuthentication()` / `UseAuthorization()` calls — all endpoints are publicly open
- Library shared project is the designated home for shared infrastructure; all services already consume it
- MongoDB is the datastore for all services; no relational DB
- Two confirmed actor roles: Provider (full CRUD own data) and Customer (limited own appointments)
- JWT direction confirmed: claims `sub` (email), `role`, `jti`; access token 60 min; refresh token 24 hr server-side in MongoDB
- bcrypt for password hashing; plaintext never stored
- Confirmed endpoints: `POST /auth/register` (201+JWT), `POST /auth/login` (200+JWT), `POST /auth/refresh`, `POST /auth/logout`
- Duplicate email on register → 409; wrong credentials → 401
- `AppointmentEntity` links actors via `EmailProvider` / `EmailCustomer` string fields
- xUnit is the test framework; all existing tests must remain green

### Round 2 — Inferential (Accepted Inferences)

- A shared `AddAgendaBuddyAuthentication()` extension method in Library will encapsulate JWT Bearer DI registration; one-line adoption per service `Program.cs`
- New `credentials` MongoDB collection in Identity service: `email` (unique index), `passwordHash`, `role`, `refreshToken` (hashed), `refreshTokenExpiry` (TTL index)
- `Microsoft.AspNetCore.Authentication.JwtBearer` NuGet reference added to Library and all six service `.csproj` files
- New `Identity.Tests` project serves as both the Identity unit/integration test suite and a shared auth test harness (base classes, JWT factory helpers) for all services
- Role enforcement belongs in middleware; entity-level ownership checks (JWT sub vs. EmailProvider/EmailCustomer) belong in handlers where entity data is available
- F-001 register creates credentials only; no full profile at this stage — F-002/F-003 complete the profile

### Round 3 — Key Consequences

- All six `Program.cs` files require surgical pipeline changes — `UseAuthentication()` / `UseAuthorization()` must appear in correct order (after routing, before endpoint mapping)
- Booking service handler logic must validate `HttpContext.User` sub against `AppointmentEntity.EmailProvider` / `EmailCustomer` — not just check the role claim
- Valid-JWT-but-no-profile state (incomplete onboarding) must be tolerated gracefully by F-002/F-003 endpoints; Identity cannot validate profile completeness
- JWT signing uses RSA asymmetric keys: private key in Identity service only; public key (PEM) distributed to all six consumer services via environment variable or config
- `JWT_SECRET` (symmetric) is replaced by `JWT_PRIVATE_KEY` / `JWT_PUBLIC_KEY` — never in `appsettings.json`, only injected at runtime

### Round 4 — Risks and Unknowns

1. **HIGH** — RSA private key leakage or hardcoding in config files; must be environment-variable injection only
2. **HIGH** — Middleware pipeline order regression across six services — wrong order = silent auth bypass; smoke test immediately after each service is wired
3. **MEDIUM** — No brute-force protection on login (out of scope, but a compliance risk to document)
4. **MEDIUM** — 60-min access token window post-logout (accepted, documented as known limitation)
5. **LOW** — Clock abstraction needed for reliable refresh token expiry tests (`DateTime.UtcNow` must be injectable or mockable)
6. **LOW** — Antiforgery (`UseAntiforgery()`) already present in all services — must verify it doesn't conflict with JWT Bearer header auth on auth endpoints

### Round 5 — Conflicts Resolved

| Conflict | Resolution |
|----------|-----------|
| Logout token invalidation | 60-min passive expiry accepted for Milestone 1 (user decision A). jti blocklist deferred as future hardening item. |
| JWT signing: symmetric vs. asymmetric | **Asymmetric RSA** chosen (user decision B). Private key in Identity only; public key distributed to consumers. Adds key management overhead but eliminates cross-service forgery risk. |
| Single vs. multi-role accounts | Single role per account for v1 (user decision A). Providers cannot also act as Customers in the same account. Deferred to a future milestone. |
| Shared test infrastructure | `Identity.Tests` created as the shared auth harness; other service test projects reference it as a project dependency. Follows existing one-test-project-per-service convention. |
| Ownership check location | Middleware enforces role; handlers enforce entity ownership (JWT sub vs. email fields on entity). Both layers required. |

### Round 6 — Design Priorities

| Rank | Priority | Risk |
|------|----------|------|
| 1 | Identity microservice core (register/login/refresh/logout, MongoDB credentials collection, bcrypt, RSA JWT issuance) | Blocker for all other features |
| 2 | Shared JWT middleware in Library (`AddAgendaBuddyAuthentication()` + RSA public key validation) | Without this, six services duplicate the implementation |
| 3 | Six-service auth wiring (correct pipeline order in all `Program.cs` files, smoke-tested immediately) | Silent bypass if order is wrong |
| 4 | Handler-level ownership enforcement in Booking service (JWT sub vs. EmailProvider/EmailCustomer) | 403 cross-provider gate |
| 5 | Test coverage: middleware matrix (401/403 full matrix per service), Identity integration tests, clock abstraction for refresh token expiry | Only automated guarantee auth is enforced |

---

## User Escalation Answers

| Question | User's Answer |
|----------|--------------|
| Should logout immediately invalidate the access token (jti blocklist) or accept 60-min passive expiry? | **A — 60-min passive expiry accepted for Milestone 1** |
| Symmetric shared secret or asymmetric RSA JWT signing? | **B — Asymmetric RSA from the start** |
| Single role per account or allow Provider + Customer simultaneously? | **A — Single role per account for v1** |

---

## Conclusion

The Progressive Thinking analysis confirms the discovery findings are sound and surfaces three critical architectural decisions (all now resolved by the user). The feature is ready to proceed to Define. Key design callouts: RSA asymmetric signing requires a key generation step before any service can run; the pipeline wiring across six services is the highest execution risk; the handler-level ownership check in Booking is the most easily missed security requirement.
