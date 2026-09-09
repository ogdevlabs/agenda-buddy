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

            // Registering only creates an Identity credential. Without the matching domain record a
            // provider cannot pass the profession gate and a customer cannot subscribe to anyone — the
            // repository never upserts, so both writes answer 404 against a profile that does not exist.
            // Done here, straight after register, because Identity is deliberately decoupled from the
            // domain services and has no way to create either record itself.
            var phone = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim();
            var profileCreated = IsProvider
                ? await _providerApiService.CreateProfileAsync(Email, FirstName.Trim(), LastName.Trim(), phone)
                : await _customerApiService.CreateProfileAsync(Email, FirstName.Trim(), LastName.Trim(), phone);

            // A provider's availability window is generated in their own zone, and until now that zone was
            // only ever recorded when they happened to open Account — so a provider who never did kept UTC
            // hours, and every slot offered to their customers was wrong by their offset. Recorded here
            // instead, at the one moment we know a provider profile has just come into existence.
            //
            // Its own try/catch and never awaited for correctness: the profile is the thing that had to be
            // created, and a zone that can be re-synced on the next Account load must not be the reason
            // registration reports a failure.
            if (profileCreated && IsProvider)
            {
                try { await _providerApiService.SyncTimeZoneAsync(Email); }
                catch (Exception) { /* re-synced on the next Account load */ }
            }

            if (!profileCreated)
            {
                // The account exists and the caller is signed in, so this is recoverable rather than fatal
                // — but say so, because the parts of the app that need the profile will fail until it is
                // created from the Account screen.
                ErrorMessage = AppResources.GetString("Error_ProfileAfterRegistration");
            }

            RegistrationSucceeded?.Invoke(this, EventArgs.Empty);
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

    public event EventHandler? RegistrationSucceeded;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
