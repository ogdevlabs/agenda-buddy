# PRD: Native Calendar Sync
<!-- pdlc-template-version: 2.4.0 -->

**Date:** 2026-10-01
**Status:** Approved (unattended run, user directive 2026-10-01)
**Feature slug:** native-calendar-sync
**Episode:** <!-- assigned after delivery -->

---

## Overview

AgendaMe appointments appear in the calendar app a person already uses — Apple Calendar on iOS, Google Calendar
on Android — through a private, revocable subscribed calendar feed. Once added, entries follow every later change
(confirmation, reschedule, cancellation) with no further action and without AgendaMe running.

## Problem Statement

Providers and customers keep two calendars: AgendaMe for bookings and their phone's calendar for everything else.
Nothing in AgendaMe reaches the phone's calendar today (no iCalendar, EventKit or CalendarContract code exists), so
users retype appointments, miss changes, and double-book their own time.

## Target User

- **Primary:** the independent service provider, whose working day is AgendaMe sessions plus personal commitments.
- **Secondary:** customers, who want a booked session next to the rest of their week.

## Requirements

1. The Calendar service MUST let an authenticated user enable a calendar feed (`POST /api/v1/calendar/feed`),
   returning an absolute HTTPS-or-HTTP URL under the public Gateway address, read once.
2. Enabling again MUST rotate: the previous URL MUST stop working immediately.
3. `GET /api/v1/calendar/feed` MUST report whether a feed is enabled and since when, never the URL.
4. `DELETE /api/v1/calendar/feed` MUST revoke the feed and answer `204` whether or not one existed.
5. Every management route MUST identify the owner from the JWT `sub` claim — no address in the request.
6. `GET /api/v1/calendar/feed/{token}.ics` MUST be anonymous and answer `text/calendar` (RFC 5545) for a live
   token, and an identical `404` for unknown, malformed and revoked tokens.
7. The server MUST store only a SHA-256 hash of the token.
8. The feed MUST contain the owner's appointments from 60 days ago to 365 days ahead, at most 500, excluding
   legacy `day_off` rows, each keyed by a stable UID derived from the appointment identifier.
9. Status mapping: Requested → `TENTATIVE` + "Pending" title prefix; Booked / RescheduleRequested / Completed →
   `CONFIRMED`; Cancelled → `CANCELLED` + "Cancelled" title prefix. A pending reschedule stays at the original time.
10. Event content MUST be limited to service name, counterparty display name, time, status and a fixed
    "open AgendaMe" line. It MUST NOT contain emails, phone numbers, fees, payment data, notes or block reasons.
11. Event text MUST be in the language (en / es) the user's app was using when the feed was enabled.
12. The feed token MUST be redacted from exported telemetry URL tags.
13. Account erasure MUST delete the owner's feed.
14. Enable / rotate / revoke MUST audit via `eventStore.SaveAsync`; the anonymous fetch MUST NOT (polled traffic).
15. The mobile app MUST offer a **Calendar sync** screen (both roles) reachable from Profile and from Appointments,
    with: enable; "Add to Apple Calendar" (`webcal://`); "Add to Google Calendar" (Google's `cid` subscribe link);
    "Copy link"; "Reset link" (confirmed); "Turn off" (confirmed). iOS leads with Apple, Android with Google.
16. The screen MUST say what is shared and that the calendar app decides how often it refreshes.
17. All new strings MUST exist in English and Spanish (es-MX).

## Acceptance Criteria (BDD)

- **AC-1** Given a signed-in user with no feed, when they enable sync, then they receive a URL and
  `GET /feed` reports enabled.
- **AC-2** Given a live feed URL, when it is fetched anonymously, then the response is `200 text/calendar`
  containing one `VEVENT` per in-window appointment with the mapped status.
- **AC-3** Given a feed, when the user enables again, then the old URL answers `404` and the new one `200`.
- **AC-4** Given a feed, when the user turns it off, then its URL answers `404`; a second `DELETE` answers `204`.
- **AC-5** `[security]` Unknown, malformed and revoked tokens produce byte-identical `404` responses.
- **AC-6** `[security]` The feed body contains no email address or phone number of either party.
- **AC-7** `[security]` A span whose `url.path` carries a feed token is exported with the token redacted.
- **AC-8** `[security]` Management routes answer `401` without a token.
- **AC-9** Erasing an account deletes its feed.
- **AC-10** The mobile screen enables sync, opens the platform subscribe link, copies, resets and turns off, with
  every string present in both languages, and is reachable from Profile and Appointments.

## Out of Scope

- Writing events onto the device calendar (EventKit / CalendarContract) — follow-up for Android refresh latency.
- Importing device calendar busy time into provider availability.
- Per-appointment "add to calendar" `.ics` downloads.
- Locations (no location field exists).
