# Changelog

All notable changes to this project are documented in this file, in [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) style.

## [Unreleased]

## [0.23.0] - 2026-09-12

### Added

- Providers and customers now share an Appointments screen with counted Scheduled, Done, and Cancelled
  segments (`Programadas`, `Realizadas`, and `Canceladas` in Spanish).
- Done and Cancelled initially retrieve the five newest records and use authenticated server-backed pagination
  for older pages. Providers retain the dedicated Manage calendar entry point for working hours and time off.

### Changed

- The user-facing Calendar tab and page are now named Appointments in English and Citas in Spanish.
- Pending reschedules are grouped and displayed by their proposed time, while the agreed original time remains
  available on appointment detail until the proposal is answered.

## [0.22.2] - 2026-09-12

### Changed

- Reconciled the PDLC constitution, intent, overview, roadmap, and live state with the shipped .NET 10,
  Aspire, Gateway, broker-free messaging, mobile, payment, authentication, and release architecture.
- Restored the missing `v0.21.0` through `v0.22.1` release chronology and made Beads the explicit authority
  for live backlog and claim state.

### Fixed

- Integration booking fixtures now move weekend candidate slots to Monday, matching the default working week
  and preventing the suite from failing only when run on Saturday or Sunday.

## [0.22.1] - 2026-09-11

### Fixed

- Password recovery now uses absolute Shell routes for top-level pages, preventing forgot-password navigation
  from crashing. A structural test guards the route contract.

## [0.22.0] - 2026-09-11

### Added

- Cold-start sessions restore through secure rotating refresh tokens and native device biometrics without
  storing passwords.
- Email-verification messages include richer bilingual content and platform configuration coverage.

## [0.21.2] - 2026-09-11

### Fixed

- Stripe onboarding deferral is restricted to local development; non-local payment configuration continues
  to fail closed.

## [0.21.1] - 2026-09-10

### Fixed

- Email verification supports a code fallback when a platform callback cannot be opened.

## [0.21.0] - 2026-09-10

### Added

- Registration now requires email ownership verification before issuing an authenticated session, with
  bilingual confirmation email, local iOS capture flow, and configuration-driven platform links.

## [0.20.0] - 2026-09-10

### Added

- A guarded, manual, dev-only workflow can invalidate all registered development users after an incompatible
  account/onboarding change. It stops every Container App, waits for replicas to terminate, clears all user data
  from `agenda_buddy` and `IdentityDb` while preserving the profession catalogue and indexes, verifies the purge,
  and restores the environment only when its working-hours schedule requires it.

### Changed

- Access tokens now carry an issued-at claim, and the shared MongoDB revocation store supports a durable global
  cutoff. A dev data reset immediately rejects every pre-reset access token instead of waiting for its one-hour
  expiry; users registered after the reset receive valid new tokens.

### Fixed

- Dev user-data reset now reuses the existing `dev-env-stop.yml` workflow and allows Azure Container Apps up to
  15 minutes to drain replicas before failing closed. The first live reset proved the original fixed 12-attempt
  window was too short: six replica records still existed when it expired, so no data was deleted.
- Global reset cutoffs are written to both `agenda_buddy` and `IdentityDb`, matching the database each service's
  authentication middleware reads. Independent post-reset verification caught the missing Identity cutoff before
  release tagging; all user documents had already been removed successfully.
- Long replica drains now refresh Azure OIDC authentication between bounded polling slices. A live drain reached
  zero replicas after 12 minutes, but the original login assertion expired before the final Azure query; no MongoDB
  write ran on that failed pass.

## [0.19.2] - 2026-09-10

### Fixed

- Corrected the embedded Python indentation in the deployment's optional-Stripe-secret branch, allowing the
  fail-closed sentinel configuration to reach `azd deploy`.
- Strengthened the workflow regression test to assert the executable sentinel block's indentation. The corrected
  main run deployed successfully to Azure Container Apps and passed the Gateway health smoke test.

## [0.19.1] - 2026-09-10

### Fixed

