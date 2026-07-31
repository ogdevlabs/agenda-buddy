# PRD: Mobile App (iOS + Android)
<!-- pdlc-template-version: 2.1.0 -->

**Date:** 2026-07-31
**Status:** Approved
**Feature slug:** mobile-app
**Episode:** —

---

## Overview

Agenda Buddy's backend exposes a full scheduling and communication API but has no mobile client. Independent service providers manage their day from their phones — between sessions, on the go, away from a desk. This feature delivers a .NET MAUI cross-platform app targeting iOS 16+ and Android 13+ so providers can manage appointments, clients, and messages from a native mobile experience. It directly advances the core promise in INTENT.md: providers spend zero time on scheduling admin.

---

## Problem Statement

All of Agenda Buddy's core workflows — appointment booking, calendar management, client roster, messaging, and notifications — are accessible only via REST API. No mobile surface exists. Providers and customers cannot take action on their schedule from a phone. This blocks the product from reaching its primary users in the context where they actually work: between sessions, away from a computer. Without a mobile app, adoption is limited to developers or API integrations, not the independent service professionals the platform is built for.

---

## Target User

**Primary — Independent Service Provider:** Solo professional (fitness coach, tutor, therapist, software instructor) managing 5–50 active clients and 5–20 sessions per week. Needs to view today's schedule, confirm pending bookings, check calendar availability, read session notes, and message clients — all from a phone, often between sessions with under 60 seconds available.

**Secondary — Customer/Client:** Client who books sessions with a provider, receives appointment notifications, and messages their provider. Interaction is primarily reactive: viewing, confirming, messaging. Booking a session is the key active flow.

---

## Requirements

1. The app MUST authenticate via the existing Identity service RS256 JWT — login with email/password and receive a bearer token stored in platform secure storage (iOS Keychain, Android Keystore).
2. The app MUST display the provider's appointments for the current day on a dashboard screen, loaded from the Booking service.
3. The app MUST allow a provider to confirm, cancel, and complete appointments from the app.
4. The app MUST display 30-day calendar availability loaded from the Calendar service.
5. The app MUST display the provider's customer list from the Customer service.
6. The app MUST display the messaging inbox and individual message threads, loaded from the messaging endpoints.
7. The app MUST display the notifications list and mark individual notifications as read.
8. The app MUST deliver push notifications for appointment events (booked, updated, cancelled, completed) using FCM (Android) and APNs (iOS).
9. The app MUST make all API calls over HTTPS; no API keys or JWT private keys may be embedded in the app bundle.
10. The app MUST display a visible error state (banner or inline message) when an API call fails; no silent failures.
11. The app SHOULD cold-start to the dashboard in under 3 seconds on mid-range hardware (Pixel 6 class, iPhone 13 class).
12. The app MUST be submission-ready for the Apple App Store (iOS 16+ target, Privacy Manifest) and Google Play (API 33+ target SDK).
13. Payment flows (in-app Stripe checkout) are explicitly out of scope for v1 — booking confirmation is the end of the v1 flow.
14. Offline editing is explicitly out of scope for v1 — the app degrades gracefully (stale data banner) but does not write while disconnected.

---

## Assumptions

- The existing Identity service JWT endpoint accepts standard `POST /login` (email + password) and returns a signed RS256 bearer token consumable by all microservices.
- All backend endpoints already enforce JWT bearer auth (F-001 is shipped); the mobile app does not need to work around unauthenticated endpoints.
- FCM and APNs credentials will be provisioned before the push notification task begins; the backend notification delivery mechanism (webhook or direct FCM call) will be set up alongside the mobile client.
- The Library project's entities (`AppointmentEntity`, `ServiceEntity`, `NotificationEntity`, `MessageEntity`) are accessible as NuGet-style project references in the MAUI project within the same solution.
- The team has access to a macOS machine for the iOS build and Xcode signing step; Android builds can run on any CI platform.
- No white-label or multi-tenant requirements exist — the app connects to a single known backend base URL configurable at build time.

---

## Acceptance Criteria

1. A provider with valid credentials can log in on both iOS Simulator and Android Emulator; the JWT is stored in SecureStorage and used for all subsequent API calls.
2. The dashboard loads and displays today's appointments within 3 seconds of login on a local network against the dockerised backend.
3. A provider can tap a pending appointment and confirm, cancel, or complete it; the status change is reflected immediately in the UI and verified against the Booking service.
4. The 30-day calendar view loads and correctly marks booked vs available slots.
5. The customer list loads and displays at least the customer name and email.
6. The messaging inbox loads threads; opening a thread displays messages in order; sending a message appends it to the thread and is returned on the next fetch.
7. The notifications list loads; tapping a notification marks it as read and the unread badge decrements.
8. A simulated FCM test push triggers a visible notification on Android Emulator (API 33+); a simulated APNs push triggers on iOS Simulator.
9. All API calls use HTTPS; no credentials or keys appear in the compiled app bundle or in `appsettings.json` committed to source.
10. When the backend is unreachable, every screen displays a non-empty error state — no blank screens, no unhandled exceptions visible to the user.
11. `dotnet build -f net10.0-android -c Release` and `dotnet build -f net10.0-ios -c Release` complete with zero errors on CI.
12. ViewModel unit tests cover: login success, login failure (invalid credentials), appointment status change (confirm/cancel/complete), and notifications mark-read.

