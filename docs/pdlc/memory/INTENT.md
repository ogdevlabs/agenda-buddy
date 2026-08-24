# Intent
<!-- pdlc-template-version: 2.1.0 -->
<!-- This file defines the core purpose of the product.
     It is set during /pdlc init and should rarely change.
     If the fundamental problem or user changes, update this file and record why in docs/pdlc/memory/DECISIONS.md.
     Claude reads this at the start of every Inception phase to anchor the Discover conversation. -->

**Project:** Agenda Buddy
**Created:** 2026-07-30
**Last updated:** 2026-07-30

---

## Project Name

Agenda Buddy — the scheduling platform for one-to-one service providers

---

## Problem Statement

Professionals who offer personalized one-to-one sessions — fitness coaches, tutors, therapists, software instructors, and hundreds of other specialists — lack a purpose-built tool to manage their clients and appointments. Generic calendar tools don't understand the provider/customer relationship, and full CRM platforms are overkill for a solo practitioner. These providers spend time on scheduling admin that should be spent on their clients, and have no unified place to manage their service catalog, customer list, bookings, and communications. Agenda Buddy fills that gap with a lightweight platform built specifically for the session-based service economy.

---

## Target User (Persona)

**Primary: The Independent Service Provider**
- Solo professional offering personalized one-to-one sessions (fitness coach, tutor, therapist, coding instructor, etc.)
- Manages 5–50 active clients; books 5–20 sessions per week
- Currently juggling a calendar app, a contacts spreadsheet, and direct messaging — no unified view
- Frustrated by no-shows, double-bookings, and manual follow-up
- Wants to spend less time on scheduling admin and more time delivering value to clients
- Will adopt a new tool if onboarding is fast and the core flow (add client → book session) is under 2 minutes

**Secondary users (if any):**
- Customers/clients of providers — book appointments, receive confirmations, view upcoming sessions

---

## Core Value Proposition

Only Agenda Buddy lets independent service providers manage their entire client workflow — from service catalog to appointment booking — in one place, so they spend zero time on scheduling admin.

---

## What Success Looks Like

| Metric | Target | Timeframe |
|--------|--------|-----------|
| Provider can book a first appointment | < 2 minutes from registration | At launch |
| All core CRUD operations covered | Provider, Customer, Booking, Calendar, Services, Professions | Before public beta |
| Authentication in place | All endpoints protected | Before public beta |
| Test coverage across all services | > 80% unit test pass rate | Before v1.0 |
| Zero Sev-1 bugs | No data loss or booking corruption bugs | First 30 days post-launch |

---

## Out of Scope

- ~~Mobile app (web API only for now — mobile client is a future phase)~~ **Stale as of F-012/F-015.** The
  mobile app shipped (F-012, `MobileApp/`, .NET MAUI) and, as of F-015 (2026-08-23), it actually reaches the
  live backend through a gateway (`Gateway/`) — real dashboard/calendar/customers data, real session notes,
  payments, and provider reports, with the `SeedDataProvider` fixture fallback removed. Flagged as stale at
  this feature's own Discover step; corrected here at F-015-T14's closing verification.
- Payment processing (fee tracking exists on ServiceEntity but no payment flow)
- Journal and notes feature (listed in README as future)
- Provider-to-customer messaging (Kafka infrastructure is in place but messaging UI is not built)
- Multi-provider organizations or team accounts (solo provider only in v1)
- White-labelling

---

## Key Constraints

- .NET 8 microservices architecture — cannot pivot to a monolith or different language
- MongoDB as the primary datastore — no relational DB migration planned
- Kafka already wired in for async messaging — new async features should use the existing Kafka infrastructure
- Docker Compose for local development — all services must run containerized
- ~~No authentication layer exists yet — this is a critical gap before any public exposure~~ **Stale as of
  F-001/F-021.** JWT-based authentication has existed since F-001, and F-021 (identity-hardening) added
  login/register throttling, transport security, and closed the account-destroying-refresh gap. Every
  service requires a valid JWT except the seven routes deliberately left anonymous (register/login/refresh/
  logout, and reference-data reads on Professions). Flagged as stale at this feature's own Discover step;
  corrected here at F-015-T14's closing verification.
