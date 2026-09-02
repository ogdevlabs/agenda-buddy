#if MOBILE
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;
    private readonly ISecureStorageService _secureStorage;

    public DashboardPage(DashboardViewModel viewModel, ISecureStorageService secureStorage)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _secureStorage = secureStorage;
        BindingContext = _viewModel;

        _viewModel.AppointmentSelected += OnAppointmentSelected;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    private async void OnViewDetailsClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not AppointmentSummary selected)
            return;

        await OpenAppointmentAsync(selected);
    }

    /// <summary>Tapping a card anywhere opens its detail page — the cards no longer expand in place.</summary>
    private async void OnAppointmentSelected(object? sender, AppointmentSummary selected) =>
        await OpenAppointmentAsync(selected);

    private static async Task OpenAppointmentAsync(AppointmentSummary selected)
    {
        var nav = new Dictionary<string, object>
        {
            ["appointmentId"] = selected.Id,
            ["customerEmail"] = selected.CustomerEmail,
            ["customerName"] = selected.CustomerName,
            ["customerPhone"] = selected.CustomerPhone,
            ["providerName"] = selected.ProviderName,
            ["displayName"] = selected.DisplayName,
            ["scheduledAt"] = selected.ScheduledAt.ToString("O"),
            ["status"] = selected.Status.ToString(),
            ["serviceName"] = selected.ServiceName,
            ["serviceDurationMinutes"] = selected.ServiceDurationMinutes?.ToString() ?? "",
            ["customerNotes"] = selected.CustomerNotes ?? ""
        };

        await Shell.Current.GoToAsync("appointmentDetail", nav);
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        _secureStorage.Remove(JwtDelegatingHandler.JwtKey);
        await Shell.Current.GoToAsync("//login");
    }

    private async void OnViewReportClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("report");
    }
}
#endif
