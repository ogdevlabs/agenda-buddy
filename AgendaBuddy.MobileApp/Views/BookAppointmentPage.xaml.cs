#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

[QueryProperty(nameof(CounterpartEmail), "counterpartEmail")]
[QueryProperty(nameof(CounterpartName), "counterpartName")]
public partial class BookAppointmentPage : ContentPage
{
    private readonly BookAppointmentViewModel _viewModel;

    public string CounterpartEmail { get; set; } = string.Empty;
    public string CounterpartName { get; set; } = string.Empty;

    public BookAppointmentPage(BookAppointmentViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.BookingSucceeded += OnBookingSucceeded;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.CounterpartEmail = CounterpartEmail;
        _viewModel.CounterpartName = string.IsNullOrWhiteSpace(CounterpartName) ? CounterpartEmail : CounterpartName;
    }

    private async void OnBookingSucceeded(object? sender, string identifier)
    {
        await Shell.Current.GoToAsync("..");
        await AppShell.NavigateToAppointmentAsync(identifier);
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
#endif