- Optional Stripe secrets now use a nonempty fail-closed sentinel because Azure Container Apps rejects empty
  secret values. A deployment without Stripe remains healthy while financial operations stay unavailable.
- Dev redeploy restoration now runs after a failed deployment when the schedule requires the environment to be
  active, and the workflow report no longer claims failed code reached dev.

## [0.19.0] - 2026-09-10

### Added

- Stripe Connect marketplace payments: customer setup-mode Checkout, provider Express onboarding, manual
  authorization on provider confirmation, 90/10 destination-charge allocation, completion capture, cancellation
  release/refund, and signed replay-safe webhook reconciliation.
- English and Spanish payment-method and payout-account onboarding/Profile views with iOS and Android callback
  links, plus complete provider-report metric translation.

### Changed

- Booking prices are immutable server-derived minor-unit snapshots. The caller-priced payment POST and mobile
  amount form were removed.
- Non-local environments without Stripe configuration now fail financial operations closed instead of storing a
  synthetic successful payment.

## [0.18.0] - 2026-09-09

### Added

- Providers can dismiss the profession-search keyboard by tapping outside the field or pressing the keyboard's
  Search action. The query and selected professions remain intact, and the pinned Save action becomes reachable
  again on iOS.

## [0.17.0] - 2026-09-09

### Added

- Complete English and Latin-American Spanish (`es-MX`) localization for the AgendaMe mobile app: 591 paired
  resource keys, persisted device-local language selection, immediate Shell reconstruction, and OS app-language
  declarations for iOS and Android.
- Localized display names for all 115 seeded professions while preserving canonical API identities.
- Localized in-app and lock-screen notification presentation for all 12 notification types; device-token
  registration now carries an optional locale with backward-compatible English fallback.
- Resource-backed English/Spanish Terms and Privacy Policy, plus structural parity, disclosure, XAML-literal,
  profession-map, and resource-completeness tests.

### Changed

- Mobile errors, dates, times, currency, weekdays, status labels, counts, accessibility copy, and role-dependent
  navigation now render from the active culture instead of hardcoded English.
- App navigation routes and the static unauthorized-session handler initialize once outside the transient Shell,
  allowing safe language changes without duplicate handlers or losing the signed-in session.

### Known Issues

- Qualified legal and native Latin-American Spanish review is still pending in
  `docs/legal/SPANISH_LEGAL_REVIEW.md`; release was explicitly authorized with this risk disclosed and tracked as
  `agenda-buddy-hff`.

### Changed

- **F-031**: a change to the **deploy machinery itself** now triggers a dev deploy —
  `.github/workflows/{deploy,dev-redeploy,dev-env-power}.yml` are in the `deployable` filter. They were not,
  which is why the CI 358 fix (a concurrency self-deadlock and a stripped OIDC permission, both of which failed
  the job before it started and produced no log) merged to `main` **without the deploy path running once** — and
  why the defects reached `main` by the same route. A change to how deploying works now proves itself on the
  merge that makes it, rather than days later on whatever unrelated backend change happens to deploy next. The
  cost is accepted: a comment-only edit to one of those three files spends a full Terraform + azd run and eight
  container builds. `dotnet.yml` is deliberately excluded — the stage inside it is a handful of
  `if:`/`needs:`/`uses:` lines guarded by `AutoDeployPathFilterTest`, and including it would make every CI edit
  of any kind deploy.


### Added

