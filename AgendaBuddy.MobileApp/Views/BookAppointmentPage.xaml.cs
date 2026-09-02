#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

[QueryProperty(nameof(CounterpartEmail), "counterpartEmail")]
[QueryProperty(nameof(CounterpartName), "counterpartName")]
[QueryProperty(nameof(Profession), "profession")]
public partial class BookAppointmentPage : ContentPage
{
    private readonly BookAppointmentViewModel _viewModel;

    public string CounterpartEmail { get; set; } = string.Empty;
    public string CounterpartName { get; set; } = string.Empty;

    /// <summary>Carried from the directory's profession filter so the service list stays in that scope.</summary>
    public string Profession { get; set; } = string.Empty;

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
        _viewModel.ProfessionScope = string.IsNullOrWhiteSpace(Profession) ? null : Profession;

        // The services and the availability behind them are fetched here rather than in the constructor:
        // the query properties are only populated after construction, so the provider is unknown until now.
        _viewModel.LoadCommand.Execute(null);
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
