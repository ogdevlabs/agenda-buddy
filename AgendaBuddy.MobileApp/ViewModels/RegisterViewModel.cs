using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IProviderApiService _providerApiService;
    private readonly ICustomerApiService _customerApiService;

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
        ICustomerApiService customerApiService)
    {
        _authService = authService;
        _providerApiService = providerApiService;
        _customerApiService = customerApiService;
    }

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        if (Password.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters.";
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
                ErrorMessage = "Registration failed. This email may already be in use.";
                return;
            }

            // Registering only creates an Identity credential. Without the matching domain record a
            // provider cannot pass the profession gate and a customer cannot subscribe to anyone — the
            // repository never upserts, so both writes answer 404 against a profile that does not exist.
            // Done here, straight after register, because Identity is deliberately decoupled from the
            // domain services and has no way to create either record itself.
            var phone = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim();
            var profileCreated = IsProvider
                ? await _providerApiService.CreateProfileAsync(Email, FirstName.Trim(), LastName.Trim(), phone)
                : await _customerApiService.CreateProfileAsync(Email, FirstName.Trim(), LastName.Trim(), phone);

            if (!profileCreated)
            {
                // The account exists and the caller is signed in, so this is recoverable rather than fatal
                // — but say so, because the parts of the app that need the profile will fail until it is
                // created from the Account screen.
                ErrorMessage = "Your account was created, but we could not save your profile details. "
                             + "Add them from Account to finish setting up.";
            }

            RegistrationSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Could not reach the server. Check your connection and try again.";
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

    public event EventHandler? RegistrationSucceeded;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
