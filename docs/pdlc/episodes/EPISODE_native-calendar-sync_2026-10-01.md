# Episode 024: Native Calendar Sync

**Date:** 2026-10-01
**Version:** `v0.26.0`
**Status:** Final
**PR:** #181
**Deferred beads:** `agenda-buddy-gqm` (on-device writes), `agenda-buddy-agy` (two-way busy import)

## Intent

Let providers and customers see their AgendaMe appointments in the calendar app already on their phone — Apple
Calendar on iOS, Google Calendar on Android — without re-typing them and without granting calendar permissions.

## Changes

- ADR-073: a subscribed RFC 5545 feed per account rather than on-device EventKit/CalendarContract writes. One
  server-side projection serves both platforms and both roles; the phone's calendar refreshes it on its own.
- ADR-074: `GET /api/v1/calendar/feed/{token}.ics` is anonymous because calendar apps cannot send a JWT. The token
  is 256-bit base64url, stored only as a SHA-256 hash, answers one identical fixed-text 404 for every failure,
  and is redacted from exported spans and Gateway hosting/forwarder logs.
- Authenticated `GET/POST/DELETE /api/v1/calendar/feed` read status, issue-or-replace, and revoke; all audited.
  Feed fetches bypass MediatR so polling writes no audit rows.
- Feed content: 60 days back to 365 ahead, capped at 500 events, times, service, status and counterparty names
  only; cancelled sessions stay as `STATUS:CANCELLED` + `TRANSP:TRANSPARENT` so they disappear cleanly. English
  or Spanish, chosen at enable time.
- Account erasure deletes the feed first.
- Mobile: Phone calendar screen (`calendarSync`) with off / on-with-link / on-without-link states, native calendar
  action first, Copy, Get a new link and Turn off (both confirmed). The link lives in secure storage keyed by
  account, since the server returns it once. Entry rows on Profile and Appointments for both roles.
- Bruno requests and the Calendar OpenAPI baseline updated.

## Verification

- Backend: 1,390 passed, 0 failed.
- Integration: 460 passed, 0 failed, including 12 feed tests (anonymous serving, no emails, Spanish customer
  feed, reset revokes, identical 404s, hash-only storage, erasure revokes) and OpenAPI drift.
- Mobile: 1,337 passed, 7 intentional live-Identity skips, 0 failed.
- Android `net10.0-android` build passed.
- `dotnet format agenda-buddy-backend.slnf --verify-no-changes` passed.
- Not verified on a device: Apple/Google actually subscribing to a deployed feed URL. Google Calendar fetches
  from Google's servers, so it cannot reach a local Gateway; this needs the dev environment.

## Known limits

- Refresh latency is the calendar app's: Apple honours the 15-minute hint loosely, Google can take many hours.
- The feed is read-only; editing an event in the phone calendar does not change AgendaMe.
