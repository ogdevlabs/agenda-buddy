# 13 — Security

> **⚠️ F-021 delta (2026-08-22, branch `feat/F-021-identity-hardening`, not yet merged).**
>
> **✅ CLOSED by F-021, with the test that reaches each one named in
> `docs/pdlc/design/identity-hardening/verification.md`:**
> - **`RefreshAsync`'s delete-then-insert is gone.** Rotation is one `FindOneAndUpdateAsync` whose filter
>   carries the presented hash, the expiry check and a "not locked" condition; the credential is never
>   deleted, so no fault can destroy it. The fault case is now an actual test — `InMemoryRepository` gained
>   a hook that fires between matching and writing, which is what `11-testing.md:65` said was impossible.
> - **`UseHttpsRedirection` runs before `UseAuthentication` in all seven services**, via one shared
>   `UseAgendaBuddyTransportSecurity()`. Identity's `if (!IsDevelopment())` guard is removed — under the
>   AppHost that condition was always true. A source-level test asserts the order in all seven and bans
>   direct `UseHttpsRedirection` calls.
> - **HSTS exists** (`Security:Hsts:Enabled`, conservative `max-age`, no `includeSubDomains`, no `preload`).
> - **Rate limiting and lockout exist**: a per-IP sliding window on `login` **and** `register` — both spend
>   262 ms of BCrypt, measured — plus a per-account counter and a **self-clearing** 15-minute lock.
> - **Identity has a log sink**, and credential mutations are recorded with `acct_<12 hex>` rather than an
>   address, because `PiiRedactingProcessor` redacts spans and not logs.
>
> **⚠️ New or still open after F-021:**
> - Both controls are **gated on configuration and default off** (ADR-033) — services run as *Production*
>   locally, so environment cannot carry the distinction. A deployment that never sets the keys ships
>   without them (threat T-103); mitigated by a startup warning naming the key, by the cloud graph in
>   `AppHostWiring.cs` setting them, and by the harness switching them on so neither ships unexercised.
> - **The limiter is per-process** (T-106, accepted) and collapses to one bucket behind a proxy that does
>   not forward the client address (`agenda-buddy-end`, F-017).
> - **A locked account answers faster than a wrong password** (T-NL-2, accepted): hiding that oracle costs
>   262 ms per attempt and re-arms the CPU-exhaustion threat.
> - **HSTS is inert until TLS is terminated somewhere** — F-017.
> - **`credentials` has no unique index on `email`** (`agenda-buddy-b0w`), confirmed not fixed.
>
> **⚠️ F-016 delta (2026-08-18, `v0.2.0`) — refreshed 2026-08-22 at the ship gate.**
>
> **✅ CLOSED by F-016, and demonstrated live rather than by inspection:** the anonymous
> `GET /api/v1/providers` full-record exposure and the four other anonymous PII GETs (all now **401**);
> Calendar's authenticated-but-unguarded **IDOR** (both routes call `OwnershipGuard` **before** the cache
> read); the unpaginated full-dataset dump (page size clamped and capped — a security control, not
> ergonomics); `ForbiddenException` → **403** centrally, where a forgotten `try/catch` previously returned
> **500**; `AssertRole` actually wired on provider creation; `POST /api/v1/professions` deleted (ADR-025);
> and read queries no longer serialising full PII into the `events` audit collection.
>
> **⚠️ Still open after F-016:**
> - **The Atlas credential is unrotated** — out of the working tree, still in git history, still valid.
>   Removal is not rotation. **Corrected 2026-08-18:** the cluster holds **synthetic/development data only**,
>   so this is dev-data destruction and resource abuse, not a personal-data breach — re-graded MEDIUM.
>   Human-only: `docs/issues/ISSUE-002-atlas-credential-rotation.md` (`agenda-buddy-41s`).
> - **`GET /api/v1/customers` returns the full `CustomerEntity`** to any Provider-role caller. Owner-scoping
>   deferred by ADR-026; quantified as review finding I-2.
> - **Authorization failures are entirely unlogged** — no log sink at all, so IDOR probing leaves no trace
>   (advisory A-1). Belongs to F-021/F-024.
> - ~~**The whole of F-021**~~ — **all three items closed on the F-021 branch; see the delta above.** The
>   fourth item once listed here (`AssertOwner`'s null-claim pass) was closed earlier, by F-016-T09.
> - **§7's security-scan gate is still manual** — satisfied by hand for the second consecutive release. F-017.
>
> **Still true from F-013:** JWT keys are Aspire `secret: true` parameters and only Identity receives the
> private key; spans are PII-redacted by `PiiRedactingProcessor.cs` because `url.path` was exporting real
> customer emails. **Do not remove it.**
>
> Sources: `docs/pdlc/episodes/EPISODE_secure-public-endpoints_2026-08-18.md`,
> `docs/pdlc/reviews/REVIEW_secure-public-endpoints_2026-08-18.md`, ADR-022…031.


**Files:** `Library.ServerAuth/AuthenticationExtensions.cs`, `Library.ServerAuth/Tools/OwnershipGuard.cs`, `Identity/Services/IdentityService.cs`, `Library/Entities/CredentialEntity.cs`, all 7 `Program.cs` pipelines, all `appsettings*.json`, `docker-compose.override.yml`, `MobileApp/Infrastructure/*`.

**Reference:** `CONSTITUTION.md` §4 is the applicable standard. Threat IDs (`T-001`, `T-002`, `T-005`) referenced in code comments come from the F-001 `auth-and-identity` threat model.

---

## ⚠️ P0 — A live database credential is committed to git

```
mongodb+srv://<user>:<REDACTED-ROTATE-THIS>@<cluster>.mongodb.net/...
```

Present in **14 tracked files**: 6 × `appsettings.json` + 6 × `appsettings.Development.json` (domain services), `Identity/appsettings.json:22`, `EventAndCommands/appsettings.json:11`, plus `docker-compose.override.yml:114` and two commented blocks (`:88`, `:104`). Full line-by-line table in `06-configuration.md`.

- **Violates** `CONSTITUTION.md` §4: *"Secrets must never appear in source code — use `appsettings.json` / User Secrets / environment variables."*
- **The credential is in git history**, so deleting the files is insufficient — the Atlas database user must be rotated and the cluster's access log reviewed.
- **Grants full read/write** to `agenda_buddy` and `IdentityDb`, i.e. every provider, customer, appointment, private session note, payment record, and password hash.
- **Would have been caught by a secret scanner.** `CONSTITUTION.md` §7 marks "Security scan (dependency audit + secret scan)" as *always required, cannot be unchecked* — and CI implements neither (`08-cicd-deploy.md`). This is an unmet mandatory gate whose absence directly enabled the P0.

The contrast is instructive: the **JWT keys are handled correctly** in the same file — sourced from a gitignored `.env` (`docker-compose.override.yml:136-138`) with the commented service blocks carrying `# JWT_PUBLIC_KEY must be injected at deploy time — never in source` (`:89`, `:105`). The discipline existed and was not applied to the database credential.

⚠️ Secondary: `DevelopmentSeedData.DefaultPassword = "DevPass123!"` is a committed literal in the shipped `Library` assembly (`Library/Data/DevelopmentSeedData.cs:151`), echoed by `scripts/seed/seed-mongo.sh:46`. Lower severity (dev accounts, `@agendabuddy.dev` emails) but it ships in a production artifact.

---

## Authentication posture

### Token design — sound

`Identity/Services/IdentityService.cs:186-219` mints an **RS256-signed JWT**:

| Property | Value | Anchor |
|---|---|---|
| Algorithm | `RsaSha256` (asymmetric) | `:198` |
| Issuer | `"agenda-buddy-identity"` | `:19`, `:208` |
| Access-token lifetime | **60 minutes** | `:210` |
| Claims | `sub` = email, `role`, `jti` (fresh GUID) | `:200-205` |
| Refresh token | 32 random bytes from `RandomNumberGenerator`, base64 | `:215` |
| Refresh lifetime | **24 hours** | `:63`, `:108`, `:150` |
| Refresh storage | **SHA-256 hex hash only** — raw token never persisted | `:216`, `:221-226`; `CredentialEntity.cs:41-42` |

Asymmetric signing is the right choice: only Identity holds `JWT_PRIVATE_KEY`, while the six resource services need only `JWT_PUBLIC_KEY` — so a compromised resource service cannot mint tokens.

### Validation — strict

`Library.ServerAuth/AuthenticationExtensions.cs:36-46`:

```csharp
ValidateIssuer = true,  ValidIssuer = Issuer,     // :38-39
ValidateAudience = false,                         // :40  ⚠️
ValidateLifetime = true,                          // :41
ClockSkew = TimeSpan.Zero,                        // :42  ✅ no grace period
ValidateIssuerSigningKey = true,                  // :43
IssuerSigningKey = rsaKey,                        // :44
ValidAlgorithms = ["RS256"],                      // :45  ✅ algorithm confusion blocked
```

`ValidAlgorithms = ["RS256"]` (`:45`) is the important one — it blocks the `alg: none` and HS256-with-public-key confusion attacks. `ClockSkew = Zero` (`:42`) eliminates the default 5-minute grace window. Covered by `Identity.Tests/Auth/JwtMiddlewareMatrixTest.cs` (10 tests).

**Fail-fast on missing key:** `:18-21` throws `ApplicationException` if `JWT_PUBLIC_KEY` is unset, with an actionable message. All seven services call `AddAgendaBuddyAuthentication()`, so none can start without the key.

⚠️ **`ValidateAudience = false`** (`:40`) and no `aud` claim is issued. All seven services accept any token this issuer minted, so a token intended for one service is valid at all of them. Acceptable for a single trust domain, but it removes audience-scoping as a defence and means a leaked token has the widest possible blast radius.

⚠️ **`JWT_PRIVATE_KEY` is checked lazily** — read inside `GenerateTokenPair` (`IdentityService.cs:189`), not at startup. Identity **starts successfully** without it and returns 500 on the first register/login. `JWT_PUBLIC_KEY` is checked eagerly; the asymmetry means a misconfigured Identity deploy looks healthy until first use (`12-observability.md`).

⚠️ **No `kid` header and no key rotation path.** A single `RsaSecurityKey` is loaded once at startup (`:25-27`). Rotating keys requires a coordinated restart of all seven services; there is no JWKS endpoint and no multi-key validation.

⚠️ **No token revocation.** `jti` is issued (`:204`) but never recorded or checked. Logout clears the refresh token (`IdentityService.LogoutAsync:176`) but the **access token stays valid for up to 60 minutes after logout**. There is no denylist.

### ~~⚠️ `RefreshAsync` — delete-then-insert data-loss window~~ — CLOSED by F-021-T02

> **Kept in full, not deleted.** This is the highest-severity defect the project has fixed, and the
> shape of it — an atomic operation that was correct about *concurrency* and wrong about *granularity*,
> made survivable by 20 passing tests that could not express the failure — is worth being able to
> recognise again. What it looks like now is below the original.

**As it was:** `Identity/Services/IdentityService.cs:123-163`:

```csharp
credential = await repository.FindOneAndDeleteAsync(filter);   // :135  ⚠️ removes the account document
if (credential is null) throw new UnauthorizedException(...);  // :142-143
...
await repository.InsertAsync(credential);                      // :155  ⚠️ puts it back
```

The atomic `FindOneAndDeleteAsync` on `{ refresh_token.hash, refresh_token.expiry: {$gt: now} }` (`:130-135`) is a **correct** single-use-token guard — it prevents refresh-token replay races. But it deletes the **entire `CredentialEntity`**, not just the embedded refresh-token sub-document, and then re-inserts it 20 lines later.

**Failure scenarios:**
1. Any exception or process termination between `:135` and `:155` — including the `IsMongoDown` catch at `:157-160` firing on the insert — **permanently destroys the user's account**: email, password hash, and role. The user cannot log in and cannot recover; there is no audit trail (Identity does not use the EventStore) and no logging (`12-observability.md`).
2. Between `:135` and `:155` a concurrent `LoginAsync` for the same user finds no credential (`:93`) and returns `401` on valid credentials.

The correct operation is a targeted `$set`/`$unset` on `refresh_token` (or `FindOneAndUpdate`), never a whole-document delete. `IRepository<T>` offers no partial-update primitive (`04-data-access.md`), which is the underlying cause.

**As it is now (F-021-T02).** One round trip, one document, no delete:

```csharp
var filter = new BsonDocument
{
    { "refresh_token.hash", presentedHash },                       // single use: the OLD hash is the match
    { "refresh_token.expiry", new BsonDocument("$gt", now) },       // expiry, as before
    { "$or", NotLocked(now) }                                      // AC-4 / T-104, at no extra query cost
};
credential = await repository.FindOneAndUpdateAsync(filter, /* $set refresh_token */);
if (credential is null) throw new UnauthorizedException(...);      // unknown, expired, used, or locked
```

Both failure scenarios above are gone, and neither by accident:

1. There is no window in which the credential does not exist, because it is never removed. Asserted under
   an **injected fault** — `InMemoryRepository.FaultBetweenMatchAndWrite` fires between matching and
   writing, which is precisely the capability `11-testing.md:65` said was missing.
2. A concurrent `LoginAsync` can no longer find nothing, for the same reason.

Single use survives the change because the old hash moved into the **filter**: the update matches only
while that hash is still stored, so a replay matches no document. Verified against real MongoDB, not only
against the in-memory double (`CredentialUpdatePrimitiveTest`).

⚠️ **One thing to know if you read the code:** the signing key is read *before* the write, because minting
the access token needs the email and role that only the matched document can supply — but it is read
**without throwing**, and the throw sits behind the match. Reading it strictly turned every *rejected*
refresh into a 500, which the integration harness caught and no unit test could (they all set
`JWT_PRIVATE_KEY` in their constructor).

⚠️ This is very unlikely to be covered by the 21 `IdentityServiceTest` cases — `InMemoryRepository` cannot simulate a mid-operation fault (`11-testing.md`).

### Password handling — sound

- **BCrypt**, work factor **12** (`IdentityService.cs:50`, `SeedAuthCredentials.cs:60`, `SeedDevelopmentAccounts.cs:80`). Appropriate.
- **T-005 user-enumeration mitigation** (`:95-96`): on an unknown email, a dummy BCrypt verify runs against a precomputed `DummyHash` (`:22-23`) before throwing, equalising response time.
- Minimum length 8, enforced at both the route (`Identity/Program.cs:103-104`) and the service (`IdentityService.cs:29-30`).
- Emails normalised to lower case on register and login (`:27`, `:81`).

⚠️ **Password policy is length-only** — no complexity requirement, no breached-password check, no maximum length. BCrypt silently truncates inputs beyond 72 bytes, so a passphrase longer than that has its tail ignored with no warning.

~~⚠️ **No account lockout and no rate limiting.**~~ **CLOSED by F-021** (`Identity/Extensions/RateLimitingExtensions.cs`, `IdentityService.CountFailedAttemptAsync`). Per-IP sliding window on `login` **and** `register`, off by default and switched on by configuration; per-account counter with a self-clearing 15-minute lock. Note what the measurement changed about the *reason*: at **262 ms per BCrypt verify** an attacker gets 3.8 guesses/sec/core, so the dominant threat was never guessing — it was the attacker spending **the server's** CPU (T-101). The T-005 timing mitigation still stands, and is why the per-account counter alone would be blind: an attacker using random addresses generates no per-account state.

⚠️ **`MustResetPassword` is written but never read.** `CredentialEntity.cs:29` and `SeedAuthCredentials.cs:68` set it, but `LoginAsync` (`:79-121`) never inspects it — so the forced-reset flow the field exists for does not exist. And there is **no password-reset or change-password endpoint at all** (`01-api-surface.md`), so a user who forgets their password has no recovery path.

⚠️ **`SeedAuthCredentials` is dead code** (`05-data-model.md`) — the F-001 migration that would backfill credentials for pre-auth providers and customers is never invoked, so any such records cannot authenticate.

⚠️ **Single refresh token per account** (`CredentialEntity.RefreshToken` is one embedded document, not an array, `:33`). A second device login overwrites the first, silently signing the first device out.

⚠️ **The documented TTL index does not exist.** `CredentialEntity.cs:44-45` says *"UTC expiry timestamp. TTL index on this field in MongoDB"* — no TTL index is created anywhere in the repo (`seed-mongo.sh:39` creates only the unique email index). Expired refresh-token hashes accumulate indefinitely. `[verify against live Atlas cluster]`.

---

## Authorization posture

### `OwnershipGuard` — the IDOR defence

`Library.ServerAuth/Tools/OwnershipGuard.cs`, three static methods:

| Method | Line | Check |
|---|---|---|
| `AssertOwner(user, entityEmail)` | `:7` | `ClaimTypes.NameIdentifier` equals `entityEmail`, `OrdinalIgnoreCase` |
| `AssertOwnerAny(user, params entityEmails)` | `:14` | claim matches any of the supplied emails; null claim → throw |
| `AssertRole(user, requiredRole)` | `:21` | `user.IsInRole(requiredRole)` |

Case-insensitive comparison (`:10`, `:17`) is correct given `IdentityService` lower-cases emails. Well covered: 13 tests in `Identity.Tests/Auth/OwnershipGuardIdorTest.cs` + 11 in `Library.Tests/Tools/OwnershipGuardTest.cs`.

⚠️ **`AssertOwner` does not guard against a null claim.** `:9-10` — if `FindFirstValue(NameIdentifier)` returns `null` and `entityEmail` is also null, `string.Equals(null, null)` is `true` and the guard **passes**. `AssertOwnerAny` explicitly handles this (`:17` checks `sub is null` first); `AssertOwner` does not. Reachable only with a null route value, which ASP.NET generally prevents — but the asymmetry between the two methods is a latent hole.

⚠️ **`AssertRole` is never called.** No role-based authorization anywhere in the solution. Consequences: any authenticated **Customer** can `POST /api/v1/providers` to create a provider record for an arbitrary email (`Provider/Program.cs:100-129` — no `OwnershipGuard`, no role check), and any Customer can `POST /api/v1/professions` to write to the global reference catalogue (`Profession/Program.cs:93-121`). The `role` claim is minted and validated but never authorizes anything.

⚠️ **`ForbiddenException.StatusCode => 403` (`:30`) is never read.** Correct 403s depend on each endpoint hand-writing `try { OwnershipGuard… } catch (ForbiddenException) { return TypedResults.Forbid(); }` — repeated at 8 call sites. A new guarded endpoint that omits the `try/catch` returns **500 instead of 403**, with no compile-time protection (`10-error-handling.md`).

### ⚠️ Unauthenticated endpoints exposing PII

This is the second-most-serious finding after the credential. Full route table in `01-api-surface.md`.

| Route | Anchor | Exposes |
|---|---|---|
| `GET /api/v1/providers` | `Provider/Program.cs:132-147` | **Every provider's full record** — and `ProviderEntity` embeds `AppointmentEntities` (with `email_customer` on each) and `SubscribedCustomerCollection` (`ProviderEntity.cs:40-42`). An anonymous caller retrieves every provider's entire appointment book and client list. |
| `GET /api/v1/customers` | `Customer/Program.cs:146-158` | **Every customer record** — names + email addresses. |
| `GET /api/v1/providers/{email}` | `Provider/Program.cs:150-167` | One provider's full nested graph. |
| `GET /api/v1/customers/{email}` | `Customer/Program.cs:160-172` | One customer record; also an **email-enumeration oracle** (200 vs 404). |
| `GET /api/v1/services/{email}` | `Services/Program.cs:94-111` | A provider's service catalogue and fees. |
| `GET /api/v1/professions*` | `Profession/Program.cs:123,136` | Reference data — defensible. |

`CONSTITUTION.md` §4: *"PII (email addresses) is stored in MongoDB — ensure access controls are in place."* For these routes there are none. Both list endpoints are also **unpaginated**, so a single request dumps the whole dataset.

⚠️ **`GET /api/v1/calendar/{availability,appointments}/{email}` is authenticated but not ownership-guarded** (`Calendar/Program.cs:93-141`). `RequireAuthorization()` proves the caller holds a valid token, not that `{email}` is theirs — so **any registered user can read any provider's full appointment list**, including customer emails. A classic IDOR, and the only guarded-service route family that omits `OwnershipGuard` while its siblings apply it (`Provider/Program.cs:182`, `Customer/Program.cs:133`, `Services/Program.cs:122,146`). The `OwnershipGuardIdorTest` suite tests the guard, not the endpoints that forgot it — there are no integration tests (`11-testing.md`).

---

## Transport and network boundary

| Control | Status |
|---|---|
| `UseHttpsRedirection` | ✅ **F-021:** called in all seven via `UseAgendaBuddyTransportSecurity()`, **before** `UseAuthentication`. Identity's Development guard removed. Still a no-op where no HTTPS port is configured |
| HTTPS endpoint configured | ⚠️ **No.** `appsettings.json` declares only `Http` (HTTP/1) and `gRPC` (h2c). Only `launchSettings.json` supplies an HTTPS URL (`Booking/Properties/launchSettings.json:27` → `:8033`) |
| HSTS | ✅ **F-021:** `AgendaBuddy.ServiceDefaults/TransportSecurity.cs`, gated on `Security:Hsts:Enabled` (default off). No `includeSubDomains`, no `preload` — both are the hard-to-reverse parts. Inert until F-017 terminates TLS |
| Antiforgery | ✅ `AddAntiforgery`/`UseAntiforgery` in 6 services; Identity deliberately excluded (API-only, `:87`) |
| CORS | ❌ No policy registered in any service |
| Rate limiting | ❌ Absent |
| `AllowedHosts` | ⚠️ Set in Customer, Services, Profession, Identity; **omitted** in Booking, Calendar, Provider |

~~⚠️ **`UseHttpsRedirection()` is registered *after* `UseAuthentication()`/`UseAuthorization()`**~~ **CLOSED by F-021**, in all seven services. Worth keeping the correction the fix carried: reordering does **not** fix what it appears to fix — by the time any middleware runs, the password or bearer token has already crossed the wire. Redirection protects nothing already sent; HSTS is what stops the client repeating the mistake, which is why F-021 added both.

⚠️ **No HTTPS listener exists**, so `UseHttpsRedirection` has nothing to redirect to outside the `launchSettings` `https` profile. Combined with `EXPOSE 8080/8081` in the Dockerfiles versus `localhost:60xx` in `appsettings.json` (`08-cicd-deploy.md`), the deployed transport posture is plain HTTP.

⚠️ **`ServicePointManager.SecurityProtocol = Tls12 | Tls13`** at the top of 5 `Program.cs` files and `ConfigurationLoader.cs:7` is **inert on .NET Core** — it does not affect `HttpClient` or the MongoDB driver's TLS negotiation. `SYSLIB0014` is suppressed solution-wide to hide the obsolescence warning (`Directory.Build.props:16`). It reads as a TLS control and is not one.

⚠️ **Client-side:** `MobileApp/MauiProgram.cs:32,38` falls back to **`http://localhost:6036/`** — plaintext HTTP. *(Sharpened 2026-08-22:* this is not merely a fallback — **nothing in `MobileApp` ever loads `appsettings.json`**: no `AddJsonFile`/`AddJsonStream` call exists and the file is not an embedded resource, so `ApiBaseUrl` is *always* null and the plaintext URL is *always* what ships. `appsettings.json:2`'s `https://localhost` is dead text.*)* `Platforms/iOS/Info.plist` carries an ATS exception allowing insecure loads to `localhost`. No certificate pinning.

---

## Mobile client security

**Good:**
- JWT stored via `ISecureStorageService` → `MauiSecureStorageService`, which wraps MAUI `SecureStorage` (Keychain / Android Keystore). Not in preferences or plaintext.
- `JwtDelegatingHandler` (`MobileApp/Infrastructure/JwtDelegatingHandler.cs:20-22`) attaches the bearer token per request, and on a `401` **purges the stored token** (`:28`) and raises `UnauthorizedAccess` so the Shell routes back to login (`AppShell.xaml.cs:20-21`). Sound reactive-logout.
- The login/register client `"AgendaBuddyApiNoAuth"` deliberately omits the JWT handler (`MauiProgram.cs:35-39`).
- **T-002 mitigation honoured:** no PII in push payloads (`09-integrations.md`); `STATE.md` records "Push payload body is PII-free generic text".

⚠️ **The refresh token is stored and never used.** `AuthService` persists it (`:44`, `:70`) and clears it on logout (`:81`), but **nothing ever calls `POST api/v1/auth/refresh`** — grep confirms no such call in `MobileApp`. So the 60-minute access-token expiry becomes a hard logout: at minute 61 the next request 401s, `JwtDelegatingHandler` wipes the token, and the user is bounced to login despite holding a valid 24-hour refresh token. A functional defect with a security-adjacent cause (the refresh mechanism exists on both ends and is unwired in the middle).

⚠️ **`JwtDelegatingHandler.UnauthorizedAccess` is a `static` event** (`:35`) subscribed in the `AppShell` constructor (`AppShell.xaml.cs:20`) with no unsubscribe. Handlers accumulate across `AppShell` re-creations; in the test host they leak between cases.

⚠️ **`UserSessionService` decodes the JWT without verifying it** (`MobileApp/Services/UserSessionService.cs:41-67`) — splits on `.`, base64url-decodes the payload, and reads `sub`/`role`. Acceptable for a client deriving display state (the server is authoritative), but `IsProvider`/`IsCustomer` (`:23-24`) drive UI affordances from an unverified claim, so a tampered local token changes the visible UI. Not a server-side authorization bypass.

⚠️ **`MobileApp` references `Library`** (`MobileApp.csproj:54`), shipping `Stripe.net`, `BCrypt.Net-Next`, and the full MongoDB driver into the app bundle (`07-build.md`) — needless attack surface and app-size cost on end-user devices.

⚠️ **Error bodies are discarded.** `AuthService.cs:33`, `BookingApiService.cs:27` check only `IsSuccessStatusCode`. No server error detail reaches the user — and no client-side logging exists in Release builds (`12-observability.md`).

---

## Data classification and PII

| Class | Fields | Where |
|---|---|---|
| **Credentials** | `password_hash` (BCrypt-12), `refresh_token.hash` (SHA-256) | `IdentityDb.credentials` |
| **PII — direct** | `email` on provider, customer, credential, note, message, notification, payment, device token; `first_name`, `last_name` | `agenda_buddy.*`, `IdentityDb.*` |
| **PII — sensitive** | `NoteEntity.content` — provider's **private session notes** for therapy/coaching clients | `NoteEntity.cs:28` |
| **Financial** | `PaymentEntity.amount`, `stripe_payment_intent_id` | `PaymentEntity.cs:39,45` |
| **Device** | `DeviceTokenEntity.token` (FCM registration token) | `DeviceTokenEntity.cs:17` |

⚠️ **No encryption at rest beyond whatever Atlas provides by default.** No field-level encryption, no CSFLE, no Queryable Encryption — despite `NoteEntity.content` holding therapy/coaching session notes, which is the most sensitive data in the product (F-008: "private session notes… visible only to the provider").

⚠️ **PII is copied wholesale into the audit EventStore.** Every command **and every query** serialises its payload into `Event.Data` as a JSON string (`EventAndCommands/Persitency/Event.cs:14`). `GetProvidersQueryHandler.cs:23` serialises the **entire provider list** — every provider, every embedded appointment, every customer email — into a Mongo document **on every anonymous `GET /api/v1/providers` call**. The `events` collection therefore accumulates unbounded, unindexed, never-pruned copies of the full dataset with **no retention policy and no actor field**. See `15-cqrs-and-messaging.md`.

⚠️ **No data-subject-rights capability.** No export, no deletion, no anonymisation. `BookingService.CancelAppointmentAsync` hard-deletes an appointment from the `appointments` collection (`03-services.md`) but the same appointment persists **embedded in the provider document** and in the `events` audit blobs — so "delete" leaves at least two copies. Any GDPR/CCPA erasure request is currently unsatisfiable.

⚠️ **No `[unknown — outside repo]` DPA, retention schedule, or privacy policy** is committed.

---

## Input validation surface

| Service | Mechanism | Anchor |
|---|---|---|
| Booking | `MiniValidator.TryValidate` on `AppointmentEntity` | `Booking/Program.cs:100,125,150` |
| Customer | `MiniValidator.TryValidate` on `CustomerEntity` | `Customer/Program.cs:99,130` |
| Provider | `MiniValidator.TryValidate` on `ProviderEntity` | `Provider/Program.cs:106,179` |
| Services | `MiniValidator.TryValidate` on `List<ServiceEntity>` | `Services/Program.cs:119,143` |
| Profession | `MiniValidator.TryValidate` on `ProfessionEntity` | `Profession/Program.cs:98` |
| Identity | ⚠️ **hand-rolled** — `EmailAddressAttribute`, length, role allow-list | `Identity/Program.cs:100-106` |
| Calendar | ❌ **none** — no request bodies; `{email}` route value unvalidated | — |

⚠️ **`CONSTITUTION.md` §4 requires `MiniValidator` at every endpoint.** Identity validates by hand and Calendar not at all — and `Calendar.csproj` does not even reference `MiniValidation`.

⚠️ **No business-rule validation.** `MiniValidator` enforces only data annotations. Nothing checks that `Start < End` on an appointment, that `Start` is in the future, or that the slot does not overlap an existing appointment (`05-data-model.md`) — so **double-booking is unprevented**, which `INTENT.md` names as a core user frustration.

⚠️ **`BsonDocument` filters are built from user input** — `SupportTools.FilterByEmail(email)` with a route-supplied `{email}` (`Provider/Program.cs:159`). This is **not** an injection risk (the driver treats the value as a BSON string, not a query fragment), but `new ObjectId(id)` at `MongoDbRepository.cs:28` throws `FormatException` on malformed input, surfacing as a **500** rather than a 400 (`10-error-handling.md`).

⚠️ **No request size limits, no `body` length caps.** `MessageEntity.Body` and `NoteEntity.Content` have `[Required]` but no `[MaxLength]` (`05-data-model.md`), so a client can post documents up to Kestlan's default limit.

---

## ⚠️ `services.BuildServiceProvider()` inside DI registration

`Library.ServerAuth/AuthenticationExtensions.cs:52-63`:

```csharp
private static void LogKeyFingerprint(IServiceCollection services, RsaSecurityKey rsaKey)
{
    var sp = services.BuildServiceProvider();          // :54  ⚠️ ASP0000
    var loggerFactory = sp.GetService<ILoggerFactory>();
    ...
    logger.LogInformation("RSA public key loaded (fingerprint: {Fingerprint})", fingerprint);  // :62
}
```

Called from `AddAgendaBuddyAuthentication` (`:30`) in **all seven services**. This builds a **second, throwaway DI container** during registration:
- Every singleton registered up to that point is instantiated **twice** — in the throwaway provider and later in the real one.
- The throwaway provider is never disposed, so its disposable singletons leak for the process lifetime.
- Only registrations made *before* `AddAgendaBuddyAuthentication()` are visible, making the behaviour dependent on call ordering in `Program.cs`.

All of this to emit one informational line. The correct approach is to log from a hosted service or after `builder.Build()`. Note the intent is good — logging the SHA-256 fingerprint of the public key without logging key material (`:59-61`) is exactly right for key-rotation verification.

---

## Negative findings — controls that do not exist

- ❌ **No CI security scan** — no `dotnet list package --vulnerable`, no CodeQL, no Dependabot (`.github/dependabot.yml` absent), no secret scanner, no container image scan. `CONSTITUTION.md` §7 marks this mandatory and un-uncheckable. **Unmet gate.**
- ✅ **Rate limiting and account lockout** — F-021.
- ❌ **No CORS policy** in any service.
- ✅ **HSTS** — F-021, configuration-gated, inert until TLS is terminated (F-017).
- ❌ **No role-based authorization** — `AssertRole` is dead.
- ❌ **No token revocation / denylist** — `jti` unused.
- ❌ **No password reset, change, or forced-reset flow** — `MustResetPassword` unread.
- ❌ **No audit of *who* performed an action** — `Event` has `status`, `type`, `data`, `timestamp` and **no actor field** (`05-data-model.md`).
- ❌ **No secrets manager** — no Key Vault/Secrets Manager; three projects declare a `UserSecretsId` and none reads secrets (`06-configuration.md`).
- ❌ **No `.env.example`** despite Compose interpolating `${JWT_PUBLIC_KEY}`/`${JWT_PRIVATE_KEY}` from a gitignored `.env`.
- ❌ **No field-level encryption** for session notes or payment data.
- ❌ **No security headers** — no CSP, `X-Content-Type-Options`, `Referrer-Policy` (defensible for a pure JSON API, but `UseAntiforgery` implies browser use).
- ❌ **No dependency pinning by lock file** — `packages.lock.json` absent, so restores are not reproducible and a compromised upstream version could be pulled silently.

## Open items, ranked

1. **Rotate the Atlas credential and purge it from all 14 files** (history rewrite or credential rotation — rotation is mandatory regardless).
2. **Add the mandatory CI security scan** — secret scanning first, then dependency audit. This is a constitution gate that is currently unimplemented.
3. **Authenticate and ownership-guard the six anonymous PII endpoints**; add pagination.
4. **Add `OwnershipGuard` to the two Calendar routes** (authenticated-but-unscoped IDOR).
5. ~~**Replace `RefreshAsync`'s delete-then-insert**~~ — **DONE, F-021-T02.**
6. ~~**Move `UseHttpsRedirection` before `UseAuthentication`**, add `UseHsts`~~ — **DONE, F-021-T05.** Configuring a real HTTPS endpoint remains **F-017's**, and until it exists HSTS is decorative.
7. **Wire the mobile refresh-token flow** — currently a hard logout at 60 minutes.
8. **Map `ForbiddenException → 403` centrally** so a forgotten `try/catch` cannot silently become a 500.
9. ~~**Add rate limiting and lockout** on `POST /api/v1/auth/login`~~ — **DONE, F-021-T03/T06**, and on `register` too, which hashes at the same cost.
10. **Add role checks** on provider creation and profession creation.
11. **Replace `services.BuildServiceProvider()`** in `AuthenticationExtensions`.
12. **Define retention and pruning for the `events` collection**, and stop writing full payloads on read queries.
