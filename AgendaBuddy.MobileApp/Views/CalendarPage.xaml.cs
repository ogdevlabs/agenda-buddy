#if MOBILE
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class CalendarPage : ContentPage
{
    private readonly CalendarViewModel _viewModel;

    public CalendarPage(CalendarViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        _viewModel.AppointmentSelected += OnAppointmentSelected;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    /// <summary>
    /// Opens a booked session from the calendar — which is where a provider looks to change one.
    /// </summary>
    /// <remarks>
    /// The whole appointment travels, via the shared query builder, so the detail page renders immediately rather
    /// than falling back to a fetch. Returning re-triggers this page's <c>OnAppearing</c>, so a reschedule or a
    /// cancellation is reflected on the calendar without this page reaching into that one.
    /// </remarks>
    private async void OnAppointmentSelected(object? sender, AppointmentDetail appointment) =>
        await Shell.Current.GoToAsync(
            AppointmentNavigation.Route, AppointmentNavigation.BuildQuery(appointment));

    private async void OnCalendarSettingsClicked(object? sender, EventArgs e)
    {
        // Returning re-triggers OnAppearing, so the calendar reloads against the saved window.
        await Shell.Current.GoToAsync("calendarSettings");
    }
}
#endif
