using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

public partial class AccountViewModel : ObservableObject
{
    private readonly IProviderApiService _providerApiService;
    private readonly ICustomerApiService _customerApiService;
    private readonly IAuthService _authService;
    private readonly IUserSessionService _session;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _role = string.Empty;

    [ObservableProperty]
    private bool _isDeactivating;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveProfileCommand))]
    private string _firstName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveProfileCommand))]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private bool _isEditingProfile;

    [ObservableProperty]
    private bool _isSavingProfile;

    [ObservableProperty]
    private string _profileErrorMessage = string.Empty;

    public bool IsProvider => _session.IsProvider;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasProfileError => !string.IsNullOrEmpty(ProfileErrorMessage);

    public event EventHandler? DeactivationSucceeded;
    public event EventHandler? LoggedOut;

    public AccountViewModel(
        IProviderApiService providerApiService,
        ICustomerApiService customerApiService,
        IAuthService authService,
        IUserSessionService session)
    {
        _providerApiService = providerApiService;
        _customerApiService = customerApiService;
        _authService = authService;
        _session = session;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = string.Empty;
        await _session.RefreshAsync();
        Email = _session.Email;
        Role = _session.Role;
        OnPropertyChanged(nameof(IsProvider));

        // Unlike every other ViewModel's LoadAsync in this codebase, this one had no try/catch —
        // a thrown HttpRequestException/JsonException here propagated unhandled out of the
        // AsyncRelayCommand and crashed the whole app (SIGABRT via xamarin_process_managed_exception),
        // reproduced 2026-08-28 navigating to the Account tab as a Provider.
        try
        {
            var profile = IsProvider
                ? await _providerApiService.GetProfileAsync(Email)
                : await _customerApiService.GetProfileAsync(Email);

            if (profile is not null)
            {
                FirstName = profile.FirstName;
                LastName = profile.LastName;
            }

            // A provider's availability window (09:00-19:00) is generated in THEIR timezone, so the server
            // needs to know it. Taken from the device rather than asked for, and written only when it has
            // actually changed. Failure here is deliberately silent: it is a background correction, and a
            // provider whose zone is stale still has a working profile screen.
            if (IsProvider)
            {
                try { await _providerApiService.SyncTimeZoneAsync(Email); }
                catch (Exception) { /* leaves the previous zone in place */ }
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Could not load your profile. Check your connection and try again.";
        }
    }

    [RelayCommand]
    private void ToggleEditProfile() => IsEditingProfile = !IsEditingProfile;

    [RelayCommand(CanExecute = nameof(CanSaveProfile))]
    private async Task SaveProfileAsync()
    {
        IsSavingProfile = true;
        ProfileErrorMessage = string.Empty;

        try
        {
            var succeeded = IsProvider
                ? await _providerApiService.UpdateProfileAsync(Email, FirstName, LastName)
                : await _customerApiService.UpdateProfileAsync(Email, FirstName, LastName);

            if (succeeded)
            {
                IsEditingProfile = false;
                await ToastNotifier.ShowAsync("Profile saved.");
            }
            else
            {
                ProfileErrorMessage = "Could not save your profile — try again.";
                await ToastNotifier.ShowAsync(ProfileErrorMessage);
            }
        }
        catch (Exception)
        {
            ProfileErrorMessage = "Could not reach the server. Check your connection and try again.";
            await ToastNotifier.ShowAsync(ProfileErrorMessage);
        }
        finally
        {
            IsSavingProfile = false;
        }
    }

    private bool CanSaveProfile() => !string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(LastName);

    partial void OnProfileErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasProfileError));

    [RelayCommand]
    private async Task DeactivateAsync()
    {
        IsDeactivating = true;
        ErrorMessage = string.Empty;

        try
        {
            var succeeded = await _providerApiService.DeactivateAsync();
            if (succeeded)
            {
                DeactivationSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorMessage = "Could not deactivate your account — try again.";
                await ToastNotifier.ShowAsync(ErrorMessage);
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Could not reach the server. Check your connection and try again.";
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
        finally
        {
            IsDeactivating = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();
        LoggedOut?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task RequestPasswordChangeAsync()
    {
        try
        {
            var succeeded = await _authService.RequestPasswordResetAsync(Email);
            await ToastNotifier.ShowAsync(succeeded
                ? "Check your email for a link to set a new password."
                : "Could not start a password change — try again.");
        }
        catch (Exception)
        {
            await ToastNotifier.ShowAsync("Could not reach the server. Check your connection and try again.");
        }
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
