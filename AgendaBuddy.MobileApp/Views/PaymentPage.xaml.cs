#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

[QueryProperty(nameof(AppointmentId), "appointmentId")]
public partial class PaymentPage : ContentPage
{
    private readonly PaymentViewModel _viewModel;

    public string AppointmentId { get; set; } = string.Empty;

    public PaymentPage(PaymentViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.AppointmentId = AppointmentId;
        _viewModel.LoadCommand.Execute(null);
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
#endif
