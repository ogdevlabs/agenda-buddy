#if MOBILE
using AgendaBuddy.MobileApp.Infrastructure;
#endif

namespace AgendaBuddy.MobileApp.Services;

public interface ILanguageShellService
{
    Task RebuildAsync();
}

#if MOBILE
public sealed class LanguageShellService(
    IServiceProvider services,
    IUserSessionService session,
    PushNotificationService pushNotifications) : ILanguageShellService
{
    public async Task RebuildAsync()
    {
        _ = pushNotifications.RefreshRegistrationAsync();

        var window = Application.Current?.Windows.FirstOrDefault();
        if (window is null) return;

        var previousLocation = Shell.Current?.CurrentState.Location.OriginalString;
        var shell = services.GetRequiredService<AppShell>();
        window.Page = shell;

        await shell.UpdateForRoleAsync();

        var fallback = string.IsNullOrWhiteSpace(session.Email) ? "//login" : "//dashboard";
        var destination = string.IsNullOrWhiteSpace(previousLocation) ? fallback : previousLocation;

        try
        {
            await shell.GoToAsync(destination);
        }
        catch (ArgumentException)
        {
            await shell.GoToAsync(fallback);
        }
    }
}
#endif