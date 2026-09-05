#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp;

public partial class App : Application
{
    private readonly AppShell _shell;
    private readonly NotificationBadgeViewModel _notificationBadge;

    public App(AppShell shell, NotificationBadgeViewModel notificationBadge)
    {
        InitializeComponent();
        _shell = shell;
        _notificationBadge = notificationBadge;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_shell);

        // The unread count otherwise only refreshes when MorePage appears — the one screen that shows it. A
        // notification arriving while the app sat on another tab, or while it was backgrounded, left the badge
        // stale until the reader happened to open the page whose whole job is telling them it changed.
        window.Resumed += (_, _) => _ = _notificationBadge.RefreshAsync();

        return window;
    }
}
#endif