---

## User Stories

**US-001: Provider login**
*Acceptance criteria: 1, 9*
Given a provider has a registered account in the Identity service
When they enter their email and password on the login screen and tap "Sign in"
Then they are authenticated, their JWT is stored securely, and they land on the dashboard
And the JWT is attached as a bearer header to every subsequent API call

**US-002: View today's appointments**
*Acceptance criteria: 2, 10*
Given a logged-in provider opens the app
When the dashboard loads
Then it displays all of today's appointments with status, customer name, and scheduled time
And if the network is unavailable, it shows a "Could not load appointments" banner instead of a blank screen

**US-003: Manage appointment status**
*Acceptance criteria: 3*
Given a provider views a pending appointment
When they tap Confirm, Cancel, or Complete
Then the appointment status updates in the backend and the UI reflects the new status immediately

**US-004: View calendar availability**
*Acceptance criteria: 4*
Given a logged-in provider opens the Calendar screen
When the 30-day availability loads
Then booked slots are visually distinguished from available slots

**US-005: Message a customer**
*Acceptance criteria: 6*
Given a provider opens a message thread with a customer
When they type a message and send it
Then the message appears at the bottom of the thread and is persisted via the messaging API

**US-006: Receive push notification**
*Acceptance criteria: 8*
Given a provider has granted notification permission
When an appointment event occurs (booked, updated, cancelled, completed) on the backend
Then a push notification appears on the provider's device with the appointment details

**US-007: Handle API errors gracefully**
*Acceptance criteria: 10*
Given a provider is using any screen in the app
When an API call fails (network timeout, 5xx, 401)
Then the screen shows a visible, human-readable error state
And the app does not crash or display a blank white screen

---

## Non-Functional Requirements

- **Performance:** Cold start to interactive dashboard in under 3 seconds on Pixel 6 class (Android 13) and iPhone 13 class (iOS 16).
- **Security:** JWT stored only in platform secure storage (iOS Keychain, Android Keystore). No secrets in app bundle. All traffic over HTTPS. Token must be cleared on explicit logout.
- **Security:** RS256 public key used for local JWT validation must be injected at build time via environment/config — never hardcoded.
- **Reliability:** All network calls wrapped with error handling; no unhandled exceptions propagate to the user as crashes.
- **Compatibility:** iOS 16+ and Android API 33+.
- **Accessibility:** All interactive controls must have accessible labels (MAUI `SemanticProperties.Description`).
- **App Store compliance:** Apple Privacy Manifest (PrivacyInfo.xcprivacy) present; no use of private APIs. Google Play target SDK ≥ 33.
- **Build:** CI must produce a green `dotnet build` for both `net10.0-android` and `net10.0-ios` targets.

---

## Out of Scope

- **In-app Stripe payment checkout** — PaymentService exists on the backend; the v1 mobile app does not initiate or display payment flows. Booking confirmation is the end of the v1 transaction.
- **Offline editing** — the app does not queue writes while disconnected; it degrades to a read-only stale view with a connectivity banner.
- **Customer-facing booking flow** — v1 targets the provider persona. Customer flows (discovering providers, self-service booking) are a v2 concern.
- **Session notes (NoteEntity)** — exists on the backend; not surfaced in v1 mobile UI.
- **Reporting dashboard** — ProviderReport data is available via API but not rendered in v1.
- **Blazor Hybrid / web view** — pure MAUI native controls only in v1; Blazor Hybrid is an upgrade path for v2.
- **Multi-provider organizations or team accounts** — solo provider only, matching the backend constraint.

---

## Known Risks

- **FCM + APNs provisioning dependency** — push notifications require backend changes (FCM server key, APNs certificate) and device registration endpoints not yet built. If provisioning is delayed, push notifications may ship as a fast-follow after core app.
- **iOS macOS build dependency** — a macOS runner is required for the iOS build and Xcode signing step. If CI does not have a macOS runner configured, iOS builds must be run locally until one is provisioned.
- **MAUI .NET 10 package ecosystem** — CommunityToolkit.Maui and Plugin.Firebase.CloudMessaging must be validated against `net10.0-ios` and `net10.0-android` TFMs. Compatibility issues would require downgrade or alternative packages.

---

## Design Docs

- Architecture: [ARCHITECTURE.md](../../design/mobile-app/ARCHITECTURE.md)
- Data model: [data-model.md](../../design/mobile-app/data-model.md)
- API contracts: [api-contracts.md](../../design/mobile-app/api-contracts.md)
- Threat model: [threat-model.md](../../design/mobile-app/threat-model.md) *(triage: Full — 2 mitigate-now, 1 accepted)*
- UX review: [ux-review.md](../../design/mobile-app/ux-review.md) *(triage: Full — 3 fix-now P1, 3 mitigate-later P2; 29/40 heuristics)*

---

## Related Episodes

- F-001 auth-and-identity (PR #19) — JWT RS256 auth foundation the mobile app consumes
- F-006 booking-notifications (PR #24) — NotificationEntity and NotificationType the push flow maps to
- F-007 provider-customer-messaging (PR #25) — MessageEntity and MessageService the inbox consumes

---

## Approval

**Approved by:** ogdevlabs
**Date approved:** 2026-07-31
**Notes:**
