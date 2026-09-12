# Intent
<!-- pdlc-template-version: 2.1.0 -->
<!-- This file defines the core purpose of the product.
     It is set during /pdlc init and should rarely change.
     If the fundamental problem or user changes, update this file and record why in docs/pdlc/memory/DECISIONS.md.
     Claude reads this at the start of every Inception phase to anchor the Discover conversation. -->

**Project:** Agenda Buddy (product name: AgendaMe)
**Created:** 2026-07-30
**Last updated:** 2026-09-12

---

## Project Name

AgendaMe — the scheduling platform for one-to-one service providers

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

AgendaMe lets independent service providers manage their client workflow — from service catalogue and availability through booking, payment, messaging, and follow-up — in one mobile experience.

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

- Multi-provider organizations or team accounts (solo provider only in v1)
- White-labelling
- User-uploaded avatar photos until image storage is designed (`agenda-buddy-5qr`)
- Production Stripe Connect and physical-wallet activation until deployment credentials and external accounts are available (`agenda-buddy-83z`)
- iOS push verification without a physical device (`agenda-buddy-lq5`)

---

## Key Constraints

- .NET 10 microservices architecture — cannot pivot to a monolith or different language without a new architecture decision
- MongoDB as the primary datastore — no relational DB migration planned
- There is no message broker; messaging is MongoDB-backed and notification fan-out is synchronous and best-effort
- .NET Aspire is the primary local orchestrator; Docker Compose is a legacy fallback
- The MAUI client reaches all backend services through the explicit-allowlist YARP Gateway
- JWT authentication, ownership checks, role checks, token revocation, and email verification protect non-public capabilities
