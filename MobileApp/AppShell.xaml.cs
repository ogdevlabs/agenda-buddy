#if MOBILE
using MobileApp.Infrastructure;
using MobileApp.Views;

namespace MobileApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("messageThread", typeof(MessageThreadPage));
        Routing.RegisterRoute("appointmentDetail", typeof(AppointmentDetailPage));

        JwtDelegatingHandler.UnauthorizedAccess += async (_, _) =>
            await Shell.Current.GoToAsync("//login");
    }

    public static async Task NavigateToAppointmentAsync(string appointmentId)
    {
        await Shell.Current.GoToAsync($"//dashboard/appointmentDetail?appointmentId={appointmentId}");
    }
}
#endif
