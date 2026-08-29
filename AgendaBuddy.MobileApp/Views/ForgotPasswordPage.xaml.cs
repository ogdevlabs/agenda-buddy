#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class ForgotPasswordPage : ContentPage
{
    private readonly ForgotPasswordViewModel _viewModel;

    public ForgotPasswordPage(ForgotPasswordViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    private async void OnSignInTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//login");

    private async void OnHaveCodeTapped(object? sender, EventArgs e)
    {
        var nav = new Dictionary<string, object> { ["email"] = _viewModel.Email };
        await Shell.Current.GoToAsync("resetPasswordConfirm", nav);
    }
}
#endif
