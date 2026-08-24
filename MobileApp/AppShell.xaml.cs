#if MOBILE
using MobileApp.Infrastructure;
using MobileApp.Services;
using MobileApp.Views;

namespace MobileApp;

public partial class AppShell : Shell
{
    private readonly IUserSessionService _session;

    public AppShell(IUserSessionService session)
    {
        InitializeComponent();
        _session = session;

        Routing.RegisterRoute("messageThread", typeof(MessageThreadPage));
        Routing.RegisterRoute("appointmentDetail", typeof(AppointmentDetailPage));
        Routing.RegisterRoute("report", typeof(ProviderReportPage));
        Routing.RegisterRoute("payment", typeof(PaymentPage));

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
