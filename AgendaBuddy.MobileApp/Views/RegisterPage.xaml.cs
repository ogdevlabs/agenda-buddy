#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class RegisterPage : ContentPage
{
    private readonly RegisterViewModel _vm;

    public RegisterPage(RegisterViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        vm.RegistrationSucceeded += OnRegistrationSucceeded;
    }

    private async void OnRegistrationSucceeded(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//dashboard");
    }

    private async void OnSignInTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//login");
    }
}
#endif
