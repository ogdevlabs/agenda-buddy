#if MOBILE
using AgendaBuddy.MobileApp.Views;

namespace AgendaBuddy.MobileApp.Infrastructure;

public static class AppNavigation
{
    private static int _routesRegistered;

    public static void RegisterRoutes()
    {
        if (Interlocked.Exchange(ref _routesRegistered, 1) == 1) return;

        Microsoft.Maui.Controls.Routing.RegisterRoute("messageThread", typeof(MessageThreadPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("appointmentDetail", typeof(AppointmentDetailPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("report", typeof(ProviderReportPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("payment", typeof(PaymentPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("book", typeof(BookAppointmentPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("services", typeof(ServicesPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("addService", typeof(AddServicePage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("calendarSettings", typeof(CalendarSettingsPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("reschedule", typeof(ReschedulePage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("timeOff", typeof(TimeOffPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("notifications", typeof(NotificationsPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("professions", typeof(ProfessionsPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("profile", typeof(ProfilePage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("editProfile", typeof(EditProfilePage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("avatarPicker", typeof(AvatarPickerPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("language", typeof(LanguagePage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("terms", typeof(TermsPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("privacy", typeof(PrivacyPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("customerPaymentMethod", typeof(CustomerPaymentMethodPage));
        Microsoft.Maui.Controls.Routing.RegisterRoute("providerPayout", typeof(ProviderPayoutPage));
    }
}
#endif