- **F-031**: `.github/workflows/dev-env-drift.yml` — **"dev is running `main`" is now a measured fact.** A
  successful deploy stamps its commit onto the resource group (`deployedSha`, **after** the smoke test, so it
  means *deployed and serving*), and an hourly check reads it back, diffs it against `main`, and calls
  `dev-redeploy.yml` when anything material differs. Nothing previously recorded what a deployed environment
  was running — CLAUDE.md's advice was to infer it from `deploy.yml`'s run history, which is how dev sat three
  days behind `main` and produced three bug reports against already-fixed behaviour.
  - **Why a check and not a better path filter.** `deploy-dev` fires off the `deployable` **allowlist**, which
    can only be as complete as whoever last edited it and fails in the silent direction: a path nobody listed
    just stops deploying, with every job green. It also cannot notice a deploy that failed after CI went green
    (exactly what happened on the first real redeploy), an environment stopped by hand, or a dispatch that
    shipped an old branch. Comparing deployed-to-`main` catches all four with one mechanism, and costs a
    single `az group show` when they agree. **The filter is demoted to a latency optimisation** — a miss now
    costs up to an hour of staleness instead of going unnoticed indefinitely.
  - **Inert denylist, not the `deployable` allowlist.** Reusing that list would reproduce its blind spot
    exactly, with the drift check confirming nothing needs deploying while dev ran stale code. Everything is
    therefore deployable *unless* it is on a short list of paths that provably cannot change what a container
    serves (`docs/`, `bruno/`, `.github/`, `scripts/`, `compose/`, `.beads/`, `*.md`, `AgendaBuddy.MobileApp*/`,
    `*.Tests/`, `AgendaBuddy.IntegrationTests/`, compose files). **A new project is deployable the moment it
    exists**, with nobody registering it anywhere. An absent or unrecognisable stamp is read as drift, never
    as current — unknown means redeploy. `DevEnvDriftTest` (39 tests) asserts no AppHost-declared service and
    no shared project can be written onto the inert list, and validates the pattern under both .NET's regex
    engine and `grep -E`, which is what the workflow actually runs.

### Fixed

- **F-031**: `AutoDeployPathFilterTest.TheSequenceAndTheDeployDoNotShareAConcurrencyGroup` compared the
  concurrency groups as **raw text**, so `redeploy-${{ inputs.environment || 'dev' }}` and
  `redeploy-${{ inputs.environment }}` read as different groups while both resolve to `redeploy-dev` and
  deadlock identically. Found by negative-testing the new drift check against it: renaming the drift group to
  the redeploy one left the test green. Both tests now normalise `${{ … }}` away before comparing. This is the
  guard that was supposed to stop run 358's defect class recurring, and it would not have.
- **F-031**: `azd deploy` could never have worked without `azd provision`, so `provision: false` — the mode
  **both** `dev-redeploy.yml` and .NET CI's `deploy-dev` stage use — had never once succeeded. `.azure/` is
  gitignored and a runner is ephemeral, so the workflow's `azd env new` created an *empty* azd environment on
  every run and the `|| azd env select` fallback never fired. An empty environment holds none of the outputs
  `azd provision` writes into `.azure/<env>/.env`, and the container registry endpoint is one of them: azd knew
  the entire app model but not where to push an image, and died on the first service with
  `could not determine container registry endpoint`. It was invisible because **the only deploy in this
  repository's history that ever went green ran with `provision: true`**, which writes those outputs as a side
  effect — the defect was absent from the mode a human dispatches by hand and fatal in the two that run
  unattended. A `provision: false` run now calls `azd env refresh` first, re-reading the outputs from the
  environment's last real deployment in Azure, and then **asserts `AZURE_CONTAINER_REGISTRY_ENDPOINT` actually
  arrived** rather than trusting the refresh's exit code — a refresh that reports success while producing no
  endpoint otherwise reproduces the original failure 40 packaging seconds later, under a message that blames
  docker options. Chosen over azd remote state (`state.remote` in `azure.yaml`), which would need a seeding
  `provision: true` run before it held anything, whereas Azure already has the deployment.
- **F-031**: `ADeployWithoutAProvisionRefreshesTheAzdEnvironmentFirst` asserted the ordering by searching for
  the string `azd env refresh`, which also appears in the comment explaining the step — so moving the step
  *below* `azd deploy` left the test passing. Anchored on the step header instead. Same defect class as the
  `id-token` assertion below: a guard that matches prose rather than structure.
