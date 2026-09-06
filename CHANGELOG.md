# Changelog

All notable changes to this project are documented in this file, in [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) style.

## [Unreleased]

### Fixed

- **F-029**: transactional email is sent from `AgendaMe@fererelabs.com` rather than
  `onboarding@resend.dev`, Resend's sandbox sender. The sandbox address needs no verified domain but delivers
  **only** to the Resend account owner's own address — every other recipient's mail was accepted and dropped, so
  email confirmation and password reset silently went nowhere for every real user while the send reported
  success. Nothing overrode the default anywhere (no `appsettings` `Email` section, no Terraform variable, no
  deploy-workflow env var), so it was the effective value in every environment including the deployed one.
  Pinned by a test, because the failure is silent in both directions: the sandbox address swallows mail, and an
  unverified sending domain is rejected by Resend and absorbed by `ResendEmailSender`'s deliberate
  swallow-on-failure contract (ADR-063). ⚠️ **`fererelabs.com` must be verified in the Resend dashboard —
  domain, DKIM and SPF — or every send is rejected**, silently, for that same reason.

## [0.16.0] - 2026-09-06

### Security

- **F-028 notification-delivery-and-inbox-ux**: the push notification body no longer carries the
  notification's content. Threat **T-002** (mobile-app threat model, "PII Exposed in Push Notification
  Lock-Screen Payload", recommended *Mitigate now*) **had never been implemented** — the dispatcher passed each
  producer's own subject and body straight through, so an unauthenticated lock screen displayed the customer's
  email address, service name and appointment time for bookings, and for messaging the *sender's address in the
  title* plus a 120-character preview of the private message. The OS-displayed title/body are now derived from
  `NotificationType` alone (`NotificationDispatcher.DisplayText`) and say a category, never content; the real
  text moves to the FCM `data` payload, which the OS delivers to the app rather than drawing, and is rendered
  only in-app behind authentication — T-002's own prescribed mechanism (ADR-060). The in-app inbox and email
  are deliberately unchanged. Enforced by `NotificationDispatcherTest`'s T-002 section over every
  `NotificationType`, which is the mitigation's only enforcement: the exposure is invisible from the code and
  indistinguishable from a working notification.
- **F-028**: a signed-out account no longer stays addressable through its device token (T-NEW-1). `LogoutAsync`
  removed nothing server-side and `DeviceTokenService` keys on email, so signing out as A and in as B on one
  device left both rows holding the same token and A's notifications still arriving on it — indefinitely if
  nobody else ever signed in. Enforced on both writes: `UpsertAsync` evicts the token from every other account,
  and a new `DELETE /device-token` releases it at sign-out, called before the local JWT clear because the route
  authorises off that token (ADR-061, `agenda-buddy-5of`).

### Added

- **F-028**: a push arriving while the app is in the foreground now draws an in-app banner. Neither platform's
  OS presents one — Android hands a foreground message to the app instead of the tray, iOS asks and defaults to
  nothing — and the client subscribed only to `NotificationTapped`, so such a notification was completely
  silent. `SubscribeToEvents` now handles `NotificationReceived` (snackbar with a "View" action via a new
  `IInAppAlertService`, plus an immediate badge increment reconciled against the server) and `TokenChanged`,
  without which push died silently for the rest of the session on every FCM token rotation (ADR-059).
- **F-028**: every FCM message states its priority, sound and Android notification channel. FCM's defaults are
  wrong here in three ways that all present as "push does not work" — a normal-priority message can sit in Doze
  for hours, a soundless one is indistinguishable from none, and a channel-less one lands on the SDK's
  auto-created "Miscellaneous" at an importance nothing chose. The channel is declared in the manifest and
  created at `High` importance (ADR-062).
- **F-028**: pull-to-refresh, Today/Yesterday/weekday date bands (local dates), a per-type glyph and accent, and
  an explicit per-row "Mark as read" on the notification inbox. New `NotificationVisuals` (MAUI-free, so the
  mapping is covered by the `net10.0` test slice) with `HexColorConverter` as the XAML adapter.

### Changed

- **F-028**: `INotificationApiService.GetUnreadCountAsync` and `MarkAllReadAsync` return `long?`, where `null`
  means "could not read" rather than zero. The unread count returned `0` on any failure and `RefreshAsync` runs
  on every navigation, so a single network blip overwrote a real count with zero and silenced the app's only
  cross-screen signal; and a caller could not word "the server was not reached" and "there was nothing left to
  mark" differently when both arrived as `0`.
