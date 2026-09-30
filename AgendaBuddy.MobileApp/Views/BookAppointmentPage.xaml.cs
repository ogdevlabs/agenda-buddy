#if MOBILE
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

[QueryProperty(nameof(CounterpartEmail), "counterpartEmail")]
[QueryProperty(nameof(CounterpartName), "counterpartName")]
[QueryProperty(nameof(Profession), "profession")]
public partial class BookAppointmentPage : ContentPage
{
    private readonly BookAppointmentViewModel _viewModel;
    private readonly ShowcaseLinkViewModel _showcaseLink;
    private readonly IUserSessionService _session;

    public string CounterpartEmail { get; set; } = string.Empty;
    public string CounterpartName { get; set; } = string.Empty;

    /// <summary>Carried from the directory's profession filter so the service list stays in that scope.</summary>
    public string Profession { get; set; } = string.Empty;

    public BookAppointmentPage(
        BookAppointmentViewModel viewModel,
        ShowcaseLinkViewModel showcaseLink,
        IUserSessionService session)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _showcaseLink = showcaseLink;
        _session = session;
        BindingContext = _viewModel;

        _showcaseLink.Source = Routing.ShowcaseSource.Booking;
        WorkStrip.BindingContext = _showcaseLink;
        _showcaseLink.ShowcaseRequested += OnShowcaseRequested;

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

        if (_session.IsCustomer)
            _ = _showcaseLink.LoadAsync(CounterpartEmail, _viewModel.CounterpartName);
    }

    private async void OnShowcaseRequested(object? sender, ShowcaseRequestedEventArgs e) =>
        await Shell.Current.GoToAsync(ShowcaseNavigation.Route, e.Parameters);

    /// <summary>
    /// Lands on the Dashboard, not the appointment detail page. Previously this popped back to the
    /// directory and pushed detail, which left the customer on a leaf screen with the provider list behind
    /// it and no sight of the booking in the place they look for their schedule. <c>//dashboard</c> is an
    /// absolute route, so it also clears the booking stack rather than burying it.
    /// </summary>
    private async void OnBookingSucceeded(object? sender, string identifier)
    {
        await Shell.Current.GoToAsync("//dashboard");
    }

}
#endif
