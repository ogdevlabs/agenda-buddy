using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IPendingRegistrationStore? _pendingRegistrationStore;
    private readonly IProviderApiService? _providerApiService;
    private readonly ICustomerApiService? _customerApiService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string? CompletedOnboardingRole { get; private set; }

    public LoginViewModel(
        IAuthService authService,
        IPendingRegistrationStore? pendingRegistrationStore = null,
        IProviderApiService? providerApiService = null,
        ICustomerApiService? customerApiService = null)
    {
        _authService = authService;
        _pendingRegistrationStore = pendingRegistrationStore;
        _providerApiService = providerApiService;
        _customerApiService = customerApiService;
    }

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var success = await _authService.LoginAsync(Email, Password);
            if (success)
            {
                await CompletePendingRegistrationAsync();
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else if (_authService.EmailVerificationRequired)
                EmailVerificationRequired?.Invoke(Email.Trim());
            else
                ErrorMessage = AppResources.GetString("Error_InvalidCredentials");
        }
        catch (HttpRequestException)
        {
            ErrorMessage = AppResources.Error_ServerUnavailable;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanSignIn() =>
        !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);

    private async Task CompletePendingRegistrationAsync()
    {
        if (_pendingRegistrationStore is null) return;

        var pending = await _pendingRegistrationStore.GetAsync();
        if (pending is null || !string.Equals(pending.Email, Email.Trim(), StringComparison.OrdinalIgnoreCase))
            return;

        var created = string.Equals(pending.Role, "Provider", StringComparison.Ordinal)
            ? _providerApiService is not null && await _providerApiService.CreateProfileAsync(
                pending.Email, pending.FirstName, pending.LastName, pending.PhoneNumber)
            : _customerApiService is not null && await _customerApiService.CreateProfileAsync(
                pending.Email, pending.FirstName, pending.LastName, pending.PhoneNumber);

        if (!created) return;

        if (string.Equals(pending.Role, "Provider", StringComparison.Ordinal)
            && _providerApiService is not null)
        {
            try { await _providerApiService.SyncTimeZoneAsync(pending.Email); }
            catch (Exception) { }
        }

        CompletedOnboardingRole = pending.Role;
        _pendingRegistrationStore.Clear();
    }

    public event EventHandler? LoginSucceeded;
    public event Action<string>? EmailVerificationRequired;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
