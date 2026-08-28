# Retention & Data-Subject Rights — Current State (F-024)

**Status:** synthetic/development data only. No real customer, provider, or appointment record in this
cluster belongs to an actual person as of this writing (confirmed by the maintainer, 2026-08-18). There is
no live GDPR clock and no DPA in force. This document states the policy that is in place now, so it exists
*before* real data ever lands — not written retroactively under incident pressure.

## What "delete" does today

| Action | What happens |
|---|---|
| Cancel an appointment | Removed from the `appointments` collection **and** from the owning provider's embedded `AppointmentEntities` copy. Both live-data copies are gone immediately. |
| Delete a session note | Removed from the `notes` collection via `DeleteAppointmentNoteCommandHandler`. Not automatic on appointment cancellation — a provider's own clinical note is not treated as owned by the appointment's lifecycle (a deliberate non-goal, see the F-024 PRD). |
| Deactivate a provider | Soft delete only (`IsActive = false`). The record and its history remain — deactivation is a status change, not erasure. |

## What survives, and for how long

Every command handler writes an audit event to the `events` collection containing the full entity it acted
on (`CONSTITUTION.md` §3's audit mandate — this is deliberate, not a bug: that record is the actual audit
content for a write). So a deleted appointment's data still exists inside its own audit record, and inside
any audit record referencing it, until that record expires.

**Retention window:** 400 days by default (`EventStore:RetentionDays` in configuration), enforced by a
TTL index on `Event.TimeStamp`. After 400 days, the record — and any entity data it carried — is deleted
automatically by MongoDB's own background reaper. No manual purge job exists or is needed.

**Why a bounded window instead of per-record redaction:** see `DECISIONS.md` ADR-056. An audit trail that
can selectively edit or delete individual entries on request is indistinguishable from a tampered one.
Deleting the whole record on a schedule known in advance preserves the trail's trustworthiness while still
bounding how long personal data can survive inside it.

## What is not built yet (deliberately, see below)

- **A self-service "export all my data" or "delete my entire account" API.** Filed as `agenda-buddy-ge2`.
- **Field-level encryption for `NoteEntity.Content`** (session notes — the most sensitive data in the
  product). Evaluated, not implemented — see ADR-057. Filed as `agenda-buddy-vba`.
- **A committed DPA or formal privacy policy document.** No legal review has happened; this document is an
  engineering statement of current behavior, not a substitute for one.

## When this becomes urgent

The moment this cluster holds a real person's data, this document's "synthetic data only" premise is false
and every item in the section above becomes a live obligation, not backlog work. Re-read this document at
that point — it names exactly what still needs to exist.
