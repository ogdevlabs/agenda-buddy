namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>Fire-and-forget success/error feedback for a ViewModel command, shown on top of
/// whatever page is current. Same MOBILE-only guard <see cref="ViewModels.CustomersViewModel"/>'s own
/// DisplayAlert call already uses — the net10.0 fallback slice (MobileWorkloads=false) has no
/// platform toast surface to show on.</summary>
public static class ToastNotifier
{
    public static Task ShowAsync(string message)
    {
#if MOBILE
        return CommunityToolkit.Maui.Alerts.Toast.Make(message).Show();
#else
        return Task.CompletedTask;
#endif
    }
}
