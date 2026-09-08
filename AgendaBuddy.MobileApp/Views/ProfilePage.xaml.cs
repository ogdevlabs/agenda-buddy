#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.DeactivationSucceeded += OnDeactivationSucceeded;
        _viewModel.LoggedOut += OnLoggedOut;
        _viewModel.AccountDeleted += OnAccountDeleted;
    }

    /// <summary>
    /// Reloads every time the page appears, which is also what refreshes it after returning from the avatar
    /// picker or the profile editor — neither of those reaches back into this page.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    private async void OnAvatarTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("avatarPicker");

    private async void OnEditProfileTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("editProfile");

    private async void OnLanguageTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("language");

    private async void OnServicesTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("services");

    private async void OnProfessionsTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("professions");

    private async void OnTermsTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("terms");

    private async void OnPrivacyTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("privacy");

    private void OnChangePasswordTapped(object? sender, EventArgs e) =>
        _viewModel.RequestPasswordChangeCommand.Execute(null);

    private async void OnDeactivateClicked(object? sender, EventArgs e)
    {
        var choice = await DisplayActionSheetAsync(
            "Deactivate your account? Customers will no longer see you, and your data is kept.",
            "Keep my account",
            null,
            "Deactivate");
        if (choice == "Deactivate")
            _viewModel.DeactivateCommand.Execute(null);
    }

    /// <summary>
    /// Two confirmations, and the second one is typed.
    /// </summary>
    /// <remarks>
    /// A single action sheet is one mistap away from an irreversible cross-database deletion. The second step asks
    /// the user to type DELETE, which cannot be hit by accident and cannot be dismissed into by muscle memory —
    /// the standard bar for a destructive account action, and the reason the button itself is not enough.
    /// </remarks>
    private async void OnDeleteAccountClicked(object? sender, EventArgs e)
    {
        var choice = await DisplayActionSheetAsync(
            "Delete your account permanently? This cannot be undone.",
            "Keep my account",
            null,
            "Delete my account");

        if (choice != "Delete my account")
            return;

        var typed = await DisplayPromptAsync(
            "Confirm deletion",
            "Type DELETE to confirm. Your profile will be removed and your details erased from other people's records.",
            accept: "Delete",
            cancel: "Cancel",
            placeholder: "DELETE",
            maxLength: 6);

        if (!string.Equals(typed?.Trim(), "DELETE", StringComparison.Ordinal))
            return;

        _viewModel.DeleteAccountCommand.Execute(null);
    }

    private async void OnDeactivationSucceeded(object? sender, EventArgs e)
    {
        await DisplayAlertAsync("Account deactivated", "Your provider account has been deactivated.", "OK");
        await GoToLoginAsync();
    }

    private async void OnLoggedOut(object? sender, EventArgs e) => await GoToLoginAsync();

    private async void OnAccountDeleted(object? sender, EventArgs e) => await GoToLoginAsync();

    private async Task GoToLoginAsync() => await Shell.Current.GoToAsync("//login");
}
#endif
