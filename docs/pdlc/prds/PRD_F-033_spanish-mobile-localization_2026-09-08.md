# PRD: F-033 — Spanish Mobile Localization

**Feature ID:** F-033
**Beads:** `agenda-buddy-d8l`
**Date:** 2026-09-08
**Status:** Shipped in `v0.17.0` — external legal/native review remains `agenda-buddy-hff`

## Problem

AgendaMe lists Spanish on its Language screen but refuses selection because the app has no localization
infrastructure. User-visible text is embedded across 31 XAML views/controls, view models, models, services,
notification handling, the seeded profession catalog, and the in-app Terms and Privacy Policy. Flipping the
existing availability flag would produce a mixed-language app and violate the screen's current honesty rule.

The measured surface is 231 literal XAML text/title/placeholder/accessibility attributes, approximately
150–200 runtime C# strings, 115 seeded profession labels, 12 notification types, and 20 legal clauses.

## Users

- Independent providers configuring services, working weeks, time off, appointments, messages, and accounts.
- Customers discovering providers, subscribing, booking, rescheduling, messaging, and managing accounts.
- Spanish-speaking users before sign-in, including registration, email confirmation, and password recovery.

## Goal

A user whose device is Spanish, or who selects Spanish in the app, can complete every mobile workflow in
reviewed Latin-American Spanish and relaunch in the same language. Protocol values and user-authored content
remain unchanged.

The initial locale is `es-MX`: it gives the product one concrete vocabulary and formatting target rather than
an unreviewable claim to cover every Spanish market. The wording should avoid unnecessary Mexican regionalisms
so later `es-*` variants can inherit it safely.

## Requirements

1. Use built-in strongly typed `.resx` resources with neutral English and `es-MX` satellite resources. Add no
   localization package.
2. On first launch, use Spanish when the device UI culture begins with `es`; otherwise use English. An explicit
   in-app selection persists locally and overrides later device-language changes.
3. Apply culture before `AppShell.InitializeComponent()` so Shell tabs and first-page XAML resolve in the
   correct language.
4. Switching language takes effect immediately by replacing the Shell root, preserving authentication and
   restoring a safe route. It must not duplicate global route registrations or static event subscriptions.
5. Localize every mobile-owned visible and accessibility string: XAML literals, computed labels, validation,
   empty states, toasts, status text, dates, numbers, currency, plural forms, and foreground notification UI.
6. Keep API route names, enum values, IDs, email addresses, brand name, automation IDs, and user-authored names,
   service descriptions, messages, appointment descriptions, notes, and time-off reasons unchanged.
7. Localize all 115 seeded profession display names on the client while continuing to send their canonical
   English names to the existing API. A completeness test must fail when seed data and the display map drift.
8. Render in-app notification title/body and category from all 12 `NotificationType` values. Existing persisted
   English `Subject`/`Body` remain a compatibility fallback, not the primary Spanish presentation.
9. Add an optional locale to device-token registration and persistence. Booking/Customer push dispatch uses it
   to produce generic, privacy-safe Spanish lock-screen copy. Existing clients and rows without a locale default
   to English.
10. Do not display arbitrary backend English prose as the primary error. Map operation plus stable HTTP/result
    category to localized client copy; retain server detail only for diagnostics. A repo-wide backend error-code
    migration is not required for this feature.
11. Resource the complete 10-clause Terms and 10-clause Privacy Policy. Spanish legal text requires human legal
    and native-language approval before Spanish becomes selectable.
12. Advertise English and Spanish in the packaged iOS and Android applications so operating-system app-language
    settings and store metadata recognize both supported languages.
13. Keep Spanish marked unavailable until resource parity, legal approval, Android/iOS builds, and both-role
    smoke tests pass.

## Acceptance Criteria

- AC-1: A fresh install on a Spanish device opens in Spanish before the first Shell is constructed.
- AC-2: Selecting English or Spanish changes the whole app immediately and survives process restart.
- AC-3: Authentication/session state survives language switching; duplicate navigation and notification event
  handling do not occur after repeated switches.
- AC-4: Automated resource parity reports no missing, orphaned, or empty Spanish keys.
- AC-5: Structural tests reject newly introduced user-visible XAML literals and named C# display-string surfaces,
  with a narrow allowlist for glyphs and technical values.
- AC-6: Every appointment/payment status, weekday, fee type, notification type, date band, count label, and
  role label renders from resources.
- AC-7: Every seeded profession has one reviewed Spanish display name; searching and A–Z grouping use the
  localized display while writes still use the canonical API name.
- AC-8: All 12 in-app notification types and all privacy-safe push banners render in the device token's locale;
  missing/unknown locale falls back to English.
- AC-9: Common authentication, booking, rescheduling, messaging, calendar, service, profession, and account
  failures show localized actionable copy rather than raw server prose.
- AC-10: English and Spanish legal documents have identical clause count/order and required privacy topics;
  review evidence is recorded before enablement.
- AC-11: Android and iOS release builds contain the Spanish resources and declare the locale to the platform.
- AC-12: Backend, integration, and mobile suites pass; the guarded Android build also passes after `#if MOBILE`
  or `#if FIREBASE` changes.
- AC-13: Manual/simulator verification covers both roles, small-screen layout, VoiceOver/TalkBack labels,
  language switching, relaunch, and at least one flow in every primary navigation area.

## Non-Goals

- Machine-translating user-authored messages, services, notes, or descriptions. A future explicit Translate
  action may use Apple Translation or Google ML Kit while preserving the original.
- Translating transactional email in this feature. Email localization needs an account-level language decision
  because email is delivered when no device is active.
- Migrating profession identity or provider/service documents to new codes. The fixed, server-seeded English
  name remains an internal key for this release.
- Translating backend API prose or adding error codes to every service.
- Supporting right-to-left layout or a third language.
- Changing timezone behavior, routes, or domain business rules.

## Release Gate

`AppLanguages.IsSelectable("es-MX")` is the final switch, not the first implementation step. It changes only
after every acceptance criterion above has evidence. Until then, the current unavailable row remains.