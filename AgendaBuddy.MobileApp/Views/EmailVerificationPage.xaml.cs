#if MOBILE
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Views;

public partial class EmailVerificationPage : ContentPage, IQueryAttributable
{
    private readonly EmailVerificationViewModel _viewModel;
    private readonly IPendingRegistrationStore _pendingRegistrationStore;

    public EmailVerificationPage(
        EmailVerificationViewModel viewModel,
        IPendingRegistrationStore pendingRegistrationStore)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _pendingRegistrationStore = pendingRegistrationStore;
        BindingContext = viewModel;
        viewModel.VerificationSucceeded += OnVerificationSucceeded;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrWhiteSpace(_viewModel.Email)) return;

        var pending = await _pendingRegistrationStore.GetAsync();
        if (pending is not null)
            _viewModel.Email = pending.Email;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("email", out var email))
            _viewModel.Email = Uri.UnescapeDataString(email?.ToString() ?? string.Empty);

        if (query.TryGetValue("token", out var token))
        {
            _viewModel.Token = Uri.UnescapeDataString(token?.ToString() ?? string.Empty);
            _viewModel.ConfirmCommand.Execute(null);
        }
    }

    private async void OnSignInClicked(object? sender, EventArgs eventArgs) =>
        await Shell.Current.GoToAsync("//login");

    private async void OnVerificationSucceeded() =>
        await Shell.Current.GoToAsync("//login");
}
#endif