---
feature: mobile-app
date: 2026-07-31
status: inception-complete
last-updated: 2026-07-31T11:00:00Z
approved-by: ogdevlabs
approved-date: 2026-07-31T11:00:00Z
prd: docs/pdlc/prds/PRD_mobile-app_2026-07-31.md
---

# Brainstorm Log: Mobile App

## Divergent Ideation
_Not run._

## Socratic Discovery

**Completed:** 2026-07-31T10:30:00Z
**Interaction mode:** Sketch

### Round 1 — Problem Statement

**Q1:** What problem does this feature solve?
**A:** No mobile surface exists for Agenda Buddy. All core workflows (booking, calendar, messaging, notifications) are REST-only. Mobile is where providers manage their day and customers book sessions.

**Q2:** Who specifically will use this feature?
**A:** Provider (primary): manages appointments, calendar, notes, messages on-the-go. Customer (secondary): books sessions, receives notifications, messages provider.

**Q3:** What does success look like?
**A:** Provider can complete the core loop (login → view schedule → confirm booking → send message) in under 60 seconds. Runs on iOS 16+ and Android 13+. Zero data inconsistency with backend.

**Q4:** What are the technical constraints?
**A:** Backend is .NET 10 REST APIs; mobile is a consumer client. Auth is RS256 JWT from Identity service. No direct DB access. Library project holds all entities/DTOs. Team is C#/.NET only.

### Round 2 — Future State / Key Capabilities

**Q1:** Which screens are in scope for v1?
**A:** Login, provider dashboard (today's appointments), calendar view, booking management (CRUD), customer list, messaging inbox + thread, notifications list.

**Q2:** Offline support in v1?
**A:** No. Graceful degradation only (stale data + banner). Offline cache is v2.

**Q3:** Push notifications in v1?
**A:** Yes — required. Maps to existing NotificationEntity and NotificationType.

**Q4:** Payment flows in v1?
**A:** Deferred. PaymentService exists on backend but in-app Stripe checkout adds App Store complexity. V1 books; payment is server-side.

### Round 3 — Acceptance Criteria

**Q1:** Must-pass criteria?
**A:** Builds on iOS Simulator + Android Emulator. Full booking lifecycle works. JWT auth end-to-end. Push notifications fire. HTTPS only; no secrets in bundle. Unit tests on ViewModels.

**Q2:** Non-functional requirements?
**A:** Cold start < 3s on mid-range hardware. All API errors visible. App Store / Play Store submission-ready.

## Adversarial Review

**Completed:** 2026-07-31T10:30:00Z

1. MAUI startup slower than Flutter on Android → Mitigated by .NET 10 AOT + API 33+ requirement.
2. Sharing Library entities creates tight coupling → Mitigated by mobile-facing DTOs in a `Library.Mobile` sub-namespace for UI concerns; still share enums, status types, validation.
3. iOS requires Mac for Xcode signing → Framework-neutral; Flutter/RN have same requirement.
4. Blazor Hybrid pivot risk → MAUI supports Blazor Hybrid as an upgrade path; not a competing concern.

## External Context
_None ingested._

## Threat Modeling Triage

- Trust boundary changes: yes — new mobile client (untrusted); new `POST /identity/device-token` endpoint; FCM/APNs egress
- Regulated data: yes — JWT (credential), email addresses (PII per CONSTITUTION.md §4), device tokens linked to user identity
- New attack surface: yes — `POST /identity/device-token`; FCM token registration flow; mobile JWT storage surface; push notification payload
- Triage tier: Full (3/3) — party convened; 5 threats identified (1 HIGH, 2 MEDIUM, 2 LOW); 2 mitigate-now, 1 accept-with-test

## Design-Laws Audit Triage

- UI surface: yes — 8 new screens (LoginPage, DashboardPage, CalendarPage, CustomersPage, MessagingPage, NotificationsPage, AppointmentDetailPage, MessageThreadPage)
- New flow / pattern: yes — provider login → dashboard → confirm flow; messaging inbox + thread; push notification UX; Shell tab navigation
- First-experience pathway: yes — login, post-login Dashboard empty state, push permission prompt
- Triage tier: Full (3/3) — Roundtable convened; 6 findings (0 P0, 3 P1, 3 P2); 3 fix-now, 3 mitigate-later; heuristic total 29/40 (Good)

## Discovery Summary

**Status:** discover-complete
**Recommendation:** .NET MAUI

**Decisive factors:** team is C#-only; Library project provides direct entity/enum reuse; CI extends trivially via existing dotnet.yml; .NET 10 AOT brings competitive startup performance.

**Proposed stack:** .NET MAUI (net10.0-ios + net10.0-android), CommunityToolkit.Maui, CommunityToolkit.Mvvm, MSAL/custom JWT, SecureStorage, Plugin.Firebase.CloudMessaging, Library project reference, xUnit + Moq.

**When to reconsider:** larger external mobile talent pool needed → Flutter or RN. Complex animations / pixel-perfect cross-platform UI → Flutter. Heavy hardware features → RN.
