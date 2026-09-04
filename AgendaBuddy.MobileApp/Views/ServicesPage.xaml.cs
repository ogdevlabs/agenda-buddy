#if MOBILE
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class ServicesPage : ContentPage
{
    private readonly ServicesViewModel _viewModel;

    public ServicesPage(ServicesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.RemoveRequested += OnRemoveRequested;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    private async void OnRemoveRequested(object? sender, ServiceItem service)
    {
        var choice = await DisplayActionSheetAsync(
            $"Remove \"{service.Name}\"? This cannot be undone.",
            "Keep it",
            null,
            "Remove");
        if (choice == "Remove")
            await _viewModel.RemoveConfirmedAsync(service);
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnAddServiceClicked(object? sender, EventArgs e)
    {
        // Returning from the Add page re-triggers OnAppearing, so the new service is already in the list.
        await Shell.Current.GoToAsync("addService");
    }
}
#endif
