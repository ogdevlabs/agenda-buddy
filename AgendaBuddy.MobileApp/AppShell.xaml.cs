#if MOBILE
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.Resources.Strings;

namespace AgendaBuddy.MobileApp;

public partial class AppShell : Shell
{
    private readonly IUserSessionService _session;

    public AppShell(IUserSessionService session)
    {
        InitializeComponent();
        _session = session;
    }

    public async Task UpdateForRoleAsync()
    {
        await _session.RefreshAsync();
        ContactsTab.Title = AppResources.GetString(
            _session.IsCustomer ? "Shell_ContactsProviders" : "Shell_ContactsCustomers");
    }

    public static async Task NavigateToAppointmentAsync(string appointmentId)
    {
        await Shell.Current.GoToAsync($"//dashboard/appointmentDetail?appointmentId={appointmentId}");
    }
}
#endif
