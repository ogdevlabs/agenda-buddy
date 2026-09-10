using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IPendingRegistrationStore? _pendingRegistrationStore;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _firstName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _lastName = string.Empty;

    /// <summary>
    /// Optional. The only fallback channel the other party has when a session is about to be missed, so it
    /// is asked for here rather than left to be discovered later — but it does not block signing up.
    /// </summary>
    [ObservableProperty]
    private string _phoneNumber = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private bool _isProvider;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public RegisterViewModel(
        IAuthService authService,
        IProviderApiService providerApiService,
        ICustomerApiService customerApiService,
        IPendingRegistrationStore? pendingRegistrationStore = null)
    {
        _authService = authService;
        _pendingRegistrationStore = pendingRegistrationStore;
    }

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        if (Password != ConfirmPassword)
        {
            ErrorMessage = AppResources.GetString("Validation_PasswordsMatch");
            return;
        }

        if (Password.Length < 8)
        {
            ErrorMessage = AppResources.GetString("Validation_PasswordLength");
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var role = IsProvider ? "Provider" : "Customer";
            var success = await _authService.RegisterAsync(Email, Password, role);
            if (!success)
            {
                ErrorMessage = AppResources.GetString("Error_RegistrationConflict");
                return;
            }

            if (_pendingRegistrationStore is not null)
            {
                await _pendingRegistrationStore.SaveAsync(new PendingRegistration(
                    Email.Trim(),
                    FirstName.Trim(),
                    LastName.Trim(),
                    string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim(),
                    role));
            }

            VerificationPending?.Invoke(Email.Trim());
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

    // Name is required: it is what the other party sees on a booking, and an account created without one
    // has nothing human-readable to identify it by anywhere in the app. Phone stays optional.
    private bool CanRegister() =>
        !string.IsNullOrWhiteSpace(FirstName)
        && !string.IsNullOrWhiteSpace(LastName)
        && !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(Password)
        && !string.IsNullOrWhiteSpace(ConfirmPassword);

    public event Action<string>? VerificationPending;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
