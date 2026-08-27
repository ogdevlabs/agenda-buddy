# Episode 010: Booking Correctness

**Episode ID:** 010
**Feature name:** Booking Correctness — an appointment can no longer be booked backwards, in the past, or on top of another appointment for the same provider
**Feature slug:** booking-correctness
**Feature ID:** F-025
**Date built:** 2026-08-27, on `feat/F-025-booking-correctness` — PR [#72](https://github.com/ogdevlabs/agenda-buddy/pull/72), all 15 CI checks green (mobile jobs correctly skipped, no `AgendaBuddy.MobileApp` path touched)
**Phase delivered in:** Construction
**Date shipped:** 2026-08-27 — merged via the GitHub REST API `PUT .../pulls/72/merge` (`75f5505`), tagged **`v0.10.0`**, PR #72. First feature shipped through a real PR since the `gh`-failure-bypass pattern (F-017–F-020) was corrected the same day (ADR-050)
**Status:** Final

---

## What Was Built

`BookingService.BookAppointmentAsync` was a bare `InsertAsync` — no check that `Start < End`, none that
`Start` is in the future, none that a new appointment doesn't overlap one already booked for the same
provider. `AppointmentEntity` carried no validation attribute on either field. `INTENT.md`'s launch
criteria name "no booking corruption bugs" alongside "no data loss" — F-021 closed the data-loss half
(the account-destroying refresh); this closes the corruption half.

Three changes close the three named gaps:

1. **`Start < End`** — a `.Rule(...)` added to `AppointmentEntitySpecification`, the existing Validot
   spec already wired to `POST /appointments` (one of Booking's 3 Validot-migrated routes).
2. **Future-dating** — `BookingAppointmentCommandHandler` now takes `IDateTimeProvider` (an
   already-existing abstraction, previously only used by Identity) and rejects `Start <= now`.
3. **No overlap** — `BookingService.FindOverlappingAppointmentsAsync` queries `appointments` for the
   same provider with a standard half-open interval-overlap filter; the handler fails the command if
   anything comes back. An appointment immediately adjacent to an existing one (new `Start` == existing
   `End`) is correctly not treated as an overlap.

**The concurrency question the feature record raised was answered, not deferred.** Three candidate
designs were named for the overlap check: an atomic conditional write, a unique slot-key index, or an
accepted, documented race. Shipped the third (ADR-051) — the first needs an aggregation-pipeline update
unverified against the pinned `MongoDB.Driver` 2.25.0, and appointments have arbitrary ranges, not
slot-aligned ones, so a slot key would force an unrelated granularity decision. The race window this
accepts is real and narrow: two concurrent bookings for the *same provider* in an overlapping window
could both pass the check. It does not reopen the already-fixed race in the provider's embedded
appointment list (`AppendAppointmentAsync`'s atomic `$push`, ADR D-9).

**Also scoped, and deliberately not built here.** The feature record also named the dead
`AppointmentStatus.Cancelled` — cancellation hard-deletes instead of transitioning. Descoped to its own
record (`agenda-buddy-m6m`) at Design: it needs a decision touching the provider's embedded list,
`ReportingService`'s counts, and F-024's future erasure work, not a wiring fix.

Suites: backend 550/550 (547 baseline + 3 new), integration 314/314 (310 baseline + 4 new against a real
MongoDB container), 0 failures, 0 regressions. `dotnet format --verify-no-changes` clean.

---

## Links

| Artifact | Path |
|---|---|
| PRD | [`PRD_F-025_booking-correctness_2026-08-27.md`](../prds/PRD_F-025_booking-correctness_2026-08-27.md) |
| Brainstorm | [`brainstorm_booking-correctness_2026-08-27.md`](../brainstorm/brainstorm_booking-correctness_2026-08-27.md) |
| Design | [`docs/pdlc/design/booking-correctness/ARCHITECTURE.md`](../design/booking-correctness/ARCHITECTURE.md) |
| Tasks | [`docs/pdlc/tasks/F-025/`](../tasks/F-025/) — T01…T04 |
| Decisions | ADR-051 |
| Follow-on | `agenda-buddy-m6m` — `AppointmentStatus.Cancelled` soft-delete decision |

---

## Key Decisions & Rationale

**ADR-051 — accepted race, not an atomic conditional write or slot-key index.** See Design §3 and the
ADR itself. The short version: the two rejected alternatives each cost more than this fix's actual
problem (zero checks existing at all) justified, and the accepted race is documented rather than
silently present.

**Process note.** This episode ran under the project's standing full-autonomy grant, reaffirmed by the
user for this feature specifically ("be fully autonomous"). No live Socratic Q&A happened; Discover/
Define/Design questions were answered directly from `INTENT.md`, `CONSTITUTION.md`, the feature record,
and the current code, with judgment calls logged to `STATE.md`'s Guardrail Log rather than asked. Cloud
deploy was not attempted — the Atlas credential rotation (`agenda-buddy-41s`, P0) remains the standing
human-only prerequisite (ADR-035's deferral continues), and this feature has no cloud-deploy-relevant
change anyway (no schema change, no new external dependency).

---

## What This Episode Does Not Claim

- Does not make the overlap check safe under concurrent writers for the same provider — see ADR-051.
- Does not touch `AppointmentStatus.Cancelled` or cancellation semantics — filed as `agenda-buddy-m6m`.
- Does not add a minimum lead time before a booking's `Start` (e.g. "must be booked at least 15 minutes
  ahead") — not requested by the feature record or `INTENT.md`, and adding it here would be scope creep.
- Does not change the response envelope, route, or authorization model for `POST /appointments`.
