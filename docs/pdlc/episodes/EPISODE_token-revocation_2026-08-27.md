# Episode 012: Token Revocation

**Episode ID:** 012
**Feature name:** Token Revocation — logging out now stops an access token from working, instead of leaving it valid for up to an hour
**Feature slug:** token-revocation
**Feature ID:** F-023
**Date built:** 2026-08-27, on `feat/F-023-token-revocation`
**Phase delivered in:** Construction
**Date shipped:** 2026-08-27 — merged via the mandated PR path (ADR-050)
**Status:** Final

---

## What Was Built

A fresh `jti` was minted into every access token but never recorded or checked, and no denylist
existed. `LogoutAsync` cleared the stored refresh token, but the access token itself stayed valid for
up to its full 60-minute lifetime after logout — a leaked or post-logout token had the widest possible
blast radius, since all seven services accept any token the shared issuer minted.

**Denylist store (ADR-053).** A new MongoDB collection, `revoked_tokens`, keyed by `jti`, with a TTL
index on `expires_at` so a revoked entry never outlives the token it revokes. `ITokenRevocationStore`
is defined in `AgendaBuddy.Library.ServerAuth` (interface only); `MongoTokenRevocationStore` implements
it in `AgendaBuddy.Library`, which gained a `ProjectReference` to `Library.ServerAuth` to see the
interface type. `AuthenticationExtensions.AddAgendaBuddyAuthentication`'s `OnTokenValidated` hook checks
it once per authenticated request, across all seven services — one indexed lookup, resolved from the
request's own DI scope. Chosen over introducing a distributed cache (Redis, etc.): no such
infrastructure exists in this project's Aspire AppHost today, and every service already holds the
`IMongoClient` this needed instead.

**`POST /api/v1/auth/logout` gains an optional `accessToken` field**, backward compatible — omitting it
behaves exactly as before. When present, `IdentityService.LogoutAsync` decodes (does not re-verify) it
to read the `jti`/`exp` and revokes it; a garbage submission is silently ignored, same as omitting the
field. The mobile client (`AuthService.LogoutAsync`) now reads its stored access token before clearing
it and sends it alongside the refresh token — through the no-auth HTTP client, deliberately, so this
call never triggers `JwtDelegatingHandler`'s own 401-refresh-and-retry (which would rotate the refresh
token this request is trying to invalidate out from under it).

**No `aud` claim, `ValidateAudience` stays `false` — evaluated and rejected (ADR-053).** All seven
services trust one issuer uniformly today; a shared audience would duplicate the existing
`ValidateIssuer` check with no narrowing, and per-service audiences would remove this project's actual
design (one token, every service accepts it) rather than harden it.

**A real regression caught before merge, not after.** The first implementation ran the TTL-index
bootstrap unconditionally at every service's startup; `AgendaBuddy.IntegrationTests`' `OpenApiSpecGenerator`
harness deliberately gives every service a syntactically-valid-but-unreachable Mongo connection string
at boot (so spec generation needs no real database) — the unguarded `await` crashed all seven hosts
under that harness, failing 15 integration tests. Fixed by wrapping the bootstrap in try/catch, same
posture as `ProfessionSeedHostedService`'s existing "must start even with no database" precedent.

**Also found, filed separately, not fixed here.** `scripts/generate-openapi.sh` curls each service's
live `/swagger/v1/swagger.json` (Swashbuckle's own middleware), which serializes with different JSON
indentation than `OpenApiSpecGenerator`'s pinned writer settings — the two mechanisms have drifted
apart. `docs/api/openapi/Identity.json`'s update for this feature was written directly from
`OpenApiSpecCatalog`'s generator (the byte-deterministic path `OpenApiSpecDriftTest` actually checks),
not the shell script, to avoid reformatting all seven committed specs as an unrelated side effect.

Suites: backend 563/563 (560 baseline + 3 new), integration 316/316 (314 baseline + 2 new), 0 failures,
0 regressions. `dotnet format --verify-no-changes` clean.

---

## Links

| Artifact | Path |
|---|---|
| PRD | [`PRD_F-023_token-revocation_2026-08-27.md`](../prds/PRD_F-023_token-revocation_2026-08-27.md) |
| Feature record | [`docs/pdlc/tasks/F-023/`](../tasks/F-023/) |
| Decisions | ADR-053 |
