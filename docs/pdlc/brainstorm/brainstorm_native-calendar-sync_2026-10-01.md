---
feature: native-calendar-sync
date: 2026-10-01
status: discover-complete
last-updated: 2026-10-01T00:00:00Z
approved-by:
approved-date:
prd:
---

# Brainstorm Log: Native Calendar Sync (F-038)

> Seamless integration of iOS and Android native calendar applications with the AgendaMe Calendar/Agenda.

## Divergent Ideation
_Skipped by user (Step 0)._

## Run mode

The user directed an unattended run (2026-10-01): "stop until work is all completed, pr, merged and pdlc ship".
Sketch-mode drafts were therefore adopted by the agent team without per-question confirmation; every such decision
is labelled **[auto]** below. The visual companion was not started (text-only).

## Socratic Discovery

Grounding (code map, 2026-10-01): no device-calendar, iCalendar, webcal or CalDAV code exists anywhere; no
`NSCalendars*` / `READ_CALENDAR` permission is declared. Appointments carry a stable GUID `identifier`, UTC
`start`/`end`, optional `service_name`, a status lifecycle (Requested, Booked, RescheduleRequested, Completed,
Cancelled — soft delete) and no location. Calendar service routes are all authenticated; `/api/v1/go` is the only
anonymous route (ADR-070). The Gateway's `calendar` row forwards `/api/v1/calendar/**`, so any new path under that
group is reachable with no allowlist change. The mobile app has en/es-MX `.resx` strings.

1. **Purpose [auto].** A provider or customer sees their AgendaMe appointments in the calendar app they already
   live in (Apple Calendar, Google Calendar) without retyping them, and the entries follow every later change —
   confirmation, reschedule, cancellation — with no further action.
2. **Integration shape [auto].** Three candidates were weighed:
   - **A — subscribed iCalendar feed (chosen).** A per-user secret URL serving RFC 5545 text. Apple Calendar
     subscribes via `webcal://`, Google Calendar via its `?cid=` subscribe link (which also reaches Android, where
     Google Calendar is the native app). Updates arrive even when AgendaMe is closed, need no OS permission, and
     the whole contract is testable on the backend.
   - **B — on-device write (EventKit / CalendarContract).** Immediate, but only updates while AgendaMe runs,
     needs calendar permissions on both OSes, and is platform code the `net10.0` test slice cannot compile.
     Deferred as a follow-up for Android refresh latency.
   - **C — two-way busy import** (device events blocking provider availability). Valuable for providers, but it
     reads private calendars and changes what customers can book. Deferred.
3. **Who [auto].** Both roles. A provider's feed lists their sessions; a customer's feed lists their bookings.
4. **Content [auto].** Minimal: service name and counterparty display name in the title, the time, a status, and a
   line telling the reader to open AgendaMe. No email, phone, fee, payment, note or block reason.
5. **Success [auto].** Turning sync on takes one tap from Profile or Appointments plus the calendar app's own
   confirm; a cancellation made in AgendaMe disappears from (or is marked cancelled in) the subscribed calendar on
   its next refresh; turning sync off or resetting the link makes the old URL answer 404 immediately.

## Progressive Thinking (Agent Team Meeting)

- **Neo:** a feed URL is a bearer credential, so store only its SHA-256 hash; the URL is shown once and kept on
  the device that created it. Re-showing on another device is a reset, which is the honest cost of not storing it.
- **Phantom:** the token lands in the request path, so it would reach telemetry through `url.path` exactly as
  emails did (T-004). `PiiRedactingProcessor` must redact it. Unknown, malformed and revoked tokens must answer
  identically (no oracle). 256-bit tokens make brute force irrelevant, so no new rate limiter.
- **Muse:** one screen, one primary action per platform (Apple on iOS, Google on Android) with the other and
  "Copy link" as secondary; the screen states what is shared and that refresh timing belongs to the calendar app.
- **Echo:** the ICS writer is pure and gets exhaustive unit tests (escaping, line folding, CRLF, UTC); the route
  gets an integration round trip including rotation and revocation.
- **Pulse:** the deployed Gateway is the only public address, so the Calendar service is told the Gateway endpoint
  by the AppHost, as Provider already is for `/go`.
- **Bolt:** the feed fetch bypasses MediatR like `/go` and media GET — a calendar app polls every 15 minutes, and an
  audit row per poll is noise. Enable/rotate/revoke are audited handlers.

## Adversarial Review

- *"Google refreshes subscribed calendars slowly (hours)."* True and outside our control; the screen says the
  calendar app decides when it refreshes, and option B is filed as the follow-up.
- *"A leaked URL exposes a schedule."* It exposes times, service names and counterparty first/last names. Mitigated
  by minimal content, reset/turn-off that revoke instantly, and hash-only storage.
- *"Deleting an account must kill the feed."* Account erasure deletes the owner's feed row.
- *"Cancelled events left behind."* Cancelled appointments are emitted with `STATUS:CANCELLED` and a title prefix
  for the window, so both clients that honour STATUS and those that do not show the truth.

## External Context
_None ingested._

## Edge Case Analysis

| Case | Handling |
|---|---|
| Pending reschedule | Event stays at the original time (entity invariant); description notes a proposal is pending |
| Requested (unconfirmed) | `STATUS:TENTATIVE`, title prefixed "Pending" |
| Legacy `day_off` rows | Excluded |
| Missing service / name | "Appointment" / "Cita"; name omitted |
| Invalid `end` | `EffectiveEndUtc` (60-minute fallback) |
| Very long history | Window: 60 days back, 365 forward, capped at 500 events |
| Language | Captured at enable time from the app's UI language (en / es) |
| Account deleted | Feed row erased; URL answers 404 |
| Token in logs | Redacted from span URL tags |

## UX Discovery
Skipped: unattended run, visual companion not started; UX decisions recorded by Muse in Progressive Thinking.

## Discovery Summary

Ship a private, revocable subscribed calendar feed for both roles, served by the Calendar service under
`/api/v1/calendar/feed`, plus a mobile **Calendar sync** screen reachable from Profile and Appointments with
Apple/Google subscribe actions, copy, reset and turn-off. Out of scope: on-device calendar writes, two-way busy
import, per-appointment "add to calendar", locations.
