using System.Text;
using System.Text.Json;
using MobileApp.Infrastructure;
#if FIREBASE
using Microsoft.Maui.Devices;
using Plugin.Firebase.CloudMessaging;
#endif

namespace MobileApp.Services;

public class PushNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecureStorageService _secureStorage;

    public PushNotificationService(IHttpClientFactory httpClientFactory, ISecureStorageService secureStorage)
    {
        _httpClientFactory = httpClientFactory;
        _secureStorage = secureStorage;
    }

    public async Task InitializeAsync()
    {
        await RegisterTokenAsync();
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

    public void HandleNotificationTap(string appointmentId)
    {
#if MOBILE
        _ = AppShell.NavigateToAppointmentAsync(appointmentId);
#endif
    }
}
