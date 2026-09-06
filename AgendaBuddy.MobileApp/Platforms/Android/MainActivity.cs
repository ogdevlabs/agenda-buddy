using Android.App;
using Android.Content.PM;
using Android.OS;
using AgendaBuddy.MobileApp.Infrastructure;

namespace AgendaBuddy.MobileApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        CreateNotificationChannel();
    }

    /// <summary>
    /// Declares the channel named by the manifest's <c>default_notification_channel_id</c> metadata.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Creating a channel is idempotent — Android updates the name and description of an existing one and
    /// ignores a repeated importance — so running this on every activity creation is correct, and doing it here
    /// rather than lazily means the channel exists before the first notification can arrive. A channel created
    /// after a notification has already been posted to the fallback channel does not retroactively move it.
    /// </para>
    /// <para>
    /// <c>High</c> importance, so an appointment request peeks as a banner instead of landing silently in the
    /// tray. The user can still turn that down per channel, which is the point of declaring one.
    /// </para>
    /// </remarks>
    private void CreateNotificationChannel()
    {
        // Channels did not exist before API 26; on those versions importance is a per-notification priority and
        // NotificationManager has no CreateNotificationChannel at all.
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;

        if (GetSystemService(NotificationService) is not NotificationManager manager) return;

        var channel = new NotificationChannel(
            PushChannel.Id, PushChannel.Name, NotificationImportance.High)
        {
            Description = PushChannel.Description
        };

        manager.CreateNotificationChannel(channel);
    }
}
