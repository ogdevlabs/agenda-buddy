# UX Review — identity-hardening (F-021)
<!-- pdlc-template-version: 1.1.0 -->

**Date:** 2026-08-22 · **Lead:** Muse (UX Designer) · **Triage: Skip (0/3)**

---

## Triage Record

| Question | Answer | Evidence |
|---|---|---|
| Does this feature add or change a UI surface? | **no** | It changes middleware registration, a repository primitive, two persisted fields and log output. No view, no component, no copy rendered to a user |
| Does it change a user-facing flow? | **no** | The flows (`login`, `register`, `refresh`) are unchanged in shape. What changes is the *conditions* behind existing HTTP status codes — see [`api-contracts.md`](api-contracts.md) |
| Does it introduce user-facing copy? | **no** | `401` bodies stay empty by design (deliberately indistinguishable between wrong password, unknown account and locked account — threat T-NL-2). `429` carries a `Retry-After` header, not prose |

**Design-laws audit skipped per Muse's triage — no user-facing surface.** No Nielsen scorecard, no 8-state matrix, no cognitive-load assessment, no anti-pattern sweep. This record exists so the audit trail is complete either way.

**Consequence for Step 10.7 (Variant Convergence):** the trigger gate cannot fire — it requires a **Full** triage at 10.6. Skipped with this as its record. No variants generated, no calibration row added to `METRICS.md`.

---

## The one UX consequence worth handing forward

F-021 introduces **`429 Too Many Requests`** and a new reason for **`401`** (a locked account) on routes the mobile client calls. `MobileApp` cannot reach the backend today (F-015), so there is no client to update now — but when F-015 wires the real contract, these two states need real handling:

- **`429`** — the client must respect `Retry-After` rather than retrying immediately, or it will keep itself throttled. An auto-retrying HTTP client with no backoff turns a rate limit into a self-inflicted outage.
- **`401` on a locked account is indistinguishable from a wrong password**, deliberately (threat T-NL-2, PRD requirement 12). So the client **cannot** say "your account is locked" — it can only say something like "we couldn't sign you in; if you've tried several times, wait a few minutes and try again". That copy is honest without confirming to an attacker that a given address exists and is locked.

Flagged here rather than in `api-contracts.md` because it is a client-experience obligation, and **F-015 owns it**. Whoever picks up F-015 should read this section before writing the login error path.

---

## Revision History

| Date | Author | Change |
|---|---|---|
| 2026-08-22 | Muse (solo mode) | Created at Step 10.6. Triage 0/3 → Skip. Recorded the `429`/locked-`401` obligation for F-015 |
