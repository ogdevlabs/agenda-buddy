# Episode 021: Appointments Segmented Experience

**Date:** 2026-09-12
**Version:** `v0.23.0`
**Status:** Draft
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
- Integration: 412 passed, 0 failed, including authenticated page-two retrieval and OpenAPI drift.
- Mobile: 936 passed, 7 intentional live-Identity skips, 0 failed.
- Android `net10.0-android` build passed.
- iOS simulator `net10.0-ios` build passed.
- `dotnet format agenda-buddy-backend.slnf --verify-no-changes --no-restore` passed.
- iPhone 17 Pro simulator: disposable verified customer exercised Programadas, Realizadas, and Canceladas against
  the live local Gateway; all labels/counts fit, selected states and empty states rendered without overlap.

## Ship

Pending PR creation, green required checks, merge, annotated tag publication, and GitHub Release verification.