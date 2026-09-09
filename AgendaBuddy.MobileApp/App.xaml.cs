#if MOBILE
using System.Globalization;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly ILanguageCoordinator _languageCoordinator;
    private readonly NotificationBadgeViewModel _notificationBadge;

#if DEBUG
    private readonly IAuthService _authService;

    public App(
        IServiceProvider services,
        ILanguageCoordinator languageCoordinator,
        NotificationBadgeViewModel notificationBadge,
        IAuthService authService)
    {
        InitializeComponent();
        _services = services;
        _languageCoordinator = languageCoordinator;
        _notificationBadge = notificationBadge;
        _authService = authService;
        InitializeLanguageAndNavigation();
    }
#else
    public App(
        IServiceProvider services,
        ILanguageCoordinator languageCoordinator,
        NotificationBadgeViewModel notificationBadge)
    {
        InitializeComponent();
        _services = services;
        _languageCoordinator = languageCoordinator;
        _notificationBadge = notificationBadge;
        InitializeLanguageAndNavigation();
    }
#endif

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<AppShell>());

        // The unread count otherwise only refreshes when MorePage appears — the one screen that shows it. A
        // notification arriving while the app sat on another tab, or while it was backgrounded, left the badge
        // stale until the reader happened to open the page whose whole job is telling them it changed.
        window.Resumed += (_, _) => _ = _notificationBadge.RefreshAsync();

#if DEBUG
        window.Created += (_, _) => _ = SignInFromLaunchEnvironmentAsync();
#endif

        return window;
    }

    private void InitializeLanguageAndNavigation()
    {
        _languageCoordinator.Initialize(CultureInfo.CurrentUICulture);
        JwtDelegatingHandler.UnauthorizedAccess += OnUnauthorizedAccess;
    }

    private static async void OnUnauthorizedAccess(object? sender, EventArgs eventArgs)
    {
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//login");
    }

#if DEBUG
    /// <summary>
    /// Signs in at launch using credentials supplied in the environment, for local verification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not an auth bypass.</b> It performs the ordinary <c>POST /api/v1/auth/login</c> through
    /// <see cref="IAuthService"/> with credentials handed to the process — exactly what typing them does. The
    /// server still authenticates, still issues the token, and a wrong password fails here as it would there.
    /// </para>
    /// <para>
    /// It exists because the login screen is otherwise a wall in front of every other screen when the simulator
    /// cannot be driven by synthetic taps: nothing past sign-in can be inspected, so a rendering fault anywhere
    /// in the app can only be found by asking somebody to tap through it. Set both variables to use it —
    /// <c>SIMCTL_CHILD_MAUI_DEV_EMAIL</c> / <c>SIMCTL_CHILD_MAUI_DEV_PASSWORD</c> on a
    /// <c>simctl launch</c>.
    /// </para>
    /// <para>
    /// <c>#if DEBUG</c>, so it is not compiled into a Release build at all — there is no code path to reach in a
    /// shipped app, not merely a flag that defaults off.
    /// </para>
    /// </remarks>
    private async Task SignInFromLaunchEnvironmentAsync()
    {
        var email = Environment.GetEnvironmentVariable("MAUI_DEV_EMAIL");
        var password = Environment.GetEnvironmentVariable("MAUI_DEV_PASSWORD");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        try
        {
            var signedIn = await _authService.LoginAsync(email, password);

            // Reported either way: a silent failure here would look like the app ignoring the variables, which
            // is the same confusion this exists to remove.
            Console.WriteLine(signedIn
                ? $"DEV SIGN-IN: signed in as {email}"
                : $"DEV SIGN-IN: refused for {email} — check the password and that the backend is reachable");

            if (!signedIn) return;

            if (Shell.Current is AppShell shell)
                await shell.UpdateForRoleAsync();

            // MAUI_DEV_ROUTE lets a launch land on any tab or page, so a screen other than the dashboard can be
            // inspected without tapping through to it. Defaults to the dashboard, which is where a real sign-in
            // goes.
            var route = Environment.GetEnvironmentVariable("MAUI_DEV_ROUTE");
            await Shell.Current.GoToAsync(
                string.IsNullOrWhiteSpace(route) ? "//dashboard" : route);

            // MAUI_DEV_TAB selects a tab on the page just opened, so a section other than the default can be
            // inspected. A tab is not reachable by navigation — it is a tap — so without this the non-default
            // sections of a tabbed page cannot be seen at all when synthetic taps do not work.
            var tab = Environment.GetEnvironmentVariable("MAUI_DEV_TAB");
            if (string.IsNullOrWhiteSpace(tab)) return;

            // Give the page its first layout pass before asking its view model for anything.
            await Task.Delay(1500);

            if (Shell.Current.CurrentPage?.BindingContext is ViewModels.AppointmentDetailViewModel appointment)
            {
                // "Notes/Add" reaches a section's own inner tab, which is otherwise unreachable: the Notes card
                // is itself split in two, and neither half is addressable by navigation.
                var parts = tab.Split('/', 2);
                appointment.SelectTabCommand.Execute(parts[0]);

                if (parts.Length == 2)
                    appointment.SelectNotesTabCommand.Execute(parts[1]);

                Console.WriteLine(
                    $"DEV TAB: selected {appointment.SelectedTab}/{appointment.SelectedNotesTab}");
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"DEV SIGN-IN: failed — {exception.Message}");
        }
    }
#endif
}
#endif
