# Architecture: Spanish Mobile Localization (F-033)

## 1. Decision Summary

| Concern | Decision |
|---|---|
| UI resources | Strongly typed neutral-English `AppResources.resx` plus `AppResources.es-MX.resx` |
| Language storage | Device-local preference; no account/profile field |
| First launch | Supported device language, else English |
| Live switch | Rebuild the Shell root after changing culture |
| Profession catalog | Canonical English API name remains identity; client map supplies localized display |
| In-app notifications | Local generic title/body derived from `NotificationType` |
| Lock-screen push | Optional locale on device-token row; backend generic push resources |
| Errors | Client-localized operation/result categories; raw server prose is diagnostic only |
| Legal copy | Resource-backed, structurally paired, human-reviewed before enablement |
| Machine translation | Separate future content feature, not app localization |

No new NuGet package is required.

## 2. Resource Boundary

Add `AgendaBuddy.MobileApp/Resources/Strings/AppResources.resx` and
`AppResources.es-MX.resx`. Keys are semantic (`Profile_EditAction`, `Error_NetworkUnavailable`) rather than
English sentences. XAML uses `x:Static`; C# reads generated properties. A Shell rebuild is therefore the one
refresh mechanism instead of a custom observable localization layer in every view.

The preference interface and coordinator compile on `net10.0` and use a fake store in unit tests. The concrete
MAUI Preferences adapter is mobile-only and is verified by Android/iOS builds plus the live switch smoke test;
the fallback test target does not pretend to execute platform storage.

Resource scope includes:

- 231 measured XAML literals across 31 views/controls;
- runtime labels and errors in view models, models, infrastructure, and services;
- dates, relative time, currency, plural forms, weekday and status labels;
- 115 profession display names;
- 12 notification type labels plus generic title/body templates;
- 20 legal clauses plus titles, summaries, and effective-date labels.

Do not resource protocol or identity values. For example, a provider role remains `"Provider"` in JWT/API
traffic while the screen renders `Proveedor`; `DayOfWeek.Monday` stays the wire value while the screen renders
`lunes`.

## 3. Startup and Shell Replacement

The current `App` receives a singleton `AppShell`; DI therefore constructs the Shell and resolves XAML before
`App` can set culture. `CreateWindow` is synchronous, so an async initialization design cannot fix this.

Introduce three small boundaries:

```text
ILanguagePreferenceStore  synchronous Get/Set/Clear around MAUI Preferences
LanguageCoordinator       validates locale and sets Current/DefaultThread cultures
ILanguageShellService     replaces the current Window.Page with a newly resolved AppShell
```

Change `AppShell` from singleton to transient. `App` receives the coordinator and `IServiceProvider`, applies
the initial culture synchronously, then resolves the first Shell. On selection:

1. validate `en` or `es-MX`;
2. persist and apply culture;
3. capture the current Shell location and signed-in state;
4. resolve a new `AppShell` and assign it to the active `Window.Page`;
5. restore the captured route when valid, otherwise use `//dashboard` or `//login`.

Move global `Routing.RegisterRoute` calls and `JwtDelegatingHandler.UnauthorizedAccess` subscription out of the
Shell constructor into an idempotent startup registration method. Rebuilding Shell must not multiply either.
Singleton session, brand-header, and notification-badge services survive replacement.

## 4. Profession Localization Without Migration

The profession catalog is closed and server-seeded; no route creates a profession. The English `Name` is used
as route, storage, service-association, and provider-profile identity. Replacing it with a code would require a
cross-service data migration and old-client compatibility for no user-visible benefit in the two-language
release.

Add a client `ProfessionDisplayNames` mapping keyed by the exact canonical seed name. It returns a localized
resource and falls back to the canonical name for unknown future data. `ProfessionItem` carries both:

```text
CanonicalName: "Software Engineering"  -> API/search identity
DisplayName:   "Ingeniería de software" -> visible text and localized grouping
```

Selection, add/remove calls, and service payloads continue using `CanonicalName`. Search and A–Z grouping use
`DisplayName` with the active culture. A structural test parses `ProfessionSeedData` and proves every seed has a
mapping and no mapping is orphaned. This makes drift fail CI without changing MongoDB or HTTP contracts.

## 5. Notifications

### In-app

