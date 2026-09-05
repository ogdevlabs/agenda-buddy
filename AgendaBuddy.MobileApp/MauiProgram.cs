#if MOBILE
using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
#if FIREBASE
using Microsoft.Maui.LifecycleEvents;
#endif
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using AgendaBuddy.MobileApp.Views;

namespace AgendaBuddy.MobileApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit();

#if FIREBASE
        // Firebase has to be initialised before CrossFirebaseCloudMessaging.Current is touched, or that
        // property throws -- and PushNotificationService catches, so a missing initialisation is a silently
        // dead push path rather than a visible failure. Hooked to the Android activity's OnCreate because the
        // Android SDK needs a Context, which does not exist yet at this point in the builder.
        builder.ConfigureLifecycleEvents(events =>
            events.AddAndroid(android => android.OnCreate((activity, _) =>
                Plugin.Firebase.Core.Platforms.Android.CrossFirebase.Initialize(activity))));
#endif

        // Without this, builder.Configuration is empty and ApiBaseUrlResolver's middle priority --
        // the "ApiBaseUrl" key -- can never match, so every build falls through to the local gateway
        // fallback. That is why a Release build pointed at localhost and could reach no backend at all.
        // Read from embedded resources because a packaged app has no working directory for AddJsonFile.
        AddEmbeddedJson(builder.Configuration, "appsettings.json");
#if DEBUG
        // Overlays the deployed URL with the local gateway, so a Debug run needs no environment
        // variable. A Release build never sees this file's contents.
        AddEmbeddedJson(builder.Configuration, "appsettings.Development.json");
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Secure storage abstraction
        builder.Services.AddTransient<ISecureStorageService, MauiSecureStorageService>();

        // HTTP client with named client and JWT delegating handler
        builder.Services.AddTransient<JwtDelegatingHandler>();
        builder.Services.AddHttpClient("AgendaBuddyApi", client =>
        {
            client.BaseAddress = new Uri(ApiBaseUrlResolver.Resolve(builder.Configuration, Environment.GetEnvironmentVariable));
        }).AddHttpMessageHandler<JwtDelegatingHandler>();

        // No-auth client for login (no JWT handler — token doesn't exist yet)
        builder.Services.AddHttpClient("AgendaBuddyApiNoAuth", client =>
        {
            client.BaseAddress = new Uri(ApiBaseUrlResolver.Resolve(builder.Configuration, Environment.GetEnvironmentVariable));
        });

        // User session (singleton — decoded JWT cached across pages)
        builder.Services.AddSingleton<IUserSessionService, UserSessionService>();

        // API services
        builder.Services.AddTransient<IAuthService, AuthService>();
        builder.Services.AddTransient<IBookingApiService, BookingApiService>();
        builder.Services.AddTransient<ICalendarApiService, CalendarApiService>();
        builder.Services.AddTransient<ICustomerApiService, CustomerApiService>();
        builder.Services.AddTransient<IMessagingApiService, MessagingApiService>();
        builder.Services.AddTransient<INotificationApiService, NotificationApiService>();
        builder.Services.AddTransient<IProviderApiService, ProviderApiService>();
        builder.Services.AddTransient<IServicesApiService, ServicesApiService>();
        builder.Services.AddTransient<IProfessionApiService, ProfessionApiService>();
        builder.Services.AddSingleton<PushNotificationService>();

        // ViewModels
        // Singleton: the brand header is on every page, so the signed-in user's name is resolved once
        // per account rather than once per navigation.
        builder.Services.AddSingleton<BrandHeaderViewModel>();

        // Singleton for the same reason BrandHeaderViewModel is one: the unread count is shown on more than
        // one surface, and two copies of it drift the moment one of them is cleared.
        builder.Services.AddSingleton<NotificationBadgeViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<AppointmentDetailViewModel>();
        builder.Services.AddTransient<CalendarViewModel>();
        builder.Services.AddTransient<CalendarSettingsViewModel>();
        builder.Services.AddTransient<CustomersViewModel>();
        builder.Services.AddTransient<MessagingViewModel>();
        builder.Services.AddTransient<MessageThreadViewModel>();
        builder.Services.AddTransient<NotificationsViewModel>();
        builder.Services.AddTransient<ProviderReportViewModel>();
        builder.Services.AddTransient<PaymentViewModel>();
        builder.Services.AddTransient<BookAppointmentViewModel>();
        builder.Services.AddTransient<ServicesViewModel>();
        builder.Services.AddTransient<AddServiceViewModel>();
        builder.Services.AddTransient<ProfessionsViewModel>();
        builder.Services.AddTransient<AccountViewModel>();
        builder.Services.AddTransient<ForgotPasswordViewModel>();
        builder.Services.AddTransient<ResetPasswordConfirmViewModel>();

        // Views
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<CalendarPage>();
        builder.Services.AddTransient<CalendarSettingsPage>();
        builder.Services.AddTransient<CustomersPage>();
        builder.Services.AddTransient<MessagingPage>();
        builder.Services.AddTransient<MessageThreadPage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<AppointmentDetailPage>();
        builder.Services.AddTransient<ProviderReportPage>();
        builder.Services.AddTransient<PaymentPage>();
        builder.Services.AddTransient<BookAppointmentPage>();
        builder.Services.AddTransient<ServicesPage>();
        builder.Services.AddTransient<AddServicePage>();
        builder.Services.AddTransient<ProfessionsPage>();
        builder.Services.AddTransient<AccountPage>();
        builder.Services.AddTransient<MorePage>();
        builder.Services.AddTransient<ForgotPasswordPage>();
        builder.Services.AddTransient<ResetPasswordConfirmPage>();

        // Shell
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }

    /// <summary>
    /// Adds an embedded JSON configuration file, if it is present.
    /// </summary>
    /// <remarks>
    /// A missing file is not an error: it can only be missing if someone dropped the EmbeddedResource
    /// entry from the csproj, and failing startup over configuration that has a working fallback would
    /// be worse than quietly using the fallback.
    /// </remarks>
    private static void AddEmbeddedJson(IConfigurationBuilder configuration, string logicalName)
    {
        using var stream = typeof(MauiProgram).Assembly.GetManifestResourceStream(logicalName);
        if (stream is null) return;

        configuration.AddJsonStream(stream);
    }
}
#endif