- **F-028**: "Mark all read" reports its outcome on all three paths (marked N / nothing left to mark / could not
  reach the server) — it was silent on every one, so a success, a refusal and a dropped connection were
  indistinguishable. It now hides rather than greys when nothing is unread.
- **F-028**: unread is signalled once at the leading edge of each row in the notification type's own colour; it
  was a 3px bar 40px wide at the *bottom* of the card. Each row's chrome no longer repeats the app monogram and
  brand name.
- **F-028**: `PushPayloadKeys` (`AgendaBuddy.Library`, read by the client) is now the single definition of the
  FCM `data` contract. Nothing else coupled `NotificationDispatcher` to `PushNotificationService`, so a rename
  on either side was silent — the payload still arrived and the client read a key that was not in it.

### Fixed

- **F-028**: the notification inbox's expanded row showed the stored UTC instant rather than local time, so it
  disagreed with the "3h ago" line directly above it for every reader not on UTC.
- **F-028**: `bruno/agenda-buddy/0-Auth/5 Register device token` sent `"platform": "Android"` against a
  case-sensitive check, so that request had never registered anything.

## [0.15.0] - 2026-08-27

### Added

- **F-024 data-subject-rights**: `IEventStore.EnsureIndexAsync()` creates a TTL index on `Event.TimeStamp`
  (`EventStore:RetentionDays`, default 400 days) and a secondary index on `Event.Type`, wired into every
  service that registers `IEventStore`. Bounded retention closes the one surviving gap in "does erasure
  work" — the appointment 2-copy deletion and query-audit PII amplification named at this feature's
  original filing were already fixed by earlier work; only the audit trail's unbounded lifetime remained
  (ADR-056). Field-level encryption for `NoteEntity.Content` evaluated and descoped (ADR-057,
  `agenda-buddy-vba`); a cross-service export/erasure API also descoped (`agenda-buddy-ge2`). New
  `docs/pdlc/design/data-subject-rights/RETENTION.md` documents the policy.

### Fixed

- `AgendaBuddy.ServiceDefaults.Tests.TelemetryPiiTest` no longer intermittently drops its own exported
  span under host CPU contention (many test-project processes running concurrently) — explicit, bounded
  `TracerProvider.ForceFlush` before disposal, rather than relying on disposal's implicit flush timing.
  Pre-existing flake, unrelated to any feature shipped this session.

## [0.14.0] - 2026-08-27

### Changed

- **F-027 carter-route-modules**: every service's inline `Program.cs` route registrations reorganized
  into [Carter](https://github.com/CarterCommunity/Carter) `ICarterModule` classes — `BookingModule`,
  `CalendarModule`, `CustomerModule`/`MessageModule`/`NotificationModule`, `ProviderModule`,
  `ServicesModule`, `ProfessionModule`, `AuthModule`/`DeviceTokenModule`. Behavior-preserving: no route
  path, verb, auth requirement, or response shape changed — proven by the unchanged route-contract and
  OpenAPI-drift test suites. Carter's own `Validate<T>` FluentValidation integration was evaluated and
  not adopted; Validot (ADR-049) remains the sole validation DSL (ADR-055).

### Fixed

- Carter's default assembly-scanning module discovery picked up `ICarterModule` implementations across
  service boundaries inside `AgendaBuddy.IntegrationTests`' shared test process (it references all 7 API
  projects). Fixed by registering each service's modules explicitly via `AddCarter(configurator: ...)`
  rather than relying on scanning.

## [0.13.0] - 2026-08-27

### Added

- **F-023 token-revocation**: logging out now denylists the caller's own access token's `jti`
  (`POST /api/v1/auth/logout` gains an optional `accessToken` field, backward compatible), so it stops
  authenticating immediately across all seven services rather than staying valid for up to its full
  60-minute lifetime. The denylist is a new MongoDB collection (`revoked_tokens`) with a TTL index —
  cross-service, unlike the existing per-process `IDistributedCache` — checked once per authenticated
  request in `AuthenticationExtensions`' `OnTokenValidated` hook (ADR-054). No `aud` claim was
  introduced; `ValidateAudience` stays `false` (evaluated and rejected — see ADR-054).

## [0.12.0] - 2026-08-27

### Added

- **F-026 provider-subscription**: `POST`/`DELETE /api/v1/customers/{email}/subscriptions/{providerEmail}`
  and `GET /api/v1/customers/{email}/subscriptions` — idempotent subscribe/unsubscribe
  (`$addToSet`/`$pull`), ownership-gated to the customer named in the path. Writes both sides of the
  relationship: the customer's `subscribedProviderCollection` and the previously-unwired
  `ProviderEntity.SubscribedCustomerCollection` (ADR-053). Unsubscribing from a provider that no
  longer exists still succeeds for the customer's own cleanup. Mobile UI (`agenda-buddy-q9m`) and
  scoping `GET /api/v1/customers` to a provider's own subscribers (`agenda-buddy-tbs`) are
  deliberately out of scope.

