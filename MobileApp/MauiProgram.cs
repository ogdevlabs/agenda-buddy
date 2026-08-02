#if MOBILE
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using MobileApp.Infrastructure;
using MobileApp.Services;
using MobileApp.ViewModels;
using MobileApp.Views;

namespace MobileApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Secure storage abstraction
        builder.Services.AddTransient<ISecureStorageService, MauiSecureStorageService>();

        // HTTP client with named client and JWT delegating handler
        builder.Services.AddTransient<JwtDelegatingHandler>();
        builder.Services.AddHttpClient("AgendaBuddyApi", client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:6036/");
        }).AddHttpMessageHandler<JwtDelegatingHandler>();

        // No-auth client for login (no JWT handler — token doesn't exist yet)
        builder.Services.AddHttpClient("AgendaBuddyApiNoAuth", client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:6036/");
        });

        // API services
        builder.Services.AddTransient<IAuthService, AuthService>();
        builder.Services.AddTransient<IBookingApiService, BookingApiService>();
        builder.Services.AddTransient<ICalendarApiService, CalendarApiService>();
        builder.Services.AddTransient<ICustomerApiService, CustomerApiService>();
        builder.Services.AddTransient<IMessagingApiService, MessagingApiService>();
        builder.Services.AddTransient<INotificationApiService, NotificationApiService>();
        builder.Services.AddSingleton<PushNotificationService>();

        // ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<AppointmentDetailViewModel>();
        builder.Services.AddTransient<CalendarViewModel>();
        builder.Services.AddTransient<CustomersViewModel>();
        builder.Services.AddTransient<MessagingViewModel>();
        builder.Services.AddTransient<MessageThreadViewModel>();
        builder.Services.AddTransient<NotificationsViewModel>();

        // Views
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<CalendarPage>();
        builder.Services.AddTransient<CustomersPage>();
        builder.Services.AddTransient<MessagingPage>();
        builder.Services.AddTransient<MessageThreadPage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<AppointmentDetailPage>();

        // Shell
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
#endif
