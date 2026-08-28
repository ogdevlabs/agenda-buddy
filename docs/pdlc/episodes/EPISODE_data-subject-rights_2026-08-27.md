# Episode 015: Data Subject Rights

**Episode ID:** 015
**Feature name:** Data Subject Rights — bounded retention closes the audit-trail erasure gap
**Feature slug:** data-subject-rights
**Feature ID:** F-024
**Date built:** 2026-08-27, on `feat/F-024-data-subject-rights`
**Phase delivered in:** Construction
**Date shipped:** 2026-08-27 — merged via the mandated PR path (ADR-050), PR #85, tagged **`v0.15.0`**
**Status:** Final

---

## What Was Built

Filed 2026-08-18 as three problems: a 3-copy deletion gap, an unindexed/unbounded audit trail, and
unencrypted session notes. Re-verified against current code before Design (six features shipped since
filing) — **two of the three were already fixed** by earlier work:

- The appointment 2-copy deletion (live `appointments` collection + `ProviderEntity`'s embedded copy) is
  already cleaned up together by `CancelAppointmentCommandHandler`. No code change needed.
- The query-audit PII amplification (`GetProvidersQueryHandler` serializing every provider on every read)
  was already fixed by F-016's `QueryAudit`. No code change needed.
- `Event.Actor` already exists (ADR-027).

**What was actually still broken:** the `events` collection has no index and no retention, so the 11
command handlers' full-entity audit payloads (by design — that's their real audit content) survive
forever. That's the one true surviving "third copy" an erasure request can't reach.

**Fix.** `IEventStore.EnsureIndexAsync()` creates a TTL index on `Event.TimeStamp`
(`EventStore:RetentionDays`, default 400 days) plus a secondary index on `Event.Type`. Wired into all 6
services that register `IEventStore` (Identity deliberately excluded — it has no CQRS handlers to audit),
fire-and-forget at startup, mirroring F-023's `MongoTokenRevocationStore` pattern exactly.

**ADR-056**: bounded retention was chosen over per-record redaction — a mechanism that can selectively
edit or delete individual audit entries on request is indistinguishable from a tampered trail; deleting
the whole record on a known schedule preserves trustworthiness while still bounding exposure.

**ADR-057**: field-level encryption for `NoteEntity.Content` was evaluated, not implemented. Both real
candidates (CSFLE/Queryable Encryption — needs KMS/schema-map infra this project doesn't have; app-layer
AES-GCM — needs a real key-rotation story) are genuine design decisions, not low-risk additions inside a
one-shot autonomous run. Filed as `agenda-buddy-vba`.

**Descoped**: a cross-service self-service export/erasure API — needs its own design pass across
Customer/Booking/Provider/Notes. Filed as `agenda-buddy-ge2`.

**New**: `docs/pdlc/design/data-subject-rights/RETENTION.md` — the retention policy, current deletion
behavior, and explicit non-goals, committed now rather than written retroactively under incident
pressure.

**Tests:** backend suite 571/571 (baseline unchanged), integration suite 329/329 (327 baseline + 2 new —
`EventStoreRetentionIndexTest`, verifying the TTL/type indexes against a real MongoDB container and
idempotency of `EnsureIndexAsync`). `dotnet format --verify-no-changes` clean.

---

## Links

| Artifact | Path |
|---|---|
| PRD | [`PRD_F-024_data-subject-rights_2026-08-27.md`](../prds/PRD_F-024_data-subject-rights_2026-08-27.md) |
| Retention policy | [`docs/pdlc/design/data-subject-rights/RETENTION.md`](../design/data-subject-rights/RETENTION.md) |
| Feature record | [`docs/pdlc/tasks/F-024/`](../tasks/F-024/) |
| Decisions | ADR-056, ADR-057 |
| Follow-ups filed | `agenda-buddy-ge2` (export/erasure API), `agenda-buddy-vba` (field-level encryption) |
