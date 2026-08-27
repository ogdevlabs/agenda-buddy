---
feature: F-022
slug: password-reset-flow
status: approved
approved-by: ogdevlabs (full-autonomy grant, see STATE.md 2026-08-26T23:12:00Z)
approved-date: 2026-08-27
---

# PRD: Password Reset Flow (F-022)

## Problem

There is no password reset, change, or forced-reset flow anywhere in the solution.
`CredentialEntity.MustResetPassword` is declared and set by seed data (`SeedAuthCredentials.cs`) but
never read by `LoginAsync` — the forced-reset flow the field exists for does not exist. There is no
`/password-reset` endpoint at all, so a user who forgets their password has no recovery path.

## Users affected

Any Provider or Customer who forgets their password, and any account migration-seeded with
`MustResetPassword = true` (currently signs in normally, with the flag silently ignored).

## Requirements

- **R1.** A caller can request a password reset by email; the response is identical (`202 Accepted`)
  whether or not the address matches an account — anti-enumeration, same principle as `/login`'s
  constant-time dummy hash.
- **R2.** A matched request issues a single-use, time-limited (30-minute) opaque token, stored only as
  a SHA-256 hash — never the raw token — mirroring the existing refresh-token pattern.
- **R3.** A caller can confirm a reset with `{email, token, newPassword}`; a valid, unexpired,
  not-yet-used token sets the new password (BCrypt, work factor 12, same as registration) and clears
  the reset flag.
- **R4.** A successful confirm also clears any active session (refresh token) and any lockout on the
  account — the same posture a real "forgot password" recovery takes elsewhere, since the old password
  can no longer be trusted.
- **R5.** A wrong token, an expired token, a reused token, and an unknown email are all one outcome to
  the caller (`401`) — no side channel distinguishes them, same reasoning as `RefreshAsync`.
- **R6.** `LoginAsync` now honors `MustResetPassword`: a correct password on a flagged account is
  blocked (`403 password_reset_required`) rather than issuing a session, until a reset is confirmed.
- **R7.** The new password is validated to the same minimum (8 characters) as registration.

## Non-goals

- **Real email/SMS delivery.** No provider is configured or in scope — see ADR-052. The reset token is
  logged for local-development visibility, and an in-app `NotificationEntity` is written as a secondary
  audit signal; neither is a substitute for real delivery, which is future work if this project ever
  needs it.
- **Mobile UI ("Forgot password" screens).** The backend capability existing at all is this PRD's ask;
  wiring `AgendaBuddy.MobileApp`'s screens/view-models is presentation-layer follow-on work, filed
  separately rather than bundled in (same shape as F-025's `agenda-buddy-m6m` split).

## Acceptance criteria

- AC1. `POST /api/v1/auth/password-reset/request` always returns `202`.
- AC2. A request for a known account writes a hashed, 30-minute-expiry token via a targeted
  `FindOneAndUpdateAsync` — never an upsert, never a whole-document replacement.
- AC3. `POST /api/v1/auth/password-reset/confirm` with a valid token returns `204`, and a subsequent
  login with the old password fails while one with the new password succeeds.
- AC4. The same token cannot be confirmed twice.
- AC5. A token confirmed after its 30-minute expiry is rejected.
- AC6. `LoginAsync` throws `PasswordResetRequiredException` for a `MustResetPassword` account with a
  correct password, and does not rotate a refresh token in that path.
- AC7. `docs/api/openapi/Identity.json` and `Customer.json` (the shared `NotificationType` enum) are
  regenerated and drift-clean; the Bruno collection has request files for both new routes.