### Fixed

- `AgendaBuddy.Customer.Api`'s DI registration never forwarded the concrete `ProviderService` to
  `IProviderService` — a latent runtime resolution failure for any handler typed against the
  interface, caught by this feature's own integration test before it shipped.

## [0.11.0] - 2026-08-27

### Added

- **F-022 password-reset-flow**: `POST /api/v1/auth/password-reset/request` and
  `/password-reset/confirm` — a single-use, 30-minute-expiry opaque token (same hash-only-storage
  pattern as the refresh token), anti-enumeration (`request` always returns `202`), and confirming
  clears any active session and lockout. `LoginAsync` now enforces `CredentialEntity.MustResetPassword`
  (`403 password_reset_required`) instead of silently ignoring it. No real email/SMS provider exists in
  this project (ADR-052, same category as ADR-038's non-charging payment gateway) — the reset token is
  logged for local development and mirrored into the existing in-app notification inbox as a secondary
  signal. Mobile UI is deliberately out of scope (`agenda-buddy-qe9`).

## [0.10.0] - 2026-08-27

### Fixed

- **F-025 booking-correctness**: `POST /api/v1/booking/appointments` accepted appointments booked
  backwards (`End` before `Start`), in the past, and overlapping another appointment already booked
  for the same provider — zero domain-invariant checks existed before this. Now enforced: `Start < End`
  at the Validot boundary, future-dating and the overlap check in `BookingAppointmentCommandHandler`.
  An appointment immediately adjacent to an existing one is not treated as an overlap. The overlap
  check is a documented, accepted read-then-insert race (ADR-051), not an atomic conditional write —
  see `docs/pdlc/design/booking-correctness/ARCHITECTURE.md`.

## [0.9.0] - 2026-08-27

### Changed

- **Rolled Booking's Clean Architecture pattern out to 5 more services** — Calendar, Customer, Provider, Services, Profession all now split into `<Service>.Api`/`Core`/`Domain`/`Infrastructure`, each with its own `mediator.Send` dispatch, `FluentResults.Result<T>`, and in-repo `DataResponse<T>` envelope. `RequestCollection`/`IRequestCollection` deleted for all 5. `Identity` is deliberately excluded — it never adopted the CQRS/`RequestCollection` shape the others share, so migrating it would introduce the pattern fresh, not replicate a proven one.
- **Every project in the solution — all 47 — now carries the `AgendaBuddy.` prefix**: folder, `.csproj`, solution reference, and internal C# namespace, matching the convention `AgendaBuddy.AppHost`/`ServiceDefaults`/`IntegrationTests` set at F-013. This includes a retroactive rename of Booking's own 5 projects (shipped last release) plus `Library`, `EventAndCommands`, `Kafka`, `Gateway`, `Identity`, and `MobileApp` — all pure renames with no behavior change.
- `AgendaBuddy.EventAndCommands` now holds zero command/query handler implementations — every service's handlers live in its own `*.Core` project.
- `DataResponse<T>` stays per-service, not extracted to a shared package, even with 6 total near-identical copies now — no cross-service code needs the same type, only the same shape.

### Fixed

- **Threat T-204**: `Customer`'s `AddCustomerCommandHandler` was still typed against the concrete `KafkaClient` class rather than `IKafkaClient` — the one `agenda-buddy-5og`-shaped copy of this bug F-018/F-019 never touched. Retyped; a real `InvalidOperationException` under live MediatR dispatch would have resulted otherwise.
- 2 genuinely dead command handlers deleted rather than migrated forward: `BookCalendarCommand` (Calendar) and `AddProfessionCommand` (Profession) — both unreachable, `NotImplementedException`-bodied, with no route or possible DI resolution path.
- A real cross-service namespace bug, unrelated to any rename: `ProblemDetailsServiceEndpointFilter.cs` lived under `namespace Customer.Extensions;` inside the *Profession* project, compiling only because of a compensating `global using` — fixed to the correct namespace.
- `AgendaBuddy.MobileApp`'s `CustomerApiService.ParsePagedCustomers` read `items` at the response root; wrapping `GET /customers` in `DataResponse<T>` moved it to `data.items` — fixed the parser and its test fixtures.
- A subtle Aspire bug found live: a service's `appsettings.json`/`appsettings.Development.json` `Kestrel:Endpoints` blocks got swapped during project scaffolding, silently zeroing Aspire's endpoint auto-detection for that resource (no compile error — an empty collection where one was expected). Restored from git history.
- `scripts/generate-openapi.sh`'s `project_dir()` mapping and `scripts/run-ios.sh`'s service arrays, each missing an entry for one or more renamed projects — found and fixed across several of this release's own commits.

### Known issues

- `agenda-buddy-02e` (Booking's Update/Cancel routes still on `MiniValidator`) and `agenda-buddy-cy2` (Booking's null-`EmailProvider` 500) — both pre-existing, Booking-scoped, unchanged by this release.
- Customer's `UpdateCustomerCommandHandler` still audits its not-found branch under the wrong event `Type` (a copy-paste defect, already ruled out of scope at F-018-T13) — preserved, not fixed, pinned by a test.
- Services' Add/Update handlers still skip an audit write on 2 specific branches — pre-existing, pinned by tests, not fixed.
- Mapster remains approved (ADR-049) with zero call sites across all 6 migrated services.

## [0.8.0] - 2026-08-27

### Changed

- **Booking split into a 4-project Clean Architecture pilot** (`Booking.Api`, `Booking.Core`, `Booking.Domain`, `Booking.Infrastructure`), replacing the single `Booking/` project the other 6 services still use. `Booking.Api` is now thin — endpoint/DI wiring only; command/query handlers moved to `Booking.Core`, dispatched via `IMediator` instead of hand-constructed by the old `RequestCollection`, which is deleted.
- Every Booking command/query handler now returns `FluentResults.Result`/`Result<T>` instead of a string-sniffed `"exception"`-prefixed convention.
- Introduced `DataResponse<T>` (`Booking.Domain/Responses/DataResponse.cs`) as the response envelope for Booking's routes — `Success`/`Data`/`Errors`, mapped from each handler's `Result<T>` at the API boundary.
- Started migrating Booking's request validation from `MiniValidator` to Validot's declarative `Specification<T>` DSL: `POST /appointments` (Book) and the two note-content routes now validate via Validot. `PUT`/`DELETE /appointments/` (Update, Cancel) still use `MiniValidator` — tracked as `agenda-buddy-02e`, not a silent gap.
- `UpdateAppointmentCommandHandler` and `CancelAppointmentCommandHandler` now depend on `IProviderService`/`IBookingService` rather than the concrete `ProviderService`/`BookingService`, making both independently unit-testable with Moq. `BookAppointmentCommandHandler` stays on the concrete `ProviderService`/`BookingService` — it calls `AppendAppointmentAsync`, which isn't on `IProviderService`, and adding it would be a `Library` change out of scope for this pass.

### Fixed

- `PUT /appointments/` (Update) no longer echoes the client-submitted `AppointmentStatus` back in the response body. The database write already ignored it (threat T-203), but the response previously reflected the caller's forged value rather than the actual persisted status — a caller could not tell from the response alone that their forged status was rejected.
- A dormant downcast bug in the three Booking command handlers moved this feature (`Book`/`Update`/`Cancel`): each took a concrete `KafkaClient?` constructor parameter, resolvable from DI only as `IKafkaClient` — any attempt to actually use it would have thrown at resolution time. The parameter was unused in all three and has been removed rather than fixed in place.
- Validot's originally-authored note-content spec (`NoteSpec`) used `.Required().NotEmpty()`, which accepts a whitespace-only string — a strictness regression relative to the inline `IsNullOrWhiteSpace` check it replaces. Fixed to `.Required().NotWhiteSpace()`, verified live against the Validot 2.6.0 assembly to match `!string.IsNullOrWhiteSpace(x)` exactly before wiring it into any route.

### Known issues

- 2 of Booking's 10 routes (`Update`, `Cancel`) still validate via `MiniValidator`, not Validot — `agenda-buddy-02e`.
- A `null` `EmailProvider` on `POST /appointments` passes both Validot and the ownership guard, then throws downstream during provider lookup, surfacing as an unhandled 500 rather than a 400 — `agenda-buddy-cy2`.
- Mapster is approved (ADR-049) for this line of work but has zero call sites yet.

[Unreleased]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.14.0...HEAD
[0.14.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.13.0...v0.14.0
[0.13.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.12.0...v0.13.0
[0.12.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.11.0...v0.12.0
[0.11.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.10.0...v0.11.0
[0.10.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.9.0...v0.10.0
[0.9.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.8.0...v0.9.0
[0.8.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.7.0...v0.8.0
