---
feature: spanish-mobile-localization
date: 2026-09-08
status: brainstorm-complete
last-updated: 2026-09-08T00:00:00Z
approved-by: pending
approved-date: pending
prd: pending
---

# Brainstorm Log: Spanish Mobile Localization

## Process Note

This brainstorm was grounded in `INTENT.md`, `CONSTITUTION.md`, the current mobile composition root,
the Language screen, the legal documents, notification rendering, and API error handling. No existing
Beads item covers Spanish or localization. The app currently lists Spanish honestly as unavailable and
has no resource-string infrastructure.

## Discovery Summary

- **Problem:** Spanish-speaking providers and customers cannot use the app in Spanish. The existing
  Language screen cannot change language because user-facing copy is embedded in XAML, view models,
  models, services, and legal-document objects.
- **Users:** independent providers are primary; their customers are secondary. Both roles need the same
  language coverage across authentication, scheduling, messaging, notifications, account, and legal flows.
- **Success:** a user can select Spanish, immediately continue in Spanish, relaunch in Spanish, and complete
  every mobile workflow without system-authored English appearing. Names, service descriptions, messages,
  and other user-authored content remain exactly as written.
- **Current surface:** 27 mobile view XAML files plus runtime strings in view models and services. The two
  role journeys cover authentication and recovery; provider profile, profession, service, working-week and
  time-off management; customer discovery and subscription; booking and rescheduling; messaging;
  notifications; consent; and two-phase account deletion. The notification inbox renders server-authored
  subject/body text, and several API clients surface backend error strings. The Terms and Privacy Policy are
  also shipped as English C# literals.
- **Constraints:** .NET MAUI on .NET 10; no new package without discussion; the `net10.0` test slice must
  continue to cover localization logic without MAUI workloads; Spanish must not become selectable before
  the app genuinely supports it.

## Options Considered

### 1. Built-in RESX resources plus a root-Shell rebuild (recommended)

Use neutral English `AppResources.resx` and Spanish `AppResources.es-MX.resx`, generated as a strongly
typed resource class. XAML uses `x:Static`; C# uses the same generated properties. A language coordinator
sets `CurrentCulture`, `CurrentUICulture`, and the thread defaults before the first Shell is constructed.
Changing language stores the choice, applies the culture, and reconstructs the Shell while preserving the
signed-in session and returning the user to a stable route.

**Why:** this uses the platform resource system, adds no dependency, supports satellite-resource fallback,
and keeps view models free of MAUI-specific localization plumbing. Rebuilding the root is simpler and more
reliable than teaching every existing object to react to a culture-changed event.

### 2. Observable localization service with binding-based live updates

Bind every localizable property through an indexer or markup extension and raise property changes when the
culture changes.

**Rejected for the first two-language release:** it avoids rebuilding navigation, but it introduces a
custom runtime dependency into almost every view and still does not refresh computed view-model properties
unless they also subscribe. The extra moving parts do not buy enough for an app where language changes are
rare.

### 3. Duplicate pages, runtime translation, or string dictionaries

**Rejected:** duplicated XAML guarantees layout drift; runtime translation makes legal and security copy
nondeterministic and network-dependent; ad hoc dictionaries give up compile-time key checks and standard
resource fallback.

### 4. Platform on-device translation

Apple's Translation framework can translate individual strings or batches on iOS 17.4 and later, and Google
ML Kit offers downloadable on-device translation models for Android and iOS. These are useful capabilities,
but they do not automatically localize a MAUI interface. Android's per-app language APIs select among
resources the app already ships; Android explicitly documents that adding a locale does not translate those
resources. Google also describes ML Kit translation as intended for casual and simple text, with quality that
must be evaluated for each use case.

**Rejected as a replacement for RESX localization:** app navigation, consent, account deletion, validation,
and legal terms need deterministic, reviewed wording; model availability and first-use downloads cannot be a
prerequisite for understanding them. Apple and Google APIs would also create different platform paths or add
a new cross-platform SDK dependency.

**Good follow-up use:** an explicit "Translate" action on user-authored messages or service descriptions,
clearly labelled as machine translation and performed on-device. It should preserve and let the reader return
to the original text. It is content translation, not Spanish app support, and belongs in a separate feature.

## Recommended Design

### Locale and ownership

- Ship `en` and `es-MX`, using neutral Latin-American Spanish wording. Confirm the exact Spanish locale and
  glossary with the product owner during Define; changing locale after translation starts is avoidable churn.
- On first launch, follow the device language when it is supported; otherwise use English. A manual choice
  overrides device language.
- Store the override locally with MAUI Preferences, not on the user profile. Language is a device/app choice,
  must work before sign-in, and should not require a network request.
- Set both UI and formatting cultures so resource lookup, dates, numbers, and currency agree.

### Resource architecture

- Add one strongly typed `AppResources` family under `Resources/Strings` for mobile-owned copy.
- Use stable semantic keys such as `Profile_EditAction`, not English sentences as keys.
- Move XAML titles, labels, placeholders, accessibility descriptions, validation messages, empty states,
  toasts, and computed labels into resources.
- Resource every display label derived from a wire or persistence value without changing that value: all
  current and future `NotificationType` and appointment-status members, roles, weekdays, fee types, and
  rescheduling states. English enum names remain protocol values; Spanish is presentation only.
- Format dynamic values with resource templates and the active culture. Use explicit singular/plural keys;
  do not build translated sentences by concatenating fragments.
