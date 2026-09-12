# Episode 020: PDLC Documentation Reconciliation

**Date:** 2026-09-12
**Version:** `v0.22.2`
**Status:** Draft
**Bead:** `agenda-buddy-djb`
**PR:** #166

## Intent

Restore the PDLC living documents as trustworthy entry points after their summaries drifted behind the shipped
system. Preserve historical delivery records while making current architecture, scope, tests, and release state
unambiguous.

## Changes

- Updated the constitution for .NET 10, Aspire orchestration, the seven services plus Gateway, six Clean
  Architecture service families, service-owned MediatR handlers, and broker-free messaging.
- Updated product intent for the AgendaMe mobile experience, shipped payments, notes, messaging, authentication,
  and current external activation constraints.
- Added a current `v0.22.2` baseline to the overview and replaced its obsolete architecture summary.
- Completed the overview episode table through episode 020 and documented standalone releases
  `v0.21.0`–`v0.22.1`.
- Marked Beads as authoritative for live backlog and claims in roadmap and state.
- Backfilled missing changelog entries from the annotated `v0.21.0`–`v0.22.1` tags.

## Verification

- `git diff --check`
- Confirmed every newly linked episode file exists.
- Confirmed the changelog contains one ordered heading for each release from `v0.20.0` through `v0.22.2`.
- Confirmed retired current-state claims about Kafka, missing authentication, and unshipped payment/notes no
  longer appear in the living summaries.
- Documentation-only change; no product build or test suite is required before PR CI.

## Ship

Pending PR creation, green required checks, merge, annotated tag publication, and GitHub Release verification.
