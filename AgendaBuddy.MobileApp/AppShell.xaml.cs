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
        // Every sign-in passes through here. Navigating to //login swaps the visible root but keeps each tab's
        // pushed pages, so without this the next account opens a tab onto the previous account's screens —
        // their profile, email and business rows — rendered from data that was loaded for them.
        await PopEveryTabToRootAsync();
        await _session.RefreshAsync();
        ContactsTab.Title = AppResources.GetString(
            _session.IsCustomer ? "Shell_ContactsProviders" : "Shell_ContactsCustomers");
    }

    private async Task PopEveryTabToRootAsync()
    {
        foreach (var section in Items.SelectMany(item => item.Items))
        {
            if (section.Navigation.NavigationStack.Count > 1)
                await section.Navigation.PopToRootAsync(animated: false);
        }
    }

    public static async Task NavigateToAppointmentAsync(string appointmentId)
    {
        await Shell.Current.GoToAsync($"//dashboard/appointmentDetail?appointmentId={appointmentId}");
    }
}
#endif
