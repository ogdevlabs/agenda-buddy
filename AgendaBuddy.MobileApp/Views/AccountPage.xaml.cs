#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class AccountPage : ContentPage
{
    private readonly AccountViewModel _viewModel;

    public AccountPage(AccountViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.DeactivationSucceeded += OnDeactivationSucceeded;
        _viewModel.LoggedOut += OnLoggedOut;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    private async void OnServicesClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("services");

    private async void OnProfessionsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("professions");

    private async void OnDeactivateClicked(object? sender, EventArgs e)
    {
        var choice = await DisplayActionSheetAsync(
            "Deactivate your account? This cannot be undone from the app.",
            "Keep my account",
            null,
            "Deactivate");
        if (choice == "Deactivate")
            _viewModel.DeactivateCommand.Execute(null);
    }

    private async void OnDeactivationSucceeded(object? sender, EventArgs e)
    {
        await DisplayAlertAsync("Account deactivated", "Your provider account has been deactivated.", "OK");
        await GoToLoginAsync();
    }

    private async void OnLoggedOut(object? sender, EventArgs e) => await GoToLoginAsync();

    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//login");
}
#endif