- Keep brand names, email addresses, route names, identifiers, protocol values, and user-authored content out
  of translation.

### Profession catalog identity

- Profession names are system-authored catalog content and must be localized. They are not equivalent to a
  provider-authored service name or description.
- The current contract has no usable language-neutral profession key: `ProfessionEntity` has only `Id` and
  English `Name`, the mobile client deliberately ignores the malformed serialized `Id`, and profile/service
  operations send the English name back as identity.
- Add a stable culture-neutral profession code to the catalog contract and return a localized display name
  separately. The client stores/sends the code and renders the display name. Keep accepting existing English
  names during a backward-compatible migration; do not make translated text a route or database identity.
- Profession search and A-Z grouping operate on the localized display name, using the active culture's
  comparison rules.

### Runtime switching

- Introduce a MAUI-free language policy/coordinator with a small preference-store abstraction, so selection,
  fallback, and culture application remain testable under `net10.0`.
- Apply the stored/default culture before `AppShell.InitializeComponent()`; otherwise tab titles are created
  in English and remain stale.
- Move global route registration out of the `AppShell` constructor or make it one-time before supporting
  Shell reconstruction. Re-registering routes and static event handlers on every switch is unsafe.
- Recreate the Shell after selection, preserve the authenticated session, and navigate to a stable signed-in
  root. Do not attempt to mutate hundreds of already-created controls in place.

### Server-authored content boundary

- Do not translate user messages, names, notes, or service descriptions.
- Render system notifications from `NotificationType` plus structured interpolation data on the client.
  Extend the notification contract additively and retain `Subject`/`Body` as a fallback for old rows. This
  lets old notifications remain readable while new system copy follows the currently selected language.
- Keep a message notification's user-authored preview unchanged; localize only its surrounding system label.
- Replace display of arbitrary backend error prose with stable error codes and client-localized messages for
  actionable cases. Preserve the server detail for diagnostics, not as primary UI copy.
- Email localization is a separate delivery concern and is not required to call the Mobile App Spanish, but
  it should be filed explicitly so account emails do not become forgotten product debt.

### Legal content

- Resource the Terms and Privacy Policy structurally, preserving every heading and clause in both languages.
- Require human legal/translation review before Spanish can be enabled. Machine-generated legal copy is not
  an acceptable release source.
- Keep the effective date and document version aligned across languages; a translation correction that
  changes meaning requires the normal consent/version decision.
- Review consent, password reset, account deletion, cancellation, and reschedule approval/decline copy in
  context. These are decisions with security, legal, or scheduling consequences, not ordinary labels.

## Delivery Strategy

Build this as one feature with guarded waves; keep `AppLanguages.IsSelectable("es-MX")` false until the final
wave:

1. Add resources, culture/preference infrastructure, and startup behavior; migrate one representative flow.
2. Migrate all XAML and client-generated runtime copy, including accessibility text and date/number formats.
3. Add structured notification rendering and stable localized API-error handling.
4. Add professionally reviewed Spanish Terms, Privacy Policy, glossary, and translation review evidence.
5. Enable Spanish, localize store metadata, and verify both roles on iOS and Android.

This sequencing prevents a partially translated build from claiming support while allowing each wave to be
validated independently.

## Verification and Acceptance Direction

- Resource-key parity: every English key exists and is non-empty in Spanish; no orphan Spanish keys.
- A structural XAML test rejects new literal user-facing `Text`, `Title`, `Placeholder`, and accessibility
  values, with a narrow allowlist for glyphs and non-language values.
- Source-level guards cover user-visible view-model/service assignments that XAML scanning cannot see.
- Culture tests prove device fallback, persisted override, unknown-locale fallback, and `es-*` matching.
- Every `NotificationType` has localized labels/templates and legacy notification fallback remains readable.
- Both legal documents retain the same ordered clause structure and required privacy disclosures.
- Mobile tests pass under `/p:MobileWorkloads=false`; Android and iOS builds pass; live device/simulator smoke
  tests cover registration, sign-in and recovery; provider setup/calendar/booking management; customer
  discovery/subscription/booking; shared rescheduling and messaging; notifications; consent and deletion;
  language switching; and relaunch persistence in both languages.
- Visual review checks truncation and wrapping on small phones. Spanish commonly expands English copy, so a
  compile-only check is insufficient.

## Risks and Adversarial Review

- **Mixed language after selection:** prevented by the final availability gate and literal-string scans.
- **Shell reconstruction loses session/navigation:** session services remain singleton; switch returns to a
  documented stable route and is exercised in a mobile smoke test.
- **Old notifications remain English:** additive fallback is honest historical behavior; new structured rows
  localize dynamically.
- **Backend error contracts broaden scope:** localize stable, user-actionable cases first and never present raw
  server text as the only guidance. Track contract migration explicitly in the feature tasks.
- **Translation sounds correct but is operationally wrong:** establish a scheduling-domain glossary and require
  native-speaker review in both provider and customer contexts.
- **Threat-model triage:** focused review is required for localized auth, consent, deletion, and privacy copy.
  Localization adds no new trust boundary, but mistranslating a destructive or consent action can invalidate
  the user's decision.

## Recommendation

Proceed to PDLC Define as a dedicated cross-cutting mobile feature, not a one-off translation task. The best
solution is built-in strongly typed RESX localization, local device preference, culture applied before Shell
construction, controlled Shell reconstruction for immediate switching, and semantic localization of server
system content. Spanish should remain unavailable until resource parity, legal review, and end-to-end role
verification all pass.