`NotificationSummary` currently displays backend-authored English `Subject`/`Body`. For known types, render a
localized generic title/body from `NotificationType`; keep `Subject`/`Body` only as fallback for unknown legacy
rows. User message previews are content, not system copy: show them unchanged only in the authenticated expanded
view, labelled as original content.

### Push

Lock-screen copy is generated by `NotificationDispatcher.DisplayText`, so client resources cannot affect it.
Extend `RegisterDeviceTokenRequest`, `DeviceTokenEntity`, and `IDeviceTokenService.UpsertAsync` with optional
`languageCode`. Existing requests and rows default to `en`. The mobile client submits the current locale on
initial registration, token rotation, and language change.

No data migration is required: the BSON field is nullable and absent on existing rows. Their read-time fallback
is English; the next token registration or rotation writes the active locale.

`NotificationDispatcher.DisplayText(type, languageCode)` resolves privacy-safe generic text from backend
resources. It never localizes or exposes producer subject/body on the lock screen. Only Booking and Customer
need the locale because they dispatch push.

## 6. Error Boundary

Only a small set of mobile API methods propagate server prose (`BookingApiService`, `CalendarBlockApiService`,
`ProviderApiService`, and `ProfessionApiService`). Do not introduce error codes across all seven services.

Add mobile-owned result categories such as `Validation`, `Forbidden`, `Conflict`, `NotFound`, `Unavailable`,
and `Unknown`, selected from operation plus HTTP status. View models map those categories to resource keys.
The raw server detail may be logged in Debug builds but is never the only visible Spanish guidance. Existing
specific client-side guards remain specific and are localized directly.

If a future UX requires exact field-level server detail that cannot be reproduced client-side, file a narrow
stable error-code contract for that endpoint rather than broadening this feature preemptively.

## 7. Legal and Consent

`LegalDocuments` becomes a resource-backed factory. Both languages contain the same title/summary and ten
ordered heading/body pairs for each document. Store the effective date as a date/version fact and format its
label with the active culture.

Consent storage does not change: the user accepts the same document version regardless of display language.
Spanish enablement requires recorded review of translation meaning, collected-data disclosures, deletion copy,
and consent labels. A language switch never changes consent state.

## 8. Platform Integration

- iOS: package Spanish satellite resources and declare `CFBundleLocalizations` for `en` and `es-MX`. The OS
  app-language setting determines process culture on launch when no in-app override exists.
- Android: package Spanish satellite resources and add a locale config for `en` and `es-MX` so Android 13+
  exposes AgendaMe under App Languages. API 29–32 continue through the in-app preference/coordinator.
- The in-app picker remains cross-platform and authoritative once the user makes an explicit choice.

Platform APIs select a locale; they do not translate resources. Apple Translation and Google ML Kit remain
possible future tools for an explicit Translate action on user content.

## 9. Verification Architecture

- Resource parity tests read neutral and Spanish `.resx` XML directly under `net10.0`.
- A XAML structural test rejects hardcoded user-facing `Text`, `Title`, `Placeholder`, and accessibility values,
  with an explicit glyph/technical allowlist.
- Named C# surfaces have completeness tests: enums, weekdays, roles, fee/payment/appointment states,
  notifications, gateway services, professions, and legal clauses.
- Coordinator tests use a fake preference store and restore process cultures after every case.
- Shell replacement gets a mobile-target smoke test because the `net10.0` slice cannot instantiate MAUI Shell.
- A structural guard proves route registration and the static unauthorized-event subscription live outside the
  transient Shell constructor; repeated Shell construction is exercised on a mobile target.
- Device-token locale and push text get backend unit and integration coverage, including old-client fallback.
- Android and iOS release builds verify satellite resources and locale declarations in packaged output.
- Final visual checks use small phone viewports in English and Spanish and cover both roles.

## 10. Risks

| Risk | Mitigation |
|---|---|
| Mixed-language screens | Spanish stays unavailable until literal scans and parity tests pass |
| Shell rebuild duplicates handlers | Move global registration out of the constructor; repeated-switch test |
| Spanish expansion clips controls | Small-device visual pass; avoid fixed text widths |
| Translation terminology drifts | Approved glossary for appointment, booking, provider, customer, service, and availability |
| Profession mapping drifts | Exact seed-to-resource structural test |
| Lock-screen remains English | Persist locale with device token and test all 12 types |
| Legal translation changes meaning | Human legal/native review gate |
| Existing clients omit locale | Optional field with English fallback |