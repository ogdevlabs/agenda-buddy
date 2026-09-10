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

### Important: no verified app-link path exists

The mobile app has no confirmation route, URI activation handler, Android App Link association, or iOS
Universal Link association. A custom URI scheme alone can be claimed by another installed application and
is not sufficient for a bearer confirmation token.

Build requires an owned HTTPS origin that serves:

- `/.well-known/apple-app-site-association`
- `/.well-known/assetlinks.json`

The link should open AgendaMe when installed and a small HTTPS confirmation fallback when it is not.

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
5. The HTTPS confirmation link opens AgendaMe through verified platform association.
6. The app sends only the opaque token to Identity. Identity atomically matches its hash and expiry, sets
   `email_verified = true`, removes the token, and logs `credential.email-confirmed` without the token.
7. The app shows **Email verified** and routes to sign-in. Normal profile creation and role onboarding begin
   only after a successful password login.
8. Invalid, expired, and replayed links produce one public result and offer **Send a new link**.

## Acceptance Criteria

- **AC1:** Registration returns a pending-verification response and never returns or persists an active
  access/refresh session.
- **AC2:** Correct credentials for an unverified account return `403 email_verification_required`; no token
  is minted or rotated.
- **AC3:** Refresh cannot issue a session for an unverified credential.
- **AC4:** The delivered email contains the approved English and Spanish copy, two accessible CTA labels,
  one HTTPS confirmation target, and a plain-text fallback.
- **AC5:** The URL and confirmation request contain only the opaque token, never the email address.
- **AC6:** A valid, unexpired token atomically verifies the credential, clears the token, and emits a
  token-free structured log event.
- **AC7:** Wrong, expired, and replayed tokens have the same public response and do not modify the account.
- **AC8:** The verified HTTPS link opens the installed Android/iOS app; without the app it renders a safe
  browser fallback.
- **AC9:** The app clearly renders pending, confirming, verified, invalid/expired, offline, and resend
  throttled states; only verified users proceed to sign-in and profile onboarding.
- **AC10:** Resend is anti-enumerating, rotates the old confirmation token, is rate-limited, and never logs
  either raw token.
- **AC11:** Existing accounts created before this policy receive an explicit migration decision; the
  deployment must not silently lock every legacy account because a missing BSON boolean reads as `false`.
- **AC12:** Unit, integration, email-payload, mobile routing, Android association, and iOS association tests
  cover the complete flow and the three former session bypasses.

## Threat Review

| Threat | Control |
|---|---|
| Session before ownership proof | Gate registration, login, and refresh on the same invariant |
| Token theft from storage/logging | Store SHA-256 only; never log token or provider response body |
| PII leakage through URLs | Remove email from confirmation URL and request |
| Link replay | Atomic hash + expiry filter; unset token on success |
| Link interception by another app | Verified HTTPS Universal Links / Android App Links |
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
4. Platform links: HTTPS fallback endpoint, Android association/intent filter, iOS association/entitlement,
   and device-level verification.
5. PDLC Verify: OpenAPI regeneration, threat-control evidence, real Resend delivery, physical Android/iOS
   link smoke tests, and rollout evidence for legacy credentials.

## Approved Decisions

1. **Link origin:** use `https://agendame.app` for confirmation links, the browser fallback, and both
  platform association files.
2. **Legacy accounts:** reset non-production credentials when enforcement rolls out, then apply the same
  verification rule to every newly registered account. No production-account migration is required for
  the current rollout.
3. **After confirmation:** show a successful verification state, then require the user to sign in with
  their password. Possession of the email link alone does not create an authenticated session.
4. **Language:** send one bilingual email to every recipient. The app has no persisted language preference
  before registration, so selecting one language is not deterministic today.
5. **Platform rollout:** enable verified email links on iOS first. Android App Links remain an explicit TODO
  until a physical Android device and the Google Play app-signing SHA-256 fingerprint are available; the
  `AndroidEmailVerificationAppLinksEnabled` build property defaults to `false`, and a deployable
  `assetlinks.json` must remain absent until then.