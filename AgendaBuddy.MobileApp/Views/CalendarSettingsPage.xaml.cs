#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class CalendarSettingsPage : ContentPage
{
    private readonly CalendarSettingsViewModel _viewModel;

    public CalendarSettingsPage(CalendarSettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.Saved += OnSaved;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    /// <summary>
    /// Returns to the calendar, whose own OnAppearing reloads availability, so the new window is what is
    /// shown without this page reaching into it.
    /// </summary>
    private async void OnSaved(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
#endif
