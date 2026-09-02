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
        // Unlike LoginPage, a freshly-registered session was never seen by UpdateForRoleAsync —
        // without this, the Contacts tab keeps AppShell.xaml's XAML-default "Customers" title even
        // for a new Provider (or vice versa), until the next full login.
        if (Shell.Current is AppShell appShell)
            await appShell.UpdateForRoleAsync();

        await Shell.Current.GoToAsync("//dashboard");

        // A new Provider has no Professions/Services yet — route into that setup next rather
        // than leaving a Dashboard with nothing to show. A Customer has no such prerequisite.
        if (_vm.IsProvider)
            await Shell.Current.GoToAsync("professions");
    }

    private async void OnSignInTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//login");
    }
}
#endif
