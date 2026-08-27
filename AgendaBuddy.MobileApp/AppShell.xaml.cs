#if MOBILE
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.Views;

namespace AgendaBuddy.MobileApp;

public partial class AppShell : Shell
{
    private readonly IUserSessionService _session;

    public AppShell(IUserSessionService session)
    {
        InitializeComponent();
        _session = session;

        Microsoft.Maui.Controls.Routing.RegisterRoute("messageThread", typeof(MessageThreadPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("appointmentDetail", typeof(AppointmentDetailPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("report", typeof(ProviderReportPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("payment", typeof(PaymentPage));

        JwtDelegatingHandler.UnauthorizedAccess += async (_, _) =>
            await Shell.Current.GoToAsync("//login");
    }

    public async Task UpdateForRoleAsync()
    {
        await _session.RefreshAsync();
        ContactsTab.Title = _session.IsCustomer ? "Providers" : "Customers";
    }

    public static async Task NavigateToAppointmentAsync(string appointmentId)
    {
        await Shell.Current.GoToAsync($"//dashboard/appointmentDetail?appointmentId={appointmentId}");
    }
}
#endif
