# Episode 011: Password Reset Flow

**Episode ID:** 011
**Feature name:** Password Reset Flow — an account holder who forgets their password now has a recovery path, and a forced-reset flag now actually forces one
**Feature slug:** password-reset-flow
**Feature ID:** F-022
**Date built:** 2026-08-27, on `feat/F-022-password-reset-flow`
**Phase delivered in:** Construction
**Date shipped:** 2026-08-27 — merged via the mandated PR path (ADR-050), PR #76, tagged **`v0.11.0`**
**Status:** Final

---

## What Was Built

There was no password reset, change, or forced-reset flow anywhere in the solution.
`CredentialEntity.MustResetPassword` was written by seed data and read by nothing — the forced-reset
flow the field exists for did not exist. There was no `/password-reset` endpoint at all.

Two new routes and one enforcement fix:

1. **`POST /api/v1/auth/password-reset/request`** — always `202`, whether or not `email` matches an
   account (anti-enumeration). A matched request issues a single-use, 30-minute-expiry opaque token,
   stored only as a SHA-256 hash on a new `CredentialEntity.ResetToken` sub-document — the same shape
   and single-use technique (`RefreshTokenDocument`) the refresh token already used.
2. **`POST /api/v1/auth/password-reset/confirm`** — a valid, unexpired, not-yet-used token sets the new
   password (BCrypt, work factor 12) and, in the same targeted write, clears the reset flag, any active
   refresh token (ends every existing session), and any lockout. A wrong token, an expired token, a
   reused token, and an unknown email are all one `401` outcome — no side channel distinguishes them,
   the same reasoning `RefreshAsync` already applied.
3. **`LoginAsync` now enforces `MustResetPassword`** — a correct password on a flagged account is
   blocked (`403 password_reset_required`) rather than silently issuing a session.

**The delivery-channel assumption in the original feature record turned out to be wrong, and got
corrected rather than shipped anyway.** F-022 was filed assuming `NotificationService` (wired by F-014)
was the delivery channel. Building the feature surfaced that it is an **in-app inbox** requiring
authentication (`GET /api/v1/notifications`) — unreachable by the very user this feature exists to help.
No SMTP/SMS provider exists anywhere in this project. Resolved as ADR-052: same category as ADR-038's
non-charging payment gateway — the reset token is logged at `Information` for local-development
visibility, and a `NotificationEntity` (`NotificationType.PasswordResetRequested`, a new 5th enum value)
is still written as a secondary audit signal for an account holder logged in elsewhere. Extending that
shared enum also drifted `Customer.json`'s committed OpenAPI baseline (its `GET /notifications` schema
serializes the same type) — both `Identity.json` and `Customer.json` were regenerated together.

**Also scoped, and deliberately not built here.** Mobile UI ("Forgot password" screens,
`IAuthApiService` wiring) — the backend capability existing at all was this feature's ask; the
presentation layer is separate follow-on work, filed as `agenda-buddy-qe9` (same split-scope shape as
F-025's `agenda-buddy-m6m`).

Suites: backend 560/560 (550 baseline + 10 new), integration 314/314 (no new integration tests — the new
flow is covered thoroughly at the unit level via `InMemoryCredentialRepository`, extended to support the
new `reset_token`/`password_hash`/`must_reset_password` write paths), 0 failures, 0 regressions.
`dotnet format --verify-no-changes` clean.

---

## Links

| Artifact | Path |
|---|---|
| PRD | [`PRD_F-022_password-reset-flow_2026-08-27.md`](../prds/PRD_F-022_password-reset-flow_2026-08-27.md) |
| Feature record | [`docs/pdlc/tasks/F-022/`](../tasks/F-022/) |
| Decisions | ADR-052 |
| Follow-on | `agenda-buddy-qe9` (mobile UI, deliberately descoped) |
