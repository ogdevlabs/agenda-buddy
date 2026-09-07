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
        // The SHARED builder: AppointmentDetailPage reads eleven query properties, and the calendar opens the
        // same page. Two hand-built dictionaries is two chances for one to omit a key.
        var nav = AppointmentNavigation.BuildQuery(selected);

        await Shell.Current.GoToAsync(AppointmentNavigation.Route, nav);
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