- **F-031**: the `deploy-dev` stage failed on its first ever run (CI 358 on `main`), and did so in a way that
  showed **every visible job green and the run red** — two independent defects, neither of which produced a log
  or a check run because the job never started.
  - **Concurrency self-deadlock.** `dev-redeploy.yml` and `deploy.yml` declared the *same* group
    (`deploy-<env>`). The sequence acquired it and then called the deploy, which requested the same group — and
    it could never be released, because releasing it was what the parent was waiting on the child to allow.
    `dev-env-power.yml` declares no concurrency at all, which is precisely why the `stop` stage succeeded while
    `deploy` never began; that contrast is what identified it. The sequence now uses `redeploy-<env>`, so two
    redeploys still serialise against each other and the inner deploy still serialises against a directly
    dispatched `deploy.yml`.
  - **`id-token: write` was silently stripped.** It was declared on the `deploy-dev` job, which looks
    sufficient and is not: a job may narrow the workflow-level grant but cannot escalate beyond it, and
    `id-token` is only available where the workflow level allows it. The run proved it — the job-level block
    took effect for `contents` (workflow-level `pull-requests: read` was gone from the nested job) while
    `id-token` vanished, leaving `Contents: read, Metadata: read` and no way to exchange an OIDC token with
    Azure. Now granted at workflow level, which is the only option GitHub offers.
- **F-031**: `AutoDeployPathFilterTest`'s OIDC assertion checked that the string `id-token: write` appeared
  *anywhere* in `dotnet.yml` — so it passed while the permission was ineffective. It now parses the
  workflow-level block specifically, and a new assertion requires the two concurrency groups to differ. Both
  were verified by reintroducing each defect and confirming the test fails.


### Added

- **F-031 auto-deploy-dev**: `.NET CI` gained a final `deploy-dev` stage, so **the dev environment runs the
  backend code that is on `main`**. It fires on a push to `main` when the pipeline is not failing and the
  `changes` job's new `deployable` filter matched, and calls `dev-redeploy.yml` — stop → deploy → restore —
  which is also dispatchable; `deploy.yml` keeps its own dispatch, the only way to run with `provision: true`.
  Three entry points, one implementation. Written first as a separate `workflow_run` workflow and rewritten as a
  pipeline stage: the standalone version had to restate the deployable-path list in a second file, and that
  duplicate fails silently — a renamed service the copy misses simply stops being deployed. On by default with
  `AUTO_DEPLOY_DEV=false` as the brake, and the polarity is deliberate: only the literal `false` disables it, so
  an unset or mistyped variable deploys rather than doing nothing quietly (ADR-065).
- **F-031**: push credentials now reach a deployed environment, which they never had. `AppHostWiring` declared
  the two push parameters only when a value was already in `builder.Configuration` — never true while the AppHost
  is being *published*, because azd supplies parameter values at provision time and not to the app model during
  manifest generation. So the condition was always false in the Cloud shape, the parameters never entered the
  generated Bicep, and **every deployed environment resolved `UnconfiguredPushSender`: a backend that could not
  push, with nothing anywhere reporting why.** The Cloud shape now declares them unconditionally as
  `resendApiKey` does, while the Local shape keeps the configuration check that protects against ISSUE-001's
  silent `ValueMissing` parking. Terraform stores both as optional Key Vault secrets
  (`push-firebase-project-id`, `push-service-account-json`) and `deploy.yml` maps them to azd parameters.

### Changed

- **F-031**: `.NET CI`'s `cancel-in-progress` is now `false` on `main` — it was unconditionally `true`, and its
  own comment conceded main pushes "never cancel each other's… in practice". That stopped being good enough once
  a run can end in a deploy: cancelling a superseded run would cancel it mid-`terraform apply` or
  mid-`azd deploy`. PRs keep the superseding behaviour, which is why the group exists.
- **F-031**: an optional deploy secret that is absent is now passed to azd as an **empty string rather than
  omitted**. A parameter the app model declares but supplies no value for fails `azd provision --no-prompt`,
  while empty is exactly what `PushOptions`/`EmailOptions` read as "not configured" — so an environment with no
  push credentials deploys and simply logs that push is off.

