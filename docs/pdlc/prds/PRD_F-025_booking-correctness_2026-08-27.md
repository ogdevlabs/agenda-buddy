---
feature: F-025
slug: booking-correctness
status: approved
approved-by: ogdevlabs (full-autonomy grant, see STATE.md 2026-08-26T23:12:00Z)
approved-date: 2026-08-27
---

# PRD: Booking Correctness (F-025)

## Problem

`BookingService.BookAppointmentAsync` is a bare `InsertAsync`. `AppointmentEntity` carries no
validation attribute on `Start` or `End`, and nothing checks a new appointment against a provider's
existing ones before writing it. Concretely, today's API accepts:

1. An appointment with `End` before or equal to `Start`.
2. An appointment dated in the past.
3. An appointment that overlaps another appointment already booked for the same provider.

`INTENT.md`'s launch criteria name "no booking corruption bugs" alongside "no data loss" — F-021 closed
the data-loss half (the account-destroying refresh-token bug); this closes the corruption half.

## Users affected

Providers and customers booking through `POST /api/v1/booking/appointments` (both the mobile app and
any direct API caller) — silently, since nothing today tells them the appointment they just booked is
nonsensical or double-booked until a human notices the calendar.

## Requirements

- **R1.** `POST /appointments` rejects `End <= Start` with a 400 and a validation message naming the
  problem.
- **R2.** `POST /appointments` rejects a `Start` that is not strictly in the future (relative to
  request time) with a 400.
- **R3.** `POST /appointments` rejects an appointment whose `[Start, End)` range overlaps any existing
  appointment for the same `EmailProvider`, with a 400 and a message identifying the conflict as an
  overlap (distinct from R1/R2's messages).
- **R4.** An appointment immediately adjacent to an existing one (new `Start` == existing `End`, or new
  `End` == existing `Start`) is **not** an overlap and succeeds.
- **R5.** None of the above weakens the existing `[EmailAddress]`/Validot checks on `EmailProvider`/
  `EmailCustomer`, and the existing "no provider found" failure path is unchanged.

## Explicitly out of scope

- **Soft-delete / `AppointmentStatus.Cancelled`.** The feature record raised this as "also in scope,"
  but it's a materially different piece of work — it needs a decision on what cancellation means for
  the provider's embedded appointment copy, `ReportingService`'s counts, and F-024's future erasure
  work, not a wiring fix. Descoped to its own record (`agenda-buddy-m6m`) at Design, autonomously,
  rather than blocking this fix on an unrelated product question. See
  `docs/pdlc/design/booking-correctness/ARCHITECTURE.md` §4.
- **Distributed/concurrent-safe overlap prevention.** See Architecture §3 — a documented, accepted race
  window, not a gap this PRD claims to close.

## Success metric

Zero of R1–R4 reproducible against `main` post-merge (proven by
`AgendaBuddy.IntegrationTests/Persistence/BookingCorrectnessTest.cs`, run against a real MongoDB
container, not mocked).

## Non-functional constraints

- No new NuGet dependency (CONSTITUTION §9 — new packages need discussion; this needs none).
- No schema change — `AppointmentEntity`'s existing `start`/`end`/`email_provider` fields already carry
  everything the overlap query needs.
- Backend test suite and the Booking integration suite stay green; `dotnet format --verify-no-changes`
  stays clean.

## Readiness Assessment

**Completeness:** Strong — all three invariants named in the feature record are addressed, with a test
for each plus the adjacency edge case. **Traceability:** Strong — every requirement maps to a specific
test (`AppointmentEntitySpecificationTest`, `BookingCorrectnessTest`) and a specific code change (cited
in Architecture §2). **Durability:** Fair — the accepted-race decision (Architecture §3) is durable as
documented but would need revisiting if this service ever runs with more than one writer contending for
the same provider's calendar at meaningfully high frequency; flagged there, not hidden.
