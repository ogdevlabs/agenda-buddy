# Episode 021: Appointments Segmented Experience

**Date:** 2026-09-12
**Version:** `v0.23.0`
**Status:** Final
**Bead:** `agenda-buddy-cs1`
**PR:** #168

## Intent

Replace the role-inappropriate Calendar tab with a shared appointment history that lets providers and customers
find scheduled, completed, and cancelled sessions quickly in either supported language.

## Changes

- Renamed the visible Calendar tab/page to Appointments in English and Citas in Spanish.
- Replaced the provider day grid and customer day tiles with shared counted segments: Scheduled, Done, and
  Cancelled; Spanish uses the approved Programadas, Realizadas, and Canceladas labels.
- Kept the provider-only Manage calendar route for working hours and time off.
- Added an ownership-guarded paged Calendar endpoint using the established `PagedResponse<T>` envelope.
- Scheduled appointments are sorted earliest-first. Done and Cancelled are sorted newest-first, initially return
  five records, and expose Previous/Next page controls for additional history.
- Pending reschedules use their proposed time for grouping and list display while appointment detail retains the
  original agreed time until the proposal is answered.
- Simulator verification exposed a long-account-name BrandHeader defect: the shared auto-sized grid let account
  text shift the AgendaMe lockup and push the role off-screen. The brand and identity now occupy independent
  centered rows; the bounded identity row shows a profile name only, never an email fallback.
- Appointments and Messages now share the established operational-page treatment: `BackgroundPage`, a `Primary`
  title band, 24px inset, and 30px bold white title. Structural coverage prevents either page drifting again.

## Verification

- Backend: 1,164 passed, 0 failed.
- Integration: 413 passed, 0 failed, including a provider with 23 completed appointments retrieved across five
  pages without duplicates or gaps, authenticated page-two retrieval, and OpenAPI drift.
- Mobile: 937 passed, 7 intentional live-Identity skips, 0 failed.
- Android `net10.0-android` build passed.
- iOS simulator `net10.0-ios` build passed.
- `dotnet format agenda-buddy-backend.slnf --verify-no-changes --no-restore` passed.
- iPhone 17 Pro simulator: disposable verified customer exercised Programadas, Realizadas, and Canceladas against
  the live local Gateway; all labels/counts fit, selected states and empty states rendered without overlap.

## Ship

- PR #168 passed required GitHub Actions run `34717208994`: backend, integration, mobile unit, Android Release,
  iOS Release, security, all seven SDK-container/Trivy jobs, and summary succeeded.
- PR #168 merged through the GitHub REST API as `62c6b1f1356b6e3beba2d5d90bddc8ceed7498ee`.
- The user explicitly approved Ship. The annotated `v0.23.0` tag and matching stable GitHub Release are created
  from the ship-bookkeeping merge and independently verified before this operation is reported complete.