- **F-030 contact-avatars**: `AvatarCatalog` (`AgendaBuddy.Library`) names 24 avatars, assigned at random when a
  Provider or Customer profile is created (`avatar_id` on both entities) and derived deterministically from the
  email where a row has none — so this ships with no data migration and no account renders as a blank circle.
  Replaces the letter-in-a-circle on the contacts and messaging lists, which was the name again, smaller, and
  made every contact sharing a first letter look identical down a list. Abstract geometric marks rather than
  illustrated faces, and SHA-256 rather than `string.GetHashCode()` (which .NET randomises per process, so a
  hash-code-derived avatar would change on every launch) — see ADR-064. Assets are generated by
  `scripts/generate-avatars.py`; `AvatarCatalogTest` fails if any catalogue id has no committed asset, because
  a missing one renders as an empty circle rather than as an error.

### Fixed

- **F-032**: a half-created account was unrecoverable from inside the app, and registration actively told users
  to do the one thing that could not work. On a failed profile write it says "add them from Account to finish
  setting up", but `AccountViewModel.SaveProfileAsync` called `UpdateProfileAsync` only — which reads the profile
  before writing and gives up when the read 404s, against a route that answers `NotFound` because
  `FindOneAndUpdateAsync` never upserts (ADR-032, deliberate and load-bearing elsewhere). So every Save answered
  "try again", and trying again could not help: credential valid, role claim correct, sign-in working, no profile,
  no route to one. Save now falls back to creating the profile when the update fails — update is still attempted
  first, because that is the common case and creating first would hit the create handlers' name-based duplicate
  check on every ordinary edit. This also makes every account created before profile creation was wired at all
  repairable (`agenda-buddy-1hk.4`).
- **F-032**: a provider's timezone is now recorded at registration, not only when they happen to open Account.
  A provider's availability window is generated in *their* zone, so one who never opened that screen kept UTC
  hours and every slot offered to their customers was wrong by their offset (part of `agenda-buddy-9v6`).

### Added

- **F-032**: `RegisterViewModelTests` — the P0 that registration created an Identity credential and no domain
  profile (`agenda-buddy-fg5`) was fixed on 2026-09-02 and shipped with **no regression coverage at all**. 21
  tests now pin it: both roles create the right profile and never the other, the role string sent to Identity is
  exact, a failed credential creation attempts no profile, names are trimmed, an omitted phone is `null` rather
  than `""`, the pre-flight guards create nothing, and a failed timezone sync cannot fail registration.
  `AccountProfileRecoveryTests` (9) covers the recovery above.

- **F-030**: the Dashboard drew a blank white band under the brand header that only disappeared after a manual
  pull-to-refresh. `RefreshView.IsRefreshing` was bound straight to `IsLoading` and `OnAppearing` fires
  `LoadCommand`, so arriving on the page started a refresh nobody asked for; on iOS that begins a
  `UIRefreshControl` animation and inserts its content inset, and the inset was left behind when `IsLoading`
  went false before the control finished animating in (a pull resets the control, which is why pulling cleared
  it). Fixed with a dedicated `IsRefreshing` that only `RefreshCommand` drives, as `NotificationsPage` already
  had. New `RefreshViewBindingTest` fails any view that binds `IsRefreshing` to `IsLoading`/`IsBusy` — the two
  flags look interchangeable, the symptom is iOS-only, and nothing about the binding reads as wrong.

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

[Unreleased]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.18.0...HEAD
[0.18.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.17.0...v0.18.0
[0.17.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.16.0...v0.17.0
[0.16.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.15.0...v0.16.0
[0.15.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.14.0...v0.15.0
[0.14.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.13.0...v0.14.0
[0.13.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.12.0...v0.13.0
[0.12.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.11.0...v0.12.0
[0.11.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.10.0...v0.11.0
[0.10.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.9.0...v0.10.0
[0.9.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.8.0...v0.9.0
[0.8.0]: https://github.com/ogdevlabs/agenda-buddy/compare/v0.7.0...v0.8.0
