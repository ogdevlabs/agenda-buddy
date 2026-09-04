#if MOBILE
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
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    private async void OnCalendarSettingsClicked(object? sender, EventArgs e)
    {
        // Returning re-triggers OnAppearing, so the calendar reloads against the saved window.
        await Shell.Current.GoToAsync("calendarSettings");
    }
}
#endif
