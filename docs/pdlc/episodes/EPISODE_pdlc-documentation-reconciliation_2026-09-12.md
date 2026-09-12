# Episode 020: PDLC Documentation Reconciliation

**Date:** 2026-09-12
**Version:** `v0.22.2`
**Status:** Final
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
- Fixed `FutureSlot` so booking integration fixtures honor the default Monday–Friday working week; added a
  deterministic regression test for Friday, Saturday, Sunday, and Monday candidates.

## Verification

- `git diff --check`
- Confirmed every newly linked episode file exists.
- Confirmed the changelog contains one ordered heading for each release from `v0.20.0` through `v0.22.2`.
- Confirmed retired current-state claims about Kafka, missing authentication, and unshipped payment/notes no
  longer appear in the living summaries.
- `dotnet format agenda-buddy-backend.slnf --verify-no-changes --no-restore`
- Backend: 1,161 passed, 0 failed.
- Integration: 410 passed, 0 failed. The first run exposed the weekend fixture defect (404 passed, 2 failed);
  the focused regression slice then passed 7/7 before the full rerun.
- Mobile: 916 passed, 7 intentionally skipped, 0 failed.

## Ship

- PR #166 passed required GitHub Actions run `34710347353`: changes, backend build/test, dependency audit,
  integration, and summary succeeded; path-inapplicable jobs skipped.
- PR #166 merged through the GitHub REST API as `fec0a954d8cbb5853aba84fb10020d6b3dfa8201`.
- The user explicitly approved Ship. The annotated `v0.22.2` tag and matching stable GitHub Release are created
  from the ship-bookkeeping merge and independently verified before this operation is reported complete.
