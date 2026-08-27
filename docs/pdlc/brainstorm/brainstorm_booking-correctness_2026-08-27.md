---
feature: booking-correctness
date: 2026-08-27
status: inception-complete
last-updated: 2026-08-27T00:00:00Z
approved-by: full-autonomy grant (STATE.md 2026-08-26T23:12:00Z), executed autonomously per user instruction 2026-08-27
approved-date: 2026-08-27
prd: docs/pdlc/prds/PRD_F-025_booking-correctness_2026-08-27.md
---

# Brainstorm Log: booking-correctness

**Note on process.** This Inception ran under the project's standing full-autonomy grant (see
`STATE.md` 2026-08-26T23:12:00Z), reaffirmed for this specific feature by the user ("be fully
autonomous"). No live Socratic Q&A happened — the questions below were answered from `INTENT.md`,
`CONSTITUTION.md`, the F-025 feature record, and a direct read of the current code
(`BookingService.cs`, `AppointmentEntity.cs`, `BookingAppointmentCommandHandler.cs`), and the resulting
judgment calls are logged to `STATE.md`'s Guardrail Log rather than asked.

## Divergent Ideation
_Not run — the feature record already named the problem and three candidate designs for its hardest
part (the overlap check); nothing here was ambiguous enough to need option generation._

## Discovery Summary

- **Problem:** `BookAppointmentAsync` is a bare `InsertAsync` with zero domain-invariant checks.
  Confirmed directly against `AgendaBuddy.Library/Services/BookingService.cs` and
  `AgendaBuddy.Library/Entities/AppointmentEntity.cs` (no `[Range]`/custom validation on `Start`/`End`).
- **Users:** providers and customers booking through `POST /api/v1/booking/appointments` — the mobile
  app's only creation route for appointments, and any direct API caller.
- **Success:** the three invariants named in the feature record (`Start < End`, future-dated, no
  overlap) are enforced end-to-end, provable against a real database, with the adjacency edge case
  (back-to-back appointments) explicitly *not* rejected.
- **Constraints:** no roadmap dependency on F-014 (already shipped as part of `api-refactor-rollout`,
  F-020); MongoDB.Driver pinned at 2.25.0; no new NuGet package without discussion (CONSTITUTION §9).

## Adversarial Review

- **"Why not fix the race with a transaction?"** MongoDB transactions need a replica set; this
  project's local/Aspire MongoDB topology wasn't verified to run as one, and introducing that
  requirement for one call site is a bigger blast radius than the bug being fixed. Rejected for this
  pass — see Architecture §3.
- **"Doesn't this silently also block the Cancelled/soft-delete half of the feature record?"** Yes,
  deliberately — descoped to `agenda-buddy-m6m`, not silently dropped. See Architecture §4.
- **"Could the future-dating check reject a legitimate same-minute booking?"** `Start <= now` is
  rejected, `Start > now` is accepted — a booking for one second from now passes. Considered requiring
  a minimum lead time (e.g. 15 minutes) but the feature record and `INTENT.md` don't ask for one; adding
  an unrequested business rule here would be scope creep, not correctness.

## Design Discovery

Covered directly in `docs/pdlc/design/booking-correctness/ARCHITECTURE.md` — boundary placement (§1),
the concurrency decision (§3), and the explicit non-goal (§4).

## Threat Modeling Triage

**Skip.** No new attack surface: the new checks are stricter, not looser, than today's (zero checks),
and none of them touch authentication, authorization, or data exposure. `OwnershipGuard.AssertOwnerAny`
still runs before dispatch, unchanged.
