#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class AddServicePage : ContentPage
{
    private readonly AddServiceViewModel _viewModel;

    public AddServicePage(AddServiceViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.Added += OnAdded;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    /// <summary>
    /// Returns to the list, whose own OnAppearing reloads it — so the new service is there without this
    /// page having to reach into it.
    /// </summary>
    private async void OnAdded(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnProfessionsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("professions");
    }
}
#endif
