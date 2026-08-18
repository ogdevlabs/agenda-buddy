# UX Review — secure-public-endpoints (F-016)
<!-- pdlc-template-version: 1.0.0 -->

**Triage:** Skipped
**Date:** 2026-08-18
**Lead:** Muse (UX Designer)

---

## Triage Record

| Question | Answer | Evidence |
|---|---|---|
| Does this feature add or modify any user-facing UI surface? | **no** | No screen, modal, dashboard section, form, navigation element, or user-visible email/SMS template. The change set is HTTP authorization attributes, a response projection, pagination parameters, and an exception handler (`ARCHITECTURE.md` §1). `MobileApp` is not touched. |
| Does this feature introduce a new flow, page, or significant interaction pattern? | **no** | No multi-step flow, gesture, keyboard surface, or async-state design. The API contract changes have **no consumer** — the mobile client cannot reach any of these routes today (`01-api-surface.md:158`). |
| Does this feature touch first-experience pathways? | **no** | No onboarding, signup, install, or first-run empty state is modified. See the note below on *downstream* effect, which is not the same as touching it. |

**Triage outcome: Skip (0/3).**

**Rationale:** No user-facing UI surface — design-laws audit skipped per Muse's triage.

---

## Downstream UX consequence — recorded, not audited

A bare skip record would hide something a future reader needs. Muse's note:

> **Muse (UX Designer):** "F-016 has no UI, so there is nothing for me to score against the heuristics. But it **changes a precondition of a shipped user flow**, and that consequence lands on F-015, not here.
>
> Requirement 9 authenticates `GET /api/v1/providers`. `ROADMAP.md` F-003 `customer-onboarding-flow` — status **Shipped** — defines that flow as *'a customer signs up, discovers providers, and subscribes to one.'* Discovery is already post-signup by the product's own definition, so this is not a regression, and the maintainer confirmed it explicitly at the PRD gate. Good.
>
> What it does mean: **whoever builds the provider-browse screen in F-015 must design it as an authenticated screen** — with a real empty state, a real 401-expiry path, and a real 403 path, because `JwtDelegatingHandler` purges the token on 401 and bounces the user to login (`13-security.md:186`). A 60-minute token expiry mid-browse currently means a hard logout, since the refresh flow is stored but never called. That is a genuine UX cliff, and it is **F-015's to design** — I am flagging it so it arrives as a known requirement rather than a surprise.
>
> Second thing for F-015: requirement 10 means a browsing customer receives `ProviderSummary`, not the full record. The browse screen can show name, profession and the service catalogue with fees — and **cannot** show availability, because appointments are projected out. If the intended design was 'browse providers with their open slots', that needs the Calendar route, which is now ownership-guarded and will return 403 to a customer. **That is a real product gap worth naming before F-015 designs around an assumption.**"

Neither point is a finding against *this* design. Both are inputs to F-015 and are recorded here because this is where they were discovered.

---

## Findings

None. No UI surface to audit.

---

## Re-triage trigger

Re-run this audit if any of the three triage answers becomes "yes" during Construction — for example if scope expands to include a user-visible error page, an email template, or any change inside `MobileApp`.

---

## Revision History

| Date | Author | Change |
|---|---|---|
| 2026-08-18 | Muse (triage) | Created at Step 10.6. Triage Skip (0/3). Two downstream UX consequences recorded as inputs to F-015. |
