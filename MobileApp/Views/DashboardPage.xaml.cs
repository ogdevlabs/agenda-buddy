#if MOBILE
using MobileApp.Models;
using MobileApp.ViewModels;

namespace MobileApp.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
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

        var nav = new Dictionary<string, object>
        {
            ["appointmentId"] = selected.Id,
            ["customerEmail"] = selected.CustomerEmail,
            ["customerName"] = selected.CustomerName,
            ["customerPhone"] = selected.CustomerPhone,
            ["scheduledAt"] = selected.ScheduledAt.ToString("O"),
            ["status"] = selected.Status.ToString(),
            ["serviceName"] = selected.ServiceName,
            ["customerNotes"] = selected.CustomerNotes ?? ""
        };

        await Shell.Current.GoToAsync("appointmentDetail", nav);
    }
}
#endif
