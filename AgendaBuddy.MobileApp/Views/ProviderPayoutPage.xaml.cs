#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

[QueryProperty(nameof(IsOnboarding), "onboarding")]
public partial class ProviderPayoutPage : ContentPage
{
    private readonly PaymentAccountViewModel _viewModel;

    public string IsOnboarding
    {
        set => _viewModel.IsOnboarding = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public ProviderPayoutPage(PaymentAccountViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.OpenUrlRequested += OnOpenUrlRequested;
        viewModel.OnboardingCompleted += OnOnboardingCompleted;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    private static async void OnOpenUrlRequested(object? sender, Uri uri) =>
        await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);

    private static async void OnOnboardingCompleted(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("professions");
}
#endif