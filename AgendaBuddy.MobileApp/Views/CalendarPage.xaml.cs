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

    /// <summary>
    /// Opens the provider's working-week and time-off settings.
    /// </summary>
    /// <remarks>
    /// A <c>Tapped</c> handler rather than <c>Clicked</c>: the affordance is a labelled Border row now, not the
    /// 40x40 gear button it replaces — that one sat between the two week-navigation chevrons with no label, read as
    /// a third arrow, and providers were not finding it.
    /// </remarks>
    private async void OnCalendarSettingsTapped(object? sender, TappedEventArgs e)
    {
        // Returning re-triggers OnAppearing, so the calendar reloads against the saved window.
        await Shell.Current.GoToAsync("calendarSettings");
    }
}
#endif
