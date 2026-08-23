# UX Review — wire-unreached-services (F-014)

**Date:** 2026-08-23 · **Lead:** Muse (UX) · **Tier:** Skip (0/3)

---

## Triage

| Question | Answer |
|---|---|
| Does this feature add or change a user-facing screen, flow or component? | **no** — nine HTTP routes, no UI. `MobileApp` is not touched |
| Does it change what a user sees or has to do? | **no** — no client can reach any of it until F-015 |
| Does it change an error, empty or loading state a user meets? | **no**, at the UI layer |

**Triage 0/3 → Skip.** Same outcome as F-016 and F-021, for the same reason: this is a backend feature in a
product whose only client cannot reach the backend.

---

## Client obligations carried forward to F-015

A skip is not silence. Four things this feature decided will land on whoever builds the client, and each is
a UI decision disguised as a contract decision:

1. **The report has no revenue number, on purpose.** `revenueAvailable: false` plus
   `revenueUnavailableReason`. The client must render the *reason*, not a `£0` and not a blank tile — the
   whole point of the field pair is that a plausible zero would be believed. This is the one place F-014's
   contract asks the client to show an explanation rather than a value.
2. **An empty notifications list is the normal state, not an error.** Nothing writes a notification yet
   (threat T-208, requirement 19). A client that renders "something went wrong" on an empty list will be
   wrong for the whole of F-014's life.
3. **A `Succeeded` payment is not proof of settlement.** Under the default gateway the intent id is prefixed
   `local_` and no money moved. A UI that says "Paid" on that is lying to a provider about their income.
4. **Status changes now have their own route, and `Completed` is provider-only.**
   `MobileApp/Views/AppointmentDetailPage.xaml.cs:93` currently calls
   `ExecuteStatusUpdateAsync(AppointmentStatus.Completed)` and expects a `PUT` to carry it. That will
   silently do nothing after F-014 — the field is ignored — so the client needs the new route, and the
   customer-facing UI must not offer "mark complete" at all, because it will always be refused with 403.

Item 4 is the only **breaking** one, and it breaks a code path that cannot currently execute. That is why it
is free now and expensive after F-015.

---

## Not assessed

No Nielsen heuristic walk, no cognitive-load audit, no accessibility pass — there is no interface to walk.
The UX scorecard in `METRICS.md` stays empty, as it has for all three shipped features.
