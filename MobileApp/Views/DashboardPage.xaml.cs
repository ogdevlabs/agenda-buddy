#if MOBILE
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

    private async void OnAppointmentSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MobileApp.Models.AppointmentSummary selected)
            return;

        if (sender is CollectionView cv)
            cv.SelectedItem = null;

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
