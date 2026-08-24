# UX Review — api-gateway-and-mobile-contract

**Triage:** Lite
**Date:** 2026-08-23
**Lead:** Muse (UX Designer)

---

## Triage

| Question | Answer |
|---|---|
| Does this feature add or modify any user-facing UI surface? | **yes** — hides an existing control (customer "mark complete"), changes copy on the report/notifications/payment screens, and makes the existing error banner and empty-state UI reachable for the first time |
| Does this feature introduce a new flow, page, or significant interaction pattern? | **no** — no new multi-step flow, no new screen, no new component; this feature wires existing screens to real data |
| Does this feature touch first-experience pathways? | **no** |

**Triage 1/3 → Lite.** No party convened; no heuristic scorecard or cognitive-load assessment at this depth
— the surface change is small and localized, not a new flow to assess holistically.

---

## Anti-Patterns Found

None. This feature introduces no new visual surface — no new screen, modal, or layout — so the catalog's
anti-pattern refuse list (gradient heroes, glassmorphism, hero-metric templates, etc.) has nothing new to
apply to.

---

## UX Writing Findings

1. **P2 — Revenue-unavailable and non-charging-payment copy are placeholders, not final.** The PRD (Non-
   Functional / Requirement 12) states the *functional* requirement — render a reason, never a number or
   blank; never imply a `local_`-prefixed payment was charged — but leaves exact wording to Design. Finalizing
   it here, rather than deferring further:
   - **Report screen, `revenueAvailable: false`:** *"Revenue isn't available yet — [revenueUnavailableReason]."*
     Follows the empty-state formula (acknowledgement + reason) rather than a bare error tone, since this is
     an expected state, not a failure.
   - **Payment screen, `local_`-prefixed intent:** *"Payment recorded (not yet charged)"* — never "Paid." Names
     the actual state rather than implying settlement.
   - **Notifications, empty list:** *"No notifications yet — you'll see updates about your appointments
     here."* Acknowledgement + value prop; no action pathway needed since there is nothing to do from empty.
   - **Proposed action:** Fix now — these three strings are cheap, load-bearing, and already have a
     functional requirement; there's no benefit to deferring the exact wording past this gate.
2. **P3 — The gateway's named-service error (`api-contracts.md` §1, `failedService`) needs to reach the user
   through the existing error banner's copy, not a generic message.** Draft: *"[Service] is unavailable
   right now. Try again."* with `[Service]` mapped from `failedService` to a human name (`booking` →
   "Booking", not the raw cluster id). Follows the error-copy formula (what happened → implicit why → how to
   fix, via the retry action). **Proposed action:** Fix now — this is the one place PRD AC 5 has a
   user-facing consequence, so the mapping table (cluster id → display name) belongs in this feature, not a
   follow-up.

---

## 8-State Spot-Check

Two interactive elements are directly affected by this feature; the rest are unchanged.

| Element | Default | Hover | Focus | Active | Disabled | Loading | Error | Success |
|---|---|---|---|---|---|---|---|---|
| "Mark complete" (provider view) | ✅ existing | ✅ existing | ✅ existing | ✅ existing | ✅ existing (non-Booked appointment) | ⚠️ **needs a state** — the button must show a busy indicator while the `POST .../status` call is in flight, not just re-enable on completion | ✅ existing pattern (retry) | ✅ existing (transitions to Completed) |
| "Mark complete" (customer view) | ❌ **must not render at all** — see finding below | n/a | n/a | n/a | n/a | n/a | n/a | n/a |

**Finding — P2:** the customer-facing control must be **hidden entirely**, not rendered disabled. A disabled
button with no explanation is worse than no button — it invites "why can't I do this?" with no answer, where
hiding it correctly communicates "this isn't something you do." Matches PRD Requirement 6's wording ("MUST
NOT offer a 'mark complete' action") — recorded here because the PRD's phrasing could be misread as
"disabled" by an implementer. **Proposed action:** Fix now — a one-line role check at render time, cheap to
get right and easy to get wrong.

**Finding — P3:** the provider-view "mark complete" button needs an explicit loading state for the new
`POST .../status` call (PRD AC 7) — the legacy `PUT`-based call this replaces had no equivalent busy
indicator documented, so this is a genuinely new state to design, not an existing one to preserve.
**Proposed action:** Fix now — small, and the retry/error states it composes with already exist.

---

## Findings Summary

| # | Finding | Severity | Proposed Action |
|---|---|---|---|
| 1 | Revenue/payment/empty-notifications copy finalized | P2 | Fix now |
| 2 | Gateway failed-service error mapped to display name in the banner | P3 | Fix now |
| 3 | Customer "mark complete" hidden, not disabled | P2 | Fix now |
| 4 | Provider "mark complete" needs a loading state for the new status call | P3 | Fix now |

All four are tagged **fix now** — none require a redesign, an ADR, or an accept-as-tradeoff. All four should
land as Plan-phase tasks or task-level detail on the existing status/report/notifications/payment tasks.

---

## Open Questions for Human

None — all four findings are small, concrete, and already have a proposed fix. No product- or
org-specific context is needed to resolve them.

---

## Variant Convergence

**Skipped.** The trigger gate runs unconditionally but Step 10.6 completed with **Lite** triage — the gate
fires only when 10.6 ran Full and at least one of four trigger signals hits. Lite and Skip outcomes always
skip Variant Convergence. No visual exploration needed; all four findings above are copy/state-level fixes
with an obvious single implementation, not a layout choice with genuine alternatives to compare.

---

## As-Built Audit

*(Populated during Construction Review, Wave 3.)*

## Ship Verify

*(Populated during Ship Verify, Wave 4.)*
