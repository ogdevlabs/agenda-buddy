#if MOBILE
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _vm;

    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        vm.LoginSucceeded += OnLoginSucceeded;
        JwtDelegatingHandler.UnauthorizedAccess += OnUnauthorizedAccess;
    }

    private async void OnLoginSucceeded(object? sender, EventArgs e)
    {
        if (Shell.Current is AppShell appShell)
            await appShell.UpdateForRoleAsync();
        await Shell.Current.GoToAsync("//dashboard");
    }

    private async void OnUnauthorizedAccess(object? sender, EventArgs e)
    {
        _vm.ErrorMessage = "Your session expired. Please sign in again.";
        await Shell.Current.GoToAsync("//login");
    }

    private async void OnCreateAccountTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//register");
    }

    private async void OnForgotPasswordTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("forgotPassword");
    }
}
#endif
