---
feature: F-023
slug: token-revocation
status: approved
approved-by: ogdevlabs (full-autonomy grant, see STATE.md 2026-08-26T23:12:00Z)
approved-date: 2026-08-27
---

# PRD: Token Revocation (F-023)

## Problem

A fresh `jti` is minted into every access token (`IdentityService.cs`) but never recorded or checked.
`LogoutAsync` clears the stored refresh token, but the access token itself stays valid for up to its
full 60-minute lifetime after logout. A leaked or post-logout access token has the widest possible
blast radius: all seven services accept any token this issuer minted.

## Users affected

Any account holder who logs out (the access token should stop working then, not up to an hour later),
and anyone whose access token leaks (a shorter usable window narrows the exposure).

## Requirements

- **R1.** Logging out denylists the caller's own access token's `jti`, so it stops authenticating
  immediately, not just its refresh token.
- **R2.** The denylist is checked on every authenticated request, across all seven services — not just
  Identity's own routes.
- **R3.** The denylist is cross-service: a token revoked via Identity's `/logout` must be rejected by
  Booking, Calendar, Customer, Provider, Services, and Profession, not just Identity.
- **R4.** A denylist entry does not outlive the token it revokes — no unbounded growth.
- **R5.** The per-request check cost is one indexed lookup, not a scan or a cross-service network call
  beyond the database round-trip every request already makes.
- **R6.** A caller cannot revoke an arbitrary token by supplying a forged one — at worst, a garbage
  submission is silently ignored (see ADR-054's discussion of why this is safe without re-verifying the
  signature).

## Non-goals

- **A real `aud` claim / `ValidateAudience = true`.** Evaluated and rejected for this feature — see
  ADR-054. All seven services trust the same single issuer today; a shared `aud` value would duplicate
  `ValidateIssuer` without narrowing anything, and per-service audiences would break the existing
  one-token-many-services design, which is a separate, larger, unvalidated change.
  `ValidateAudience` stays `false`.
- **A distributed cache (Redis, etc.) as the denylist store.** No such infrastructure exists in this
  project's Aspire AppHost today (MongoDB + Kafka only). Introducing one for a single-purpose denylist
  is a bigger infrastructure decision than this feature's scope — see ADR-054.
- **Mobile UI changes.** The mobile client already attaches its stored access token to every
  authenticated call and reacts to `401` by refreshing; revocation surfaces as an ordinary `401` it
  already knows how to handle. No `AgendaBuddy.MobileApp` change is required.

## Acceptance criteria

- **AC1.** `POST /api/v1/auth/logout` with a valid access token in the request body denylists that
  token's `jti`; a subsequent authenticated request bearing the same access token, against any of the
  seven services, is rejected `401`.
- **AC2.** `POST /api/v1/auth/logout` with no access token in the request body behaves exactly as
  before (clears the refresh token only) — backward compatible.
- **AC3.** A revoked entry's TTL matches the revoked token's own remaining lifetime — it is not kept
  indefinitely.
- **AC4.** A token whose `jti` is not denylisted authenticates exactly as it did before this feature.
- **AC5.** Proven against a real MongoDB-backed store in an integration test, not just a unit-level fake.
