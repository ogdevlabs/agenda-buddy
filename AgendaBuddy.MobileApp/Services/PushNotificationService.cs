using System.Text;
using System.Text.Json;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.ViewModels;
#if FIREBASE
using Microsoft.Maui.Devices;
using Plugin.Firebase.CloudMessaging;
#endif

namespace AgendaBuddy.MobileApp.Services;

/// <summary>
/// Registers this device for push, announces a notification that arrives while the app is open, and routes a
/// tapped notification to the appointment it is about.
/// </summary>
/// <remarks>
/// <c>FIREBASE</c> is defined for both mobile TFMs, so this is live on Android and iOS alike.
/// <para>
/// <b>The two platforms ask for permission differently.</b> Android needs the <c>POST_NOTIFICATIONS</c>
/// runtime permission, requested through MAUI. iOS has no equivalent MAUI permission — authorization is
/// requested by Firebase itself inside <c>CheckIfValidAsync</c>, which calls
/// <c>UNUserNotificationCenter.RequestAuthorization</c>. So the iOS prompt appears during
/// <see cref="RegisterTokenAsync"/> rather than before it.
/// </para>
/// <para>
/// <b>Neither platform's OS draws a banner for a push that arrives while the app is on screen.</b> Android
/// hands a foreground message to the app instead of posting it to the tray, and iOS asks the app what to
/// present and shows nothing by default. So <see cref="SubscribeToEvents"/> also listens for the arrival and
/// draws the banner itself — without that, a notification landing while somebody is using the app is
/// completely silent, and the only trace is a row in a list they have to go and open. Everything about
/// deciding *whether* to announce an arrival lives in <see cref="InAppNotification.From"/> and
/// <see cref="OnNotificationReceived"/>, which are reachable from the <c>net10.0</c> test slice.
/// </para>
/// <para>
/// iOS additionally needs the <c>aps-environment</c> entitlement and
/// <c>UIBackgroundModes: remote-notification</c> — the first to receive anything at all, the second so a
/// data-carrying push reaches the app rather than only drawing a banner. Both are in the repo; the entitlement
/// is applied at codesign time only.
/// </para>
/// <para>
/// The send side is real: <c>FcmPushSender</c> speaks FCM HTTP v1 as soon as <c>Push:FirebaseProjectId</c> and
/// <c>Push:ServiceAccountJson</c> are configured, and its data payload key matches
/// <see cref="AppointmentIdentifierKey"/> — that pairing is what makes a tapped push open the appointment
/// rather than just the app.
/// </para>
/// <para>
/// ⚠️ <b>Nothing under <c>#if FIREBASE</c> is compiled by the local test gate.</b>
/// <c>/p:MobileWorkloads=false</c> builds only the <c>net10.0</c> slice, so a type error in here reaches CI
/// untouched. Build it on purpose:
/// <c>dotnet build AgendaBuddy.MobileApp/AgendaBuddy.MobileApp.csproj -f net10.0-android</c>.
/// </para>
/// </remarks>
public class PushNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecureStorageService _secureStorage;
    private readonly NotificationBadgeViewModel? _badge;
    private readonly IInAppAlertService? _alerts;

    /// <summary>
    /// The key the server puts the appointment identifier under. Must match
    /// <c>NotificationDispatcher</c>'s payload key, or a tapped notification opens the app and stops there.
    /// </summary>
    internal const string AppointmentIdentifierKey = "appointmentIdentifier";

    /// <summary>The banner's action. Opens the appointment when there is one, the inbox when there is not.</summary>
    internal const string ViewActionLabel = "View";

    /// <summary>
    /// Whether <see cref="SubscribeToEvents"/> has already run.
    /// </summary>
    /// <remarks>
    /// <see cref="InitializeAsync"/> is called on every sign-in, and a second subscription to the same events
    /// would draw two banners and count every arrival twice. This is a singleton, so the flag is per-process,
    /// which is the scope the platform events themselves have.
    /// </remarks>
    private bool _subscribed;

    /// <summary>The token last accepted by the server, so a rotation that resolves to the same value is a no-op.</summary>
    private string? _registeredToken;

    public PushNotificationService(
        IHttpClientFactory httpClientFactory,
        ISecureStorageService secureStorage,
        NotificationBadgeViewModel? badge = null,
        IInAppAlertService? alerts = null)
    {
        _httpClientFactory = httpClientFactory;
        _secureStorage = secureStorage;
        _badge = badge;
        _alerts = alerts;
    }

    public async Task InitializeAsync()
    {
        SubscribeToEvents();
        await RequestDisplayPermissionAsync();
        await RegisterTokenAsync();
    }

    /// <summary>
    /// Asks for permission to display notifications. Android only.
    /// </summary>
    /// <remarks>
    /// Required from Android 13 (API 33). Without a granted <c>POST_NOTIFICATIONS</c> the token registers
    /// normally and the OS drops every notification silently, which is the hardest version of this bug to
    /// diagnose: the server reports a successful send and nothing appears. A refusal is not treated as an
    /// error — a token is still worth registering, because the user may grant it later in system settings.
    /// <para>
    /// Scoped to <c>#if ANDROID</c> rather than <c>#if FIREBASE</c>: MAUI's <c>PostNotifications</c> permission
    /// is an Android concept, and on iOS authorization is requested by Firebase inside
    /// <c>CheckIfValidAsync</c>. Calling it here on iOS would rely on it throwing, which is not a contract
    /// worth depending on.
    /// </para>
    /// </remarks>
    internal static async Task RequestDisplayPermissionAsync()
    {
#if ANDROID
        try
        {
            if (await Permissions.CheckStatusAsync<Permissions.PostNotifications>() != PermissionStatus.Granted)
                await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
        catch (Exception)
        {
            // Not supported on this Android version. Pre-13 grants it at install time.
        }
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>
    /// Wires the three platform events this app has behaviour for: a notification tapped, one arriving while
    /// the app is open, and the device token rotating.
    /// </summary>
    /// <remarks>
    /// Without the tap subscription a tapped push opened the app on whatever screen it was last on and the
    /// appointment it named was never shown. Without the arrival subscription nothing at all happens for a
    /// notification that lands while the app is in the foreground, which is when a user is most likely to be
    /// waiting for one. Without the token subscription push dies silently for the rest of the session whenever
    /// FCM rotates the token — the app only registered one at sign-in, and nothing re-read it.
    /// </remarks>
    internal void SubscribeToEvents()
    {
        if (_subscribed) return;
        _subscribed = true;

#if FIREBASE
        try
        {
            // Lambdas rather than named handler methods, so the event-args types never have to be spelled out.
            // Naming one (FCMNotificationTappedEventArgs, in Plugin.Firebase.CloudMessaging.EventArgs) both
            // needs a second using and puts the identifier `EventArgs` in scope as a namespace, which shadows
            // System.EventArgs. Inferring them from the events' own delegates avoids both, and cannot break if
            // the package moves the types.
            CrossFirebaseCloudMessaging.Current.NotificationTapped += (_, e) =>
                OnNotificationTapped(e.Notification?.Data);

            CrossFirebaseCloudMessaging.Current.NotificationReceived += (_, e) =>
                OnNotificationReceived(e.Notification?.Title, e.Notification?.Body, e.Notification?.Data);

            CrossFirebaseCloudMessaging.Current.TokenChanged += (_, e) =>
                _ = OnTokenChangedAsync(e.Token);
        }
        catch (Exception)
        {
            // No Firebase on this device/build. Registration is best-effort; do not crash the app.
        }
#endif
    }

    /// <summary>
    /// Routes a tapped notification to the appointment its payload names.
    /// </summary>
    /// <remarks>
    /// Takes the payload rather than the platform event args, so the behaviour is reachable from the
    /// <c>net10.0</c> test slice — where the Firebase types do not exist at all. <c>FCMNotification.Data</c> is
    /// <c>IDictionary&lt;string, string&gt;</c>; FCM data payloads are string-to-string by protocol, which is
    /// also why <c>NotificationDispatcher</c> sends them that way.
    /// </remarks>
    internal void OnNotificationTapped(IDictionary<string, string>? data)
    {
        if (data is null
            || !data.TryGetValue(AppointmentIdentifierKey, out var appointmentId)
            || string.IsNullOrWhiteSpace(appointmentId))
        {
            return;
        }

        HandleNotificationTap(appointmentId);
    }

    /// <summary>
    /// Announces a notification that arrived while the app was in the foreground: the badge moves and a banner
    /// is drawn over whatever screen the user is on.
    /// </summary>
    /// <remarks>
    /// The badge is bumped straight away, because the arrival is itself the news that there is one more unread
    /// — waiting for a round trip would leave the count behind the banner announcing it — and then reconciled
    /// against the server, which is authoritative about a count this client cannot compute (a notification for
    /// this account may have been read on another device). A push carrying neither title nor body draws
    /// nothing: that is FCM's shape for a data-only message, and an empty banner is worse than none.
    /// </remarks>
    internal void OnNotificationReceived(string? title, string? body, IDictionary<string, string>? data)
    {
        var arrival = InAppNotification.From(title, body, data, AppointmentIdentifierKey);
        if (arrival is null) return;

        _badge?.Increment();

        // Not awaited: this is called from a platform event handler that cannot await, and neither the banner
        // nor the reconciliation is allowed to hold up delivery of the notification itself.
        _ = AnnounceAsync(arrival);
        _ = RefreshBadgeAsync();
    }

    /// <summary>
    /// Draws the in-app banner for an arrival, with a way through to what it is about.
    /// </summary>
    /// <remarks>
    /// Overridable so a test can observe the announcement without a MAUI presenter. The action goes to the
    /// appointment when the payload names one and to the inbox when it does not — a message notification
    /// carries no appointment, and dropping the button entirely for those would leave the reader a banner they
    /// can only dismiss.
    /// </remarks>
    protected internal virtual async Task AnnounceAsync(InAppNotification arrival)
    {
        if (_alerts is null) return;

        if (arrival.HasAppointment)
        {
            await _alerts.ShowAsync(
                arrival.BannerText,
                ViewActionLabel,
                () =>
                {
                    HandleNotificationTap(arrival.AppointmentIdentifier);
                    return Task.CompletedTask;
                });
            return;
        }

        await _alerts.ShowAsync(
            arrival.BannerText,
            ViewActionLabel,
            () =>
            {
                HandleOpenInbox();
                return Task.CompletedTask;
            });
    }

    /// <summary>Brings the badge back in line with the server after an arrival. Never throws.</summary>
    private async Task RefreshBadgeAsync()
    {
        if (_badge is null) return;

        try
        {
            await _badge.RefreshAsync();
        }
        catch (Exception)
        {
            // The local increment already moved the badge; a failed reconciliation leaves it there, which is
            // closer to the truth than clearing it.
        }
    }

    /// <summary>
    /// Re-registers the device when FCM rotates its token.
    /// </summary>
    /// <remarks>
    /// A rotated token makes every subsequent send answer 404 against the stored one, which reads server-side
    /// as "this device is gone". Without this the app only ever registered the token it held at sign-in, so a
    /// rotation silenced push until the next sign-in with nothing reporting it.
    /// </remarks>
    internal async Task OnTokenChangedAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token == _registeredToken) return;

        await PostTokenAsync(token, CurrentPlatform());
    }

    internal async Task RegisterTokenAsync()
    {
        string? token = null;

#if FIREBASE
        try
        {
            var messaging = CrossFirebaseCloudMessaging.Current;
            await messaging.CheckIfValidAsync();
            token = await messaging.GetTokenAsync();
        }
        catch (Exception)
        {
            return;
        }

        if (string.IsNullOrEmpty(token))
            return;
#else
        return;
#endif

#pragma warning disable CS0162
        await PostTokenAsync(token, CurrentPlatform());
#pragma warning restore CS0162
    }

    /// <summary>
    /// Tells the server to stop pushing to this device for the signed-in account.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called on sign-out, and it has to run <b>before</b> the JWT is cleared — the route authorises off the
    /// caller's own token, and there is no other way to say which account is giving the device up.
    /// </para>
    /// <para>
    /// Best-effort, like every other call here: a failed unregistration must not stop the user signing out. The
    /// server's own eviction on the next <c>UpsertAsync</c> is the backstop for that case, which is why both
    /// halves exist.
    /// </para>
    /// </remarks>
    internal virtual async Task UnregisterTokenAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
            await client.DeleteAsync("device-token");
        }
        catch (Exception)
        {
            // Signing out must not fail because of this. See the remarks above.
        }
        finally
        {
            // Forgotten unconditionally: the next sign-in on this device has to re-register, and remembering a
            // token whose server row may or may not still exist would suppress exactly that.
            _registeredToken = null;
        }
    }

    internal async Task PostTokenAsync(string token, string platform)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
            var body = new { token, platform };
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("device-token", content);

            // Remembered only on acceptance, so a rejected registration is retried by the next rotation rather
            // than suppressed as already-done.
            if (response.IsSuccessStatusCode)
                _registeredToken = token;
        }
        catch (Exception)
        {
            // Token registration is best-effort; do not crash the app
        }
    }

    /// <summary>The platform string the <c>device-token</c> route accepts. Only "android" and "ios" are valid.</summary>
    private static string CurrentPlatform()
    {
#if FIREBASE
        return DeviceInfo.Platform == DevicePlatform.iOS ? "ios" : "android";
#else
        // Unreachable on a real device: every caller is behind #if FIREBASE. The net10.0 slice needs a value
        // for PostTokenAsync's own tests, which pass the platform explicitly.
        return "android";
#endif
    }

    /// <summary>Virtual so a test can capture the navigation — Shell.Current has no test double.</summary>
    public virtual void HandleNotificationTap(string appointmentId)
    {
#if MOBILE
        _ = AppShell.NavigateToAppointmentAsync(appointmentId);
#endif
    }

    /// <summary>
    /// Opens the notification inbox, for an arrival that names no appointment.
    /// </summary>
    /// <remarks>Virtual for the same reason as <see cref="HandleNotificationTap"/>.</remarks>
    public virtual void HandleOpenInbox()
    {
#if MOBILE
        _ = Shell.Current.GoToAsync("notifications");
#endif
    }
}
