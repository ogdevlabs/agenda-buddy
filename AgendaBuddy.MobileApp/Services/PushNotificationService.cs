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
/// ⚠️ <b>Android only, and the server end is not built.</b> Two independent gaps, both outside what code alone
/// can close:
/// <list type="bullet">
/// <item><c>FIREBASE</c> is defined for <c>net10.0-android</c> only, because
/// <c>Plugin.Firebase.CloudMessaging</c> is excluded for iOS in the csproj — iOS needs a
/// <c>GoogleService-Info.plist</c>, an APNs key uploaded to Firebase and a push entitlement, none of which can
/// be provisioned from the repository. On iOS every method here returns without doing anything.</item>
/// <item>Nothing in the backend sends a push. <c>IPushSender</c> resolves to
/// <c>UnconfiguredPushSender</c> until FCM credentials exist, so a token registered here is stored and read but
/// never messaged.</item>
/// </list>
/// The token round trip and the tap handler are real and wired, so both gaps are credentials rather than code.
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
            CrossFirebaseCloudMessaging.Current.NotificationTapped += OnNotificationTapped;
        }
        catch (Exception)
        {
            // No Firebase on this device/build. Registration is best-effort; do not crash the app.
        }
#endif
    }

#if FIREBASE
    private void OnNotificationTapped(object? sender, FCMNotificationTappedEventArgs e)
    {
        if (e.Notification.Data is not null
            && e.Notification.Data.TryGetValue(AppointmentIdentifierKey, out var appointmentId)
            && !string.IsNullOrWhiteSpace(appointmentId))
        {
            HandleNotificationTap(appointmentId);
        }
    }
#endif

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

    public void HandleNotificationTap(string appointmentId)
    {
#if MOBILE
        _ = AppShell.NavigateToAppointmentAsync(appointmentId);
#endif
    }
}
