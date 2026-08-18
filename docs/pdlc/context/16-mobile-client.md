# 16 — Mobile Client (.NET MAUI)

**Files:** `MobileApp/` — 1 `MauiProgram.cs`, 1 `AppShell`, 3 `Infrastructure/`, 15 `Services/`, 9 `ViewModels/`, 9 `Views/` (+ 11 `.xaml`), 8 `Models/`, `Platforms/{Android,iOS}`.

`MobileApp` is the **only client** of the seven backend services. Delivered by F-012 `mobile-app` (PR #31) and restyled by F-012's UX redesign (PRs #32–#34).

**Coverage note:** `MauiProgram.cs`, `AppShell.xaml.cs`, `Infrastructure/*`, `Services/AuthService.cs`, `Services/UserSessionService.cs`, `Services/PushNotificationService.cs`, `Services/BookingApiService.cs`, `Services/CalendarApiService.cs`, `ViewModels/DashboardViewModel.cs`, and `ViewModels/CalendarViewModel.cs` were **read in full**. The remaining 5 API services, 7 ViewModels, all 11 `.xaml` views and their code-behind, `Models/*`, and `Platforms/*` were **not read**; claims about them are marked **Inference**.

---

## Build shape

Three target frameworks, selected by two MSBuild switches — full matrix in `07-build.md`.

| Slice | TFM | `UseMaui` | `MOBILE` | `FIREBASE` | Purpose |
|---|---|---|---|---|---|
| Android | `net10.0-android` | ✅ | ✅ | ✅ | Ships |
| iOS | `net10.0-ios` | ✅ | ✅ | ❌ | Ships |
| Fallback | `net10.0` | ❌ | ❌ | ❌ | Referenced by `MobileApp.Tests` |

⚠️ **The tested slice is not the shipped slice.** `MauiProgram.cs:1` and `AppShell.xaml.cs:1` are wrapped in `#if MOBILE`, so the `net10.0` assembly that `MobileApp.Tests` references contains **no DI registration and no Shell**. Every wiring defect below is therefore structurally untestable in the current setup (`11-testing.md`).

⚠️ `MobileApp.csproj:54` references `Library`, pulling `MongoDB.Driver`, `MongoDB.Bson`, `Stripe.net`, and `BCrypt.Net-Next` into the app bundle for the sake of `AppointmentStatus` and a few entity shapes. Size and attack-surface cost on end-user devices (`07-build.md`, `13-security.md`).

---

## DI composition (`MobileApp/MauiProgram.cs`)

Registration order and lifetimes at `:26-76`; full table in `02-entry-points.md`. The load-bearing parts:

```csharp
builder.Services.AddHttpClient("AgendaBuddyApi", client =>                      // :30
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]
                                 ?? "http://localhost:6036/");                  // :32  ⚠️
}).AddHttpMessageHandler<JwtDelegatingHandler>();                               // :33

builder.Services.AddHttpClient("AgendaBuddyApiNoAuth", client =>                // :36
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]
                                 ?? "http://localhost:6036/");                  // :38
});
```

Two named clients: `"AgendaBuddyApi"` (JWT attached) and `"AgendaBuddyApiNoAuth"` (login/register, before a token exists). Splitting them is correct.

Lifetimes: `ISecureStorageService` transient; `IUserSessionService` **singleton** (decoded JWT cached across pages); `PushNotificationService` singleton; all API services, ViewModels, and Views transient; `AppShell` singleton.

⚠️ **No `AddStandardResilienceHandler()` and no Polly** on either client — a single dropped connection surfaces as a failed page load with no retry.

---

## ⚠️ The central defect: the client cannot reach the backend

Three independent faults compound. Any one alone would break domain functionality; together they make every domain call unreachable.

### Fault 1 — one base address for seven services

The backend binds **seven different ports** (6030–6036, `01-api-surface.md`) and there is **no API gateway or reverse proxy** anywhere in the repo (`09-integrations.md`). A single `BaseAddress` cannot address seven processes.

### Fault 2 — every configured value is wrong

| Setting | Value | Anchor | Points at |
|---|---|---|---|
| `appsettings.json` | `https://localhost` | `MobileApp/appsettings.json:2` | port 443 — **no service** |
| `appsettings.Development.json` | `https://localhost:5001` | `MobileApp/appsettings.Development.json:2` | **no service** |
| Hardcoded fallback | `http://localhost:6036/` | `MauiProgram.cs:32,38` | **Identity** — and plaintext HTTP |

No service listens on 443 or 5001, and no service is configured for HTTPS at all (`13-security.md`). The only value that reaches a live process is the fallback — which points every domain call at the auth service.

### Fault 3 — every domain route path is wrong

The API services omit the `api/v1/` prefix and use singular resource names that no route group declares:

| Client call | Anchor | Backend route | Result |
|---|---|---|---|
| `GET booking?date=yyyy-MM-dd` | `Services/BookingApiService.cs:23` | *(Booking has **no GET**)* | ❌ 404 |
| `GET booking/{id}` | `Services/BookingApiService.cs:38` | *(none)* | ❌ 404 |
| `PUT booking/{id}` | `Services/BookingApiService.cs:53` | `PUT api/v1/booking/appointments/` | ❌ 404 |
| `GET calendar?from=&days=` | `Services/CalendarApiService.cs:23` | `GET api/v1/calendar/availability/{email}` | ❌ 404 |
| `POST api/v1/auth/login` | `Services/AuthService.cs:31` | `POST api/v1/auth/login` | ✅ |
| `POST api/v1/auth/register` | `Services/AuthService.cs:57` | `POST api/v1/auth/register` | ✅ |
| `POST device-token` | `Services/PushNotificationService.cs:64` | `POST /device-token` (Identity, root-mapped) | ✅ |

**Inference:** `CustomerApiService`, `MessagingApiService`, and `NotificationApiService` (not read) follow the same prefix-less convention — and messaging/notifications have **no backend routes at all** (`03-services.md`), so those calls could not succeed even with correct paths.

**Net effect:** only the three Identity routes resolve, and only because they happen to sit on port 6036 — the hardcoded fallback — and happen to carry the `api/v1/` prefix in the client. Auth works by coincidence.

Additionally: `PUT booking/{id}` sends `{ "status": "Confirmed" }` (`BookingApiService.cs:50`) while the real endpoint expects a full `AppointmentEntity` body (`Booking/Program.cs:122`). Even with the path corrected, the payload shape would not bind.

---

## ⚠️ How the defect stays invisible: the seed-data fallback

`DashboardViewModel.LoadAsync` (`ViewModels/DashboardViewModel.cs:62-102`):

```csharp
var results = await _bookingApiService.GetTodayAppointmentsAsync();   // :77

if (results.Count == 0)
    results = GenerateSeedAppointments();                            // :79-80  ⚠️
...
catch (HttpRequestException)
{
    var seed = GenerateSeedAppointments();                           // :91
    ...
}
```
where `GenerateSeedAppointments()` → `SeedDataProvider.GetForUser(_session.Email, _session.IsProvider, _session.IsCustomer)` (`:129-130`).

`CalendarViewModel` does the same — `GenerateSeedWeek()` → `SeedDataProvider.GetCalendarWeek(...)` (`ViewModels/CalendarViewModel.cs:134-135`).

The masking is complete because **`BookingApiService` swallows every non-success status**:
```csharp
if (!response.IsSuccessStatusCode)
    return new List<AppointmentSummary>();     // Services/BookingApiService.cs:27-28
```

So a `404` becomes an empty list, the empty list triggers the `results.Count == 0` branch at `:79`, and the UI renders `MobileApp/Services/SeedDataProvider.cs` fixtures — three providers, three customers, and hand-written appointments (`:8-40`) keyed to the `@agendabuddy.dev` seed accounts.

⚠️ **The fallback fires on "no data" as well as on "failure".** A user with genuinely zero appointments sees another provider's fabricated appointments — including fictitious client names, phone numbers (`SeedDataProvider.cs:18,26,34`), and session notes. That is a correctness and privacy-perception problem independent of the routing bug.

⚠️ **The `catch` is narrowed to `HttpRequestException`** (`:89`), so `TaskCanceledException` (timeout), `JsonException`, and `OperationCanceledException` propagate to the UI unhandled — while genuine HTTP failures are silently absorbed.

⚠️ **`ErrorMessage` is set to `string.Empty` at `:65` and never assigned a value in either path.** `HasError` (`:43`) is therefore always `false`, so the error banner — which `STATE.md` records as a UX F-002 fix ("All error banners include Try again button") — **can never appear on the dashboard**. Same in `CalendarViewModel` (`:143` reacts to `OnErrorMessageChanged`, which nothing triggers).

⚠️ **`IsEmpty` (`:45`) can also never be true**, because a zero-length result is replaced by seed data before `Appointments` is assigned. The empty-state UI is unreachable.

`git log` corroborates that this fallback was actively developed rather than left behind: `8aa4802 fix(mobile): unify seed data between dashboard and calendar` and `f69b837 fix(tests): update ViewModel tests for IUserSessionService and seed-data fallback`.

---

## Auth flow

`Services/AuthService.cs`:

| Operation | Line | Behaviour |
|---|---|---|
| `LoginAsync` | `:26` | `POST api/v1/auth/login` on the **NoAuth** client; on success stores `accessToken` under `"jwt"` and `refreshToken` under `"refresh_token"` in secure storage (`:43-44`), then `PushNotificationService.InitializeAsync()` (`:47`) |
| `RegisterAsync` | `:52` | Same shape (`:69-73`) |
| `LogoutAsync` | `:78` | Removes both keys locally (`:80-81`) |
| `GetTokenAsync` | `:85` | Reads `"jwt"` |

`Infrastructure/JwtDelegatingHandler.cs` attaches `Authorization: Bearer <token>` per request (`:20-22`) and on a `401` purges the token (`:28`) and raises `UnauthorizedAccess` (`:29`); `AppShell.xaml.cs:20-21` subscribes and routes to `//login`.

`Services/UserSessionService.cs` decodes the JWT payload client-side (`:41-67`) — base64url padding fixed at `:47-52` — exposing `Email` (from `sub`, `:58`), `Role` (full claim URI first, then short `role`, `:61-67`), and `IsProvider`/`IsCustomer` (`:23-24`).

⚠️ **The refresh token is stored and never used.** Grep confirms **no call to `api/v1/auth/refresh` anywhere in `MobileApp`** — `RefreshTokenKey` appears only at `AuthService.cs:14,44,70,81` (store and clear) and in two test assertions. So the 60-minute access-token lifetime becomes a hard logout: at minute 61 the next call 401s, `JwtDelegatingHandler` wipes the token, and the user is bounced to login while holding a valid 24-hour refresh token. Both ends of the refresh mechanism exist; the middle is unwired.

⚠️ **`LogoutAsync` never calls `POST api/v1/auth/logout`** (`:78-83`) — it only clears local storage, so the server-side refresh token stays valid for its full 24 hours after logout.

⚠️ **`JwtDelegatingHandler.UnauthorizedAccess` is a `static` event** (`:35`), subscribed in the `AppShell` constructor with no unsubscribe. Handlers accumulate across `AppShell` re-creation and leak between test cases.

⚠️ **`AuthService` discards every error body.** `:33-34` and `:59-60` check only `IsSuccessStatusCode`. Identity returns `{ error, message }` for 400/409 (`10-error-handling.md`) and none of it reaches the user — a failed registration is indistinguishable from a network error. **Inference:** `LoginViewModel` (62 lines, 6 tests) presents a generic message.

⚠️ **`PushNotificationService` is an optional constructor parameter** (`AuthService.cs:19`, `= null`) — so on the `net10.0` test slice it is absent and the `is not null` guards at `:46`, `:72` skip initialisation. Convenient for tests, but it means the DI graph and the tested graph differ.

---

## Navigation

`AppShell.xaml.cs`:
- Routes registered imperatively: `"messageThread"` → `MessageThreadPage`, `"appointmentDetail"` → `AppointmentDetailPage` (`:17-18`). **Inference:** the five tab roots plus `login` are declared in `AppShell.xaml` (not read).
- `UpdateForRoleAsync()` (`:24-28`) refreshes the session and retitles one tab: `ContactsTab.Title = _session.IsCustomer ? "Providers" : "Customers"` — matching `f32f368 feat(mobile): live search by name or service in providers view`.
- `NavigateToAppointmentAsync(id)` (`:30-33`) → `//dashboard/appointmentDetail?appointmentId={id}`, for push deep-links.

⚠️ **`UpdateForRoleAsync` has no caller found in the files read.** **Inference:** it is invoked from `LoginPage`/`App` code-behind (not read). If not, the contacts tab keeps its XAML default for the session.

⚠️ **`PushNotificationService.HandleNotificationTap` (`:72-77`) has no caller** — no FCM notification-tap subscription exists. The deep-link path is dead (`09-integrations.md`).

⚠️ **Role-based tab visibility is driven by an unverified client-side token decode** (`13-security.md`). Cosmetic only; the server is authoritative — but the server's PII endpoints are anonymous anyway (`01-api-surface.md`).

`git log` records real navigation bugs found late: `01b44ec fix(mobile): correct logout route from //LoginPage to //login to prevent crash`, `6017e59 fix(mobile): fix messages crash`.

---

## ViewModels

Nine, all `partial class : ObservableObject` using `CommunityToolkit.Mvvm` 8.3.2 source generators (`[ObservableProperty]`, `[RelayCommand]`). 1,151 lines total. Read: `DashboardViewModel` (147), `CalendarViewModel` (partial). Not read: the other seven.

| ViewModel | Lines | Tests |
|---|---:|---:|
| `NotificationsViewModel` | 183 | 5 |
| `CustomersViewModel` | 177 | 5 |
| `DashboardViewModel` | 147 | 5 |
| `CalendarViewModel` | 144 | 2 |
| `AppointmentDetailViewModel` | 138 | 9 |
| `MessagingViewModel` | 129 | 3 |
| `MessageThreadViewModel` | 86 | 4 |
| `RegisterViewModel` | 85 | — |
| `LoginViewModel` | 62 | 6 |

⚠️ **`RegisterViewModel` has no test file** — the only ViewModel with zero coverage, and it drives account creation.

Observed patterns in the two read: client-side pagination (`PageSize = 4`, `DashboardViewModel.cs:14,120-127`), `IsLoading`/`ErrorMessage`/`HasError`/`IsEmpty` state quartet, `partial void On<X>Changed` hooks to re-raise computed properties (`:138-146`), and expand/collapse toggles mutating the model object (`:133-135`).

⚠️ **`DateTime.Now` (local) is used for greeting and date comparisons** (`:53`, `:68`, `:85`, `CalendarViewModel.cs:113`) while appointments come from a UTC-persisted backend (`05-data-model.md`). `TodayCount` at `:85` compares `a.ScheduledAt.Date == DateTime.Today` — a timezone-dependent count. Mirrors the same UTC/local confusion in `SupportTools.GetThirtyDaysCalendarAvailability` (`04-data-access.md`).

⚠️ **`ToggleAppointment` mutates `AppointmentSummary.IsExpanded` directly** (`:133-135`). **Inference:** `AppointmentSummary` must implement change notification for the UI to update; `Models/` was not read, so this is unverified.

---

## Views

11 `.xaml` + code-behind: `LoginPage`, `RegisterPage`, `DashboardPage`, `CalendarPage`, `CustomersPage`, `MessagingPage`, `MessageThreadPage`, `NotificationsPage`, `AppointmentDetailPage`, plus `App.xaml` and `AppShell.xaml`. **None read in this scan.**

⚠️ **Zero UI test coverage** — no Appium, no MAUI UITest, no snapshot testing (`11-testing.md`). The entire F-012 UX redesign (PRs #31–#34: iOS-immersive restyle of dashboard, calendar, customers, messages, notifications; tab-bar icons; pinned Sign Out pill) shipped with no automated verification.

Resources: `Resources/AppIcon/` (2 svg), `Resources/Images/` (5 tab icons), `Resources/Splash/splash.svg`. ⚠️ `MobileApp.csproj:66` declares `<MauiFont Include="Resources\Fonts\*" />` but **no `Resources/Fonts/` directory exists** in the repo — an empty glob.

---

## Platform-specific

`Platforms/Android/`: `AndroidManifest.xml`, `MainActivity.cs`, `MainApplication.cs`, `Resources/values/colors.xml`.
`Platforms/iOS/`: `AppDelegate.cs`, `Program.cs`, `Info.plist`, `Resources/PrivacyInfo.xcprivacy`.

**None read in this scan.** `PrivacyInfo.xcprivacy` is present, which App Store review requires.

⚠️ **No `google-services.json` and no `GoogleService-Info.plist`** — Firebase cannot initialise, and CI does not inject them (`09-integrations.md`).

⚠️ **iOS is excluded from the Firebase package** (`MobileApp.csproj:48`), so `FIREBASE` is never defined for iOS and `RegisterTokenAsync` returns early at `PushNotificationService.cs:48`. **iOS devices never register for push**, even though the server accepts `"ios"` (`Identity/Program.cs:159`) and `PushNotificationService.cs:38` contains iOS detection code that can never execute.

---

## Summary of mobile-side findings

| # | Finding | Severity |
|---|---|---|
| 1 | Every domain API path omits `api/v1/` and targets nonexistent routes/verbs | **Blocking** |
| 2 | A single `ApiBaseUrl` cannot address 7 ports; no gateway exists | **Blocking** |
| 3 | All three configured/fallback base URLs point at no service (or at Identity) | **Blocking** |
| 4 | Seed-data fallback silently masks 1–3, so the app *looks* functional | **High** — hides the blockers |
| 5 | Refresh token stored but never used → hard logout at 60 min | High |
| 6 | `ErrorMessage` never assigned → error banner and empty state unreachable | High |
| 7 | Seed fallback fires on legitimate "no data", showing fabricated client PII | High |
| 8 | `LogoutAsync` never calls the server; refresh token stays valid 24 h | Medium |
| 9 | iOS never registers for push; no Firebase config files committed | Medium |
| 10 | `net10.0` test slice excludes `MauiProgram`/`AppShell`, so wiring is untestable | Medium |
| 11 | Static `UnauthorizedAccess` event never unsubscribed | Low |
| 12 | `Library` reference ships Stripe + BCrypt + Mongo driver to devices | Low |
| 13 | `DateTime.Now` vs UTC backend in counts and greetings | Low |
| 14 | `RegisterViewModel` untested; zero UI tests | Low |
| 15 | Empty `Resources/Fonts/*` glob | Trivial |

**Findings 1–4 together are the most consequential defect in the product.** F-012 `mobile-app` is marked Shipped, and the client is not integrated with the backend — it renders local fixtures. The backend-side counterpart (no gateway, port-per-service, `api/v1/` prefix) is documented in `01-api-surface.md` and `09-integrations.md`.
