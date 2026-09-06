# Threat Model — mobile-app
<!-- pdlc-template-version: 1.0.0 -->

**Triage:** Full
**Convened:** 2026-07-31
**Lead:** Phantom (Security Reviewer)
**Participants:** Phantom, Neo, Bolt, Echo, Pulse, Atlas, Muse, Jarvis, Friday
**Status:** Pending human approval (Step 12)

---

## Triage Record

| Question | Answer | Evidence |
|---|---|---|
| Does this feature introduce or modify a trust boundary? | **yes** | New mobile client is an untrusted boundary; new `POST /identity/device-token` endpoint; FCM/APNs egress (backend → Google/Apple) |
| Does this feature touch regulated data (PII, payment, health, biometric, children's)? | **yes** | JWT (credential), email addresses (PII per CONSTITUTION.md §4), device tokens linked to user identity |
| Does this feature add a new attack surface? | **yes** | `POST /identity/device-token`; FCM token registration flow; mobile client JWT storage surface; push notification payload |

**Triage outcome:** Full

---

## Trust Boundaries

| ID | Boundary | What crosses | Trust direction | Diagram reference |
|---|---|---|---|---|
| TB-1 | Mobile app → Identity service | Login credentials (email + password), `POST /device-token` (FCM/APNs token + JWT), JWT response body | untrusted → semi-trusted | ARCHITECTURE.md §System Placement |
| TB-2 | Mobile app → Any backend service | JWT bearer token + mutation request bodies (appointment status, message bodies) | untrusted → trusted | ARCHITECTURE.md §Infrastructure |
| TB-3 | Mobile app ↔ OS Secure Storage | JWT on iOS Keychain / Android Keystore | app-trusted ↔ OS-trusted | ARCHITECTURE.md §Infrastructure |
| TB-4 | Backend → FCM/APNs (egress) | Push notification payload (may include user email, appointment details) | trusted → external-untrusted | ARCHITECTURE.md §Key User-Journey Data Flows |
| TB-5 | FCM/APNs → Device (ingress) | Push payload arriving at device — visible on lock screen before authentication | external → untrusted device surface | ARCHITECTURE.md §Infrastructure |

---

## Threats Identified

### T-001 — JWT and Credentials in Server Logs

- **STRIDE category:** Information Disclosure
- **Trust boundary:** TB-1
- **Asset affected:** JWT (provider credentials), user email (PII)
- **Attack vector:** If ASP.NET Core request/response body logging is active at DEBUG or TRACE level on the Identity service, the `POST /login` response body (containing the JWT) and the request body (containing the plaintext password) could appear in application logs. Any user with log-reader access (ops, monitoring dashboards, CI artifacts) could extract live credentials. Per CONSTITUTION.md §4, PII must not appear in logs — email as JWT `sub` also flows through this boundary.
- **Severity:** HIGH
- **DREAD breakdown:** Damage H · Reproducibility H (if debug logging is on, 100% reproduction) · Exploitability H (no special skill beyond log access) · Affected users: all authenticated users · Discoverability M (requires log access, but many people have it)
- **Mapped frameworks:** OWASP API Top 10 — API8:2023 (Security Misconfiguration); CWE-532 (Insertion of Sensitive Information into Log File)
- **Current mitigation status:** Partial — CONSTITUTION.md §4 prohibits PII in logs, but no explicit log-sanitization guard exists for the login endpoint body in the current Identity service implementation
- **Proposed action (party recommendation):** Mitigate now
  - **Specific change:** In `Identity/Program.cs`, ensure no request body logging middleware captures the login endpoint payload. If structured logging (Serilog/OpenTelemetry) is in use, add a `DestructuringPolicy` or request filter that excludes the `/identity/login` and `/identity/device-token` endpoints from body logging. Add an explicit integration test: `POST /login` → assert no `password` or `token` field appears in captured log output.
  - **Bolt's effort estimate:** Low — 1–2h audit + explicit exclusion, 1h test
  - **Neo's architectural-fit confirmation:** Fits in `Library/Tools/` as a reusable log-sanitization extension; consistent with existing CONSTITUTION §4 constraints
  - **Will land as Plan-phase Beads task:** yes — `[mobile-app] T-001: verify login endpoint log sanitization`
- **Decision (human, at Step 12 approval):** *[blank until human reviews]*
- **Cross-talk note:** Phantom flagged the credential logging risk; Echo confirmed it is testable via a log-capture integration test in the Identity.Tests project.

---

### T-002 — PII Exposed in Push Notification Lock-Screen Payload

- **STRIDE category:** Information Disclosure
- **Trust boundary:** TB-5
- **Asset affected:** Customer email, appointment time, session type — PII visible on device lock screen
- **Attack vector:** The backend push dispatcher sends a notification payload that includes the customer's name/email and appointment time in the notification body (e.g., "Your 2pm appointment with coach@example.com has been confirmed"). On a shared, borrowed, or lost device, the lock screen displays this notification body without requiring authentication. An onlooker reads the customer's scheduling data without consent.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M · Reproducibility H (every push event) · Exploitability L (requires physical device access) · Affected users: individual (per-device exposure) · Discoverability H (lock screen is publicly visible by design)
- **Mapped frameworks:** OWASP Mobile Top 10 — M1:2023 (Improper Credential Usage — sensitive data in notification payload); CWE-200 (Exposure of Sensitive Information to Unauthorized Actor)
- **Current mitigation status:** None — no push dispatcher exists yet; this threat applies to the implementation task
- **Proposed action (party recommendation):** Mitigate now
  - **Specific change:** Push notification payload body MUST contain only a generic status string: "You have a new appointment update — open Agenda Buddy for details." The full appointment context is delivered only after the provider authenticates within the app. The push payload `data` field (not the `notification.body`) may carry the appointment ID so the app can deep-link to the correct detail screen after auth — this is safe because the `data` payload is not displayed on the lock screen.
  - **Bolt's effort estimate:** Low — this is a one-line template choice in the push dispatcher; costs nothing to do right from day one
  - **Neo's architectural-fit confirmation:** Backend push dispatcher controls the payload — no mobile-side change required
  - **Will land as Plan-phase Beads task:** yes — design constraint on the push dispatcher implementation task
- **Decision (human, at Step 12 approval):** *[blank until human reviews]*
- **Cross-talk note:** Pulse raised the lock-screen visibility risk; Atlas confirmed the business impact (customer PII visible to third parties violates the provider's trust relationship with their clients); Muse confirmed users expect push preview on lock screen but that "appointment with coach@example.com" is more detail than necessary.

---

### T-003 — Client-Crafted Status Transition (Tampering)

- **STRIDE category:** Tampering
- **Trust boundary:** TB-2
- **Asset affected:** `AppointmentEntity.AppointmentStatus` — appointment state integrity
- **Attack vector:** A provider with a valid JWT crafts a `PUT /booking/{id}` request with a status string not in the `AppointmentStatus` enum (e.g., `"Admin"` or a numeric value outside the defined range), attempting to drive the appointment into an undefined state or bypass the `Book()` / `Cancel()` transition guards.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage M · Reproducibility M (requires crafted HTTP client, valid JWT) · Exploitability M (needs knowledge of the enum structure) · Affected users: individual (own appointments only, OwnershipGuard prevents cross-account) · Discoverability L (not publicly documented)
- **Mapped frameworks:** OWASP API Top 10 — API3:2023 (Broken Object Property Level Authorization); CWE-20 (Improper Input Validation)
- **Current mitigation status:** Partial — C# `JsonStringEnumConverter` rejects unknown enum strings at deserialization, and `AppointmentEntity` state-machine methods (`Book()`, `Cancel()`, `Complete()`) enforce valid transitions. Not explicitly tested for boundary inputs.
- **Proposed action (party recommendation):** Accept — with test condition
  - **Rationale:** The existing ASP.NET Core enum binding and state-machine guards together provide sufficient defense. The risk is medium-low because OwnershipGuard already prevents cross-account manipulation; the only impact is to the attacker's own appointment. The mitigation is verification, not new code.
  - **Test condition:** The Plan-phase booking endpoint task must include an explicit negative test: `PUT /booking/{id}` with `status: "INVALID_VALUE"` → assert 400 response, no state change in DB.
  - **ADR in DECISIONS.md:** yes — accepted-risk record
- **Decision (human, at Step 12 approval):** *[blank until human reviews]*

---

## Threats Noted but Not Prioritized

| ID | Title | STRIDE | Boundary | Why deprioritized |
|---|---|---|---|---|
| T-NL-1 | No rate limit on `POST /identity/device-token` | Denial of Service | TB-1 | Authenticated endpoint; can only affect own account; consistent with existing no-rate-limit stance (deferred per DECISIONS.md). No cross-account impact. |
| T-NL-2 | 401 interception silently discards in-progress user input | Repudiation | TB-2 | UX concern more than security threat. Muse flagged as a UX finding — carried to `ux-review.md`. Impact is user frustration, not data breach. |

---

## Open Questions for Human

*None. All threats are within design authority — mitigations are design-level decisions with no org-specific dependency (no regulatory exposure question, no threat-actor profile ambiguity, no contractual constraint unknown at this stage).*

---

## Approval Outcomes (filled in at Step 12)

| Threat ID | Party recommendation | Human decision | Rationale |
|---|---|---|---|
| T-001 | Mitigate now | *[pending]* | — |
| T-002 | Mitigate now | *[pending]* | — |
| T-003 | Accept (with test condition) | *[pending]* | — |
| T-NL-1 | Noted / not prioritized | *[pending]* | — |
| T-NL-2 | Noted / not prioritized | *[pending]* | — |

**ADR registry updates required (after human approval):**
- ADR for T-003 accepted risk (enum validation deferred to test coverage only)

**Beads tasks to be created at Plan (Step 13):**
- T-001 mitigation: verify login endpoint log sanitization (Identity.Tests)
- T-002 mitigation: push notification payload body must be PII-free (push dispatcher implementation constraint)
- T-003 test condition: negative test for invalid status string on `PUT /booking/{id}`

---

## Mitigation Status — verified against code 2026-09-06 (F-028)

### T-002 — was NOT implemented, and shipped that way for two features

The mitigation above was recommended *Mitigate now* and recorded as a Plan-phase task, and the push dispatcher
was then built (and shipped, twice — Android push in F-028's predecessors, then iOS) **without it**.
`NotificationDispatcher` passed the producer's own `Subject`/`Body` straight into
`IPushSender.SendAsync`, so what reached the lock screen was:

| Producer | On the lock screen, unauthenticated |
|---|---|
| `BookingAppointmentCommandHandler` | the customer's email address, the service name and the appointment time |
| `ChangeAppointmentStatusCommandHandler`, `CancelAppointmentCommandHandler` | the same, for the changed appointment |
| `MessageModule` | **the sender's email address in the title**, and a 120-character preview of the private message in the body |

The third case is worse than this threat model anticipated: T-002 was written about *appointment* metadata, and
the messaging feature (added later) put conversation content on the same path. A private message on a locked
screen is a larger disclosure than "your 2pm with coach@example.com".

**Why it stayed invisible.** Nothing about the code looks wrong — the dispatcher forwards the strings it was
given, and the result is a notification that arrives, reads well, and does exactly what a reader expects. There
was no test asserting the payload's *content*, only that a push was sent. Two separate features reviewed this
path without catching it.

**Made temporarily worse by F-028 before being found.** ADR-062 added `default_sound` and high priority to every
message. That converted an exposure a user might never notice into one that reliably wakes and lights a locked
screen — which is how the omission surfaced.

**Now mitigated** per **ADR-060**: the OS-displayed `title`/`body` are derived from `NotificationType` alone
(`NotificationDispatcher.DisplayText`) and carry a category, never content; the producer's real text moves to
the FCM `data` payload, which the OS hands to the app rather than drawing, and is rendered only by the in-app
banner and inbox — both behind authentication. That is precisely the mechanism this threat's own
recommendation named. Enforced by `NotificationDispatcherTest`'s T-002 section
(`ThePushedTitleAndBodyNeverCarryTheProducersText`, over every `NotificationType`), which is the mitigation's
**only** enforcement.

### T-NEW-1 — a signed-out account stayed addressable through its device token

Not in the original model, found during F-028. `POST /identity/device-token` is listed above as new attack
surface, but the model considered only its *own* authorisation, not the lifetime of what it writes.

- **STRIDE category:** Information Disclosure · **Trust boundary:** TB-1 / TB-4 · **Severity:** MEDIUM
- **Attack vector:** `DeviceTokenService` keys its row on the account's email and `LogoutAsync` removed nothing
  server-side, so the registration outlived the session. Sign out as A and sign in as B on the same device and
  two rows held one token — every notification for A continued to be pushed to a device A had given up. With
  nobody signing in afterwards, indefinitely. Compounds T-002: the content pushed to that device was, until
  ADR-060, the full unredacted body.
- **Mitigation status:** Mitigated — **ADR-061**. Enforced on both writes: `UpsertAsync` evicts the token from
  every other account, and `DELETE /device-token` releases it on sign-out. Neither alone suffices — eviction
  only fires if somebody else signs in, and sign-out is a path a user can skip by quitting or handing the phone
  over.

### Still open

- **T-001** — unverified here; unchanged by F-028.
- **T-003** — accepted with a test condition; unchanged by F-028.
- **T-NL-1** (no rate limit on `POST /device-token`) — still noted, not prioritized. `DELETE /device-token` is
  added on the same terms: authenticated, own-account only, no cross-account impact.
- ⚠️ **None of the above is verified on a physical iOS device** (`agenda-buddy-lq5`). An iOS simulator cannot
  obtain a real FCM token, so no push arrives on one at all.

---

## Revision History

| Date | Author | Change |
|---|---|---|
| 2026-07-31 | Phantom (initial draft) | Created at Step 10.5 — Full party (3/3 triage gates) |
