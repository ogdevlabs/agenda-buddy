#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

[QueryProperty(nameof(Email), "email")]
public partial class ResetPasswordConfirmPage : ContentPage
{
    private readonly ResetPasswordConfirmViewModel _viewModel;

    public string Email { get; set; } = string.Empty;

    public ResetPasswordConfirmPage(ResetPasswordConfirmViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.ResetSucceeded += OnResetSucceeded;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrWhiteSpace(Email))
            _viewModel.Email = Email;
    }

    private async void OnResetSucceeded(object? sender, EventArgs e)
    {
        await DisplayAlertAsync("Password reset", "Your password has been reset. Sign in with your new password.", "OK");
        await Shell.Current.GoToAsync("//login");
    }
}
#endif
