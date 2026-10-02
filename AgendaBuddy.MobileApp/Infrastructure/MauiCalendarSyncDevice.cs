#if MOBILE
namespace AgendaBuddy.MobileApp.Infrastructure;

public class MauiCalendarSyncDevice : ICalendarSyncDevice
{
    public bool PrefersAppleCalendar => DeviceInfo.Platform == DevicePlatform.iOS;

    public async Task<bool> OpenAsync(string url)
    {
        // OpenAsync, not TryOpenAsync: on iOS the latter asks canOpenURL, which answers false for webcal unless the
        // scheme is declared in LSApplicationQueriesSchemes, though the system Calendar opens it fine.
        try
        {
            return await Launcher.Default.OpenAsync(new Uri(url));
        }
        catch (Exception)
        {
            return false;
        }
    }

    public Task CopyAsync(string text) => Clipboard.Default.SetTextAsync(text);

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        var page = Shell.Current?.CurrentPage;
        return page is null ? Task.FromResult(false) : page.DisplayAlertAsync(title, message, accept, cancel);
    }
}
#endif
