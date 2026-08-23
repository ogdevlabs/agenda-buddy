---
id: F-025
title: booking-correctness
status: planned
priority: 25
labels: [roadmap, "priority:25"]
claimed_by: null
created: 2026-08-23
updated: 2026-08-23
---
Split out of F-014 at Discover 2026-08-23. BookingService.BookAppointmentAsync is a bare InsertAsync: no Start<End check, no future-dating check, and no overlap check against the provider's existing appointments. AppointmentEntity carries no validation attribute on Start or End. So an appointment can be booked backwards (End before Start), in the past, and on top of another appointment for the same provider.

INTENT.md's launch criteria name "no booking corruption bugs" alongside "no data loss" — the half F-021 closed by fixing the account-destroying refresh. This is the other half.

Separated from F-014 because it is a different shape of work. F-014 registers and routes capabilities that already exist; this needs domain invariants plus a concurrency story for the overlap check, and a read-then-insert is racy. Three candidate designs, none obviously right without measurement:
  1. an atomic conditional write (the FindOneAndUpdateAsync primitive F-021 added can express "insert only if nothing overlaps" only via an aggregation-pipeline update — worth spiking);
  2. a unique index on a derived slot key, which forces a decision about slot granularity;
  3. an explicitly accepted and documented race, which for a single-provider calendar may be honest.

No technical dependency on F-014, though both touch BookAppointmentCommandHandler, so sequencing them adjacently keeps the merge cheap.

Also in scope, found at the same Discover: AppointmentStatus.Cancelled exists in the enum and is never assigned, because cancellation hard-deletes from both the appointments collection and the provider's embedded copy. Whether cancellation should be a soft delete is a product question that belongs with the booking rules rather than with F-024's erasure work.

Tracker: agenda-buddy-ohw. Source: docs/pdlc/brainstorm/brainstorm_wire-unreached-services_2026-08-23.md §3.
