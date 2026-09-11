# Review: Email Registration and Onboarding

**Date reviewed:** 2026-09-10
**Phase:** Discover / Review
**Status:** Changes required before release

## Outcome

The current flow creates a valid authenticated session before the registered email address is verified.
Email confirmation exists in the Identity service, but it is informational only: registration returns an
access token and refresh token, login ignores `EmailVerified`, and refresh ignores it as well. The current
email is plain text and cannot render the requested action button. No mobile deep-link handler completes
the confirmation flow.

The release gate is **not met**. Email ownership must become a prerequisite for every session-issuing path,
and the email-to-app path must be implemented and tested as one flow.

## Findings

### Critical: unverified accounts receive authenticated access

- `IdentityService.RegisterAsync` creates and returns an access token and an active refresh token while
  storing `EmailVerified = false`.
- `IdentityService.LoginAsync` issues a session without checking `EmailVerified`.
- `IdentityService.RefreshAsync` rotates a refresh token without checking `EmailVerified`.
- `RegisterViewModel` treats registration as sign-in, creates the role profile, and starts onboarding.

Changing only `/login` would leave registration and refresh as bypasses. The invariant must be enforced at
every session-issuing path: **an unverified credential cannot obtain or refresh an authenticated session**.

### Important: the confirmation email has no action button

`BuildConfirmationEmail` produces plain text. `ResendEmailSender` sends only Resend's `text` field, so HTML
markup placed in the body would be displayed as text rather than as a button.

The email must contain both language blocks and a plain-text fallback:

> Welcome to AgendaMe. Please confirm your email address by clicking the button below.
>
> **Confirm email**
>
> Bienvenido a AgendaMe. Por favor, confirma tu correo electrónico haciendo clic en el botón de abajo.
>
> **Confirmar correo**

Both buttons point to the same single-use confirmation URL. The corrected punctuation and accents are part
of the customer-facing copy; they do not change the requested meaning.

### Important: the generated link cannot complete the current API contract

When `Email:AppLinkBaseUrl` is configured, `BuildConfirmationEmail` includes only `token`, while
`POST /api/v1/auth/register/confirm` requires both `email` and `token`. Even with a mobile handler, the
generated link lacks enough data to call the current endpoint.

Do not add the email address to the URL. Query strings are commonly retained in browser history, proxy
logs, analytics, crash reports, and OS handoff logs. The opaque random token is already unique and stored
only as a SHA-256 hash. Confirmation should match the hash and expiry atomically, without email in the
request or URL.

### Corrected: no owned web domain exists

The earlier review assumed `agendame.app` was owned. It is not, so Universal/App Links and domain
association files are not viable. The email button uses AgendaMe's registered `agendame://` custom scheme
when the app is installed. A separate six-digit, email-bound, rate-limited code is the universal fallback;
the button is convenience, not the only route to confirmation.

### Advisory: delivery failure can strand a new account

Email delivery is best-effort by contract. Once verification gates access, a transient or permanent send
failure leaves the address registered but unusable. The flow therefore needs a resend endpoint and a
pending-verification screen. Resend must return the same public response for known, unknown, and already
verified addresses to avoid account enumeration, and should be rate-limited.

## Target Flow

1. The app submits registration details.
2. Identity stores the password hash, role, `EmailVerified = false`, and a hashed 24-hour confirmation
   token. It stores no refresh token and returns no access token.
3. Identity sends the bilingual HTML email with a plain-text fallback.
4. The app shows a pending-verification screen with **Resend email** and **Back to sign in** actions. It
   does not create the Customer or Provider profile yet.
5. The `agendame://` button opens AgendaMe when installed; otherwise the user returns to the pending screen
  and enters the six-digit code from the email.
6. Identity atomically confirms either the opaque token or matching `{email, code}`. Both are stored only as
  hashes; the code expires after 15 minutes and the button token after 24 hours.
7. The app routes directly to sign-in after successful confirmation. Normal profile creation and role onboarding begin
   only after a successful password login.
8. Invalid, expired, and replayed links produce one public result and offer **Send a new link**.

## Acceptance Criteria

- **AC1:** Registration returns a pending-verification response and never returns or persists an active
  access/refresh session.
- **AC2:** Correct credentials for an unverified account return `403 email_verification_required`; no token
  is minted or rotated.
- **AC3:** Refresh cannot issue a session for an unverified credential.
- **AC4:** The delivered email contains the approved English and Spanish copy, two accessible CTA labels,
  one installed-app confirmation target, a six-digit code, and a plain-text fallback.
- **AC5:** The URL and confirmation request contain only the opaque token, never the email address.
- **AC6:** A valid, unexpired token atomically verifies the credential, clears the token, and emits a
  token-free structured log event.
- **AC7:** Wrong, expired, and replayed tokens have the same public response and do not modify the account.
- **AC8:** The custom-scheme button opens the installed Android/iOS app, and the six-digit code confirms the
  account when direct app opening is unavailable.
- **AC9:** The app clearly renders pending, confirming, verified, invalid/expired, offline, and resend
  throttled states; only verified users proceed to sign-in and profile onboarding.
- **AC10:** Resend is anti-enumerating, rotates the old confirmation token, is rate-limited, and never logs
  either raw token.
- **AC11:** Existing accounts created before this policy receive an explicit migration decision; the
  deployment must not silently lock every legacy account because a missing BSON boolean reads as `false`.
- **AC12:** Unit, integration, email-payload, mobile routing, custom-scheme, and code-entry tests cover the
  complete flow and the three former session bypasses.

## Threat Review

| Threat | Control |
| --- | --- |
| Session before ownership proof | Gate registration, login, and refresh on the same invariant |
| Token theft from storage/logging | Store SHA-256 only; never log token or provider response body |
| PII leakage through URLs | Remove email from confirmation URL and request |
| Link replay | Atomic hash + expiry filter; unset token on success |
| Custom-scheme interception or unavailable app | Email-bound six-digit fallback code; password still required for session |
| Account enumeration through login/resend | Stable public errors and responses; rate limiting |
| Delivery outage strands account | Pending screen, resend path, observable delivery failure |
| Legacy-account lockout | Explicit migration/backfill policy before enforcement |

## Proposed Build Slices

1. Identity contract and policy: pending registration, token-only confirmation, verification gates, resend,
   migration policy, and API/integration tests.
2. Email delivery: structured plain-text + HTML message support, bilingual template, accessible buttons,
   and payload tests.
3. Mobile flow: pending and confirmation screens, token-only route builder, state handling, and no profile
   creation before verified sign-in.
4. Platform links: registered custom scheme on Android/iOS plus device-level verification.
5. PDLC Verify: OpenAPI regeneration, threat-control evidence, real Resend delivery, physical Android/iOS
   link smoke tests, and rollout evidence for legacy credentials.

## Approved Decisions

1. **Link handling (superseded 2026-09-10):** do not use `agendame.app`; no such domain is owned. Use the
  installed-app `agendame://` scheme plus an email-bound six-digit fallback code.
2. **Legacy accounts:** reset non-production credentials when enforcement rolls out, then apply the same
  verification rule to every newly registered account. No production-account migration is required for
  the current rollout.
3. **After confirmation:** show a successful verification state, then require the user to sign in with
  their password. Possession of the email link alone does not create an authenticated session.
4. **Language:** send one bilingual email to every recipient. The app has no persisted language preference
  before registration, so selecting one language is not deterministic today.
5. **Platform rollout:** both mobile platforms register the existing custom scheme. No domain association
  files or Google Play signing fingerprint are required for email confirmation.
