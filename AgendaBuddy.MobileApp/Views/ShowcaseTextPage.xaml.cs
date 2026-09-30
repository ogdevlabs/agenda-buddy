#if MOBILE
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class ShowcaseTextPage : ContentPage
{
    private readonly ShowcaseTextViewModel _viewModel;
    private readonly IInAppAlertService _alerts;
    private bool _loaded;

    public ShowcaseTextPage(ShowcaseTextViewModel viewModel, IInAppAlertService alerts)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _alerts = alerts;
        BindingContext = _viewModel;
        _viewModel.Saved += OnSaved;
    }

    /// <summary>Loaded once: re-reading on every appearance would overwrite what is being typed.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;
        _loaded = true;
        _viewModel.LoadCommand.Execute(null);
    }

    private async void OnSaved(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
        _ = _alerts.ShowAsync(AppResources.Action_ShowcaseSaved);
    }
}
#endif
