# PRD: F-024 — data-subject-rights

**Feature ID:** F-024
**Date:** 2026-08-27
**Status:** Approved (self-approved under this session's standing full-autonomy grant)

## Problem

Filed 2026-08-18 as: no export/deletion/anonymisation capability exists, deletion is unsatisfiable because
an appointment persists in three places (`appointments`, embedded in `ProviderEntity`, and again in the
`events` audit blobs), the `events` collection has no retention/pruning/index/actor field, and
`NoteEntity.Content` (private session notes) has no field-level encryption. Deferred at filing time: the
cluster holds synthetic data only, so there is no live GDPR clock — this is a hard prerequisite for
production launch, not an active incident.

**Re-verified against current code before Design (six features have shipped since this record was
written):**

- The 3-copy deletion problem is **already fixed**. `CancelAppointmentCommandHandler.SearchAndCancelAppointment`
  already removes the embedded copy from `ProviderEntity.AppointmentEntities` (via `UpdateProviderAsync`)
  in addition to `BookingService.CancelAppointmentAsync`'s delete from the `appointments` collection. No
  code change needed for this half of the original problem.
- The query-audit PII amplification is **already fixed** (F-016 / `QueryAudit.Success`/`Failure`): query
  handlers write only a result count, never the dataset. `GetProvidersQueryHandler` no longer serialises
  every provider.
- `Event.Actor` **already exists** (ADR-027, added since this record was filed).
- **Still true:** the `events` collection has no index and no retention/pruning. Command handlers
  (11 of them, by design — see `QueryAudit`'s remarks) still write the full entity they acted on into
  their audit event's `Data` field, and that record survives forever. This is the actual remaining "third
  copy" an erasure request cannot reach.
- **Still true:** `NoteEntity.Content` has no field-level encryption.
- **Still true:** no DPA, retention schedule, or privacy policy is committed anywhere.

## Goals

1. Bound how long any audit record — and the entity data it carries — survives, closing the one remaining
   gap in "does erasure actually work."
2. Document the retention policy and the deliberate scope boundary of this feature.
3. Evaluate field-level encryption for `NoteEntity.Content` and either implement a low-risk improvement or
   explicitly descope it with reasoning — not silently skip it.

## Non-goals

- **A cross-service self-service "export/delete my entire account" API.** That needs its own design pass
  across Customer, Booking, Provider, and Notes (which records to touch, ownership checks, whether a
  provider's own clinical notes about a now-deleted customer should be deleted too — a product decision,
  not a mechanical one). Filed as `agenda-buddy-<id>` for its own feature.
- **Redacting or editing individual historical audit records on request.** A selectively-edited audit
  trail is indistinguishable from a tampered one. Retention (bounded expiry of the whole trail) is the
  chosen mechanism instead — see ADR below.
- **Cascading note deletion when an appointment is cancelled.** A provider's session note is their own
  clinical record about the appointment, not data whose lifecycle is owned by the appointment — deleting
  it automatically on cancellation would be a product decision this feature does not make. Explicit note
  deletion already exists (`DeleteAppointmentNoteCommandHandler`).
- **CSFLE / MongoDB Queryable Encryption.** Evaluated at Design; needs infrastructure (KMS, encrypted-field
  schema map) this project doesn't have today and is a genuine new-infrastructure decision, not a low-risk
  addition — descoped, see ADR below.

## Requirements

1. `IEventStore.EnsureIndexAsync()` creates a TTL index on `Event.TimeStamp` (retention window
   configurable via `EventStore:RetentionDays`, default 400 days) and a secondary index on `Event.Type`.
2. Every service that registers `IEventStore` (6 of the 7 — Identity deliberately does not, it has no
   CQRS handlers to audit) calls `EnsureIndexAsync()` once at startup, fire-and-forget, mirroring F-023's
   `MongoTokenRevocationStore` pattern exactly (same rationale: don't stall Kestrel on Mongo's ~30s
   server-selection timeout).
3. A committed retention/DPA doc (`docs/pdlc/design/data-subject-rights/RETENTION.md`) states the policy,
   what's synthetic-data-only today, and the explicit non-goals above.
4. Field-level encryption decision recorded as an ADR, either implemented (if low-risk) or descoped to a
   filed issue with reasoning.

## Acceptance Criteria

- AC-1: `EnsureIndexAsync()` creates a TTL index on `timestamp` with the configured (or default 400-day)
  expiry, verified against a real MongoDB container.
- AC-2: `EnsureIndexAsync()` creates a non-TTL index on `type`.
- AC-3: `EnsureIndexAsync()` is idempotent — calling it twice does not throw.
- AC-4: Every one of the 6 EventStore-registering services calls `EnsureIndexAsync()` at startup without
  blocking Kestrel's own readiness.
- AC-5: A retention/DPA doc exists and is committed.
- AC-6: Field-level encryption is either implemented or descoped with a recorded, reasoned ADR — not
  silently absent from the final report.
