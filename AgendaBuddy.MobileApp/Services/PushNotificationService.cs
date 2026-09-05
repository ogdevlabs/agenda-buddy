using System.Text;
using System.Text.Json;
using AgendaBuddy.MobileApp.Infrastructure;
#if FIREBASE
using Microsoft.Maui.Devices;
using Plugin.Firebase.CloudMessaging;
#endif

namespace AgendaBuddy.MobileApp.Services;

/// <summary>
/// Registers this device for push and routes a tapped notification to the appointment it is about.
/// </summary>
/// <remarks>
/// ⚠️ <b>Android only.</b> <c>FIREBASE</c> is defined for <c>net10.0-android</c> only, because
/// <c>Plugin.Firebase.CloudMessaging</c> is excluded for iOS in the csproj — iOS needs a
/// <c>GoogleService-Info.plist</c>, an APNs key uploaded to Firebase and a push entitlement, none of which can
/// be provisioned from the repository. On iOS every method here returns without doing anything.
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

    /// <summary>
    /// The key the server puts the appointment identifier under. Must match
    /// <c>NotificationDispatcher</c>'s payload key, or a tapped notification opens the app and stops there.
    /// </summary>
    internal const string AppointmentIdentifierKey = "appointmentIdentifier";

    public PushNotificationService(IHttpClientFactory httpClientFactory, ISecureStorageService secureStorage)
    {
        _httpClientFactory = httpClientFactory;
        _secureStorage = secureStorage;
    }

    public async Task InitializeAsync()
    {
        SubscribeToTaps();
        await RegisterTokenAsync();
    }

    /// <summary>
    /// Wires the tapped-notification event to navigation. Without this the handler below is unreachable: a
    /// tapped push opened the app on whatever screen it was last on and the appointment it named was never shown.
    /// </summary>
    internal void SubscribeToTaps()
    {
#if FIREBASE
        try
        {
            // A lambda rather than a named handler method, so the event-args type never has to be spelled out.
            // Naming it (FCMNotificationTappedEventArgs, in Plugin.Firebase.CloudMessaging.EventArgs) both
            // needs a second using and puts the identifier `EventArgs` in scope as a namespace, which shadows
            // System.EventArgs. Inferring it from the event's own delegate avoids both, and cannot break if the
            // package moves the type.
            CrossFirebaseCloudMessaging.Current.NotificationTapped += (_, e) =>
                OnNotificationTapped(e.Notification?.Data);
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

    internal async Task RegisterTokenAsync()
    {
        string? token = null;
        string platform = "android";

#if FIREBASE
        try
        {
            var messaging = CrossFirebaseCloudMessaging.Current;
            await messaging.CheckIfValidAsync();
            token = await messaging.GetTokenAsync();
            platform = DeviceInfo.Platform == DevicePlatform.iOS ? "ios" : "android";
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
        await PostTokenAsync(token, platform);
#pragma warning restore CS0162
    }

    internal async Task PostTokenAsync(string token, string platform)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
            var body = new { token, platform };
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            await client.PostAsync("device-token", content);
        }
        catch (Exception)
        {
            // Token registration is best-effort; do not crash the app
        }
    }

    /// <summary>Virtual so a test can capture the navigation — Shell.Current has no test double.</summary>
    public virtual void HandleNotificationTap(string appointmentId)
    {
#if MOBILE
        _ = AppShell.NavigateToAppointmentAsync(appointmentId);
#endif
    }
}
