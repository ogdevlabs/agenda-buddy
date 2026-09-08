using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// The Profile screen: who is signed in, the avatar as the hero, and everything an account can do to itself.
/// </summary>
/// <remarks>
/// <para>
/// Replaces <c>AccountViewModel</c>. The profile <b>editor</b> moved out to <see cref="EditProfileViewModel"/> —
/// this screen reads, the editor writes. Keeping the inline edit form here is what made the old screen a list of
/// fields with a menu bolted underneath, and it meant the one screen that can repair a half-created account was
/// also the screen carrying sign-out and deactivation.
/// </para>
/// <para>
/// It shows a role's own extras conditionally rather than being two screens: a provider gets My Services and
/// Professions, a customer does not, and everything else — avatar, name, consent, language, legal, sign out,
/// delete — is identical. Two parallel screens would drift the first time an account-level item was added to one.
/// </para>
/// </remarks>
public partial class ProfileViewModel : ObservableObject
{
    private readonly IProviderApiService _providerApiService;
    private readonly ICustomerApiService _customerApiService;
    private readonly IAuthService _authService;
    private readonly IUserSessionService _session;

    /// <summary>
    /// The shared unread badge, cleared on sign-out and on deletion so the next account on this device does not
    /// inherit the previous one's count. Optional so tests that construct this view model directly need no change.
    /// </summary>
    private readonly NotificationBadgeViewModel? _notificationBadge;

    /// <summary>
    /// The signed-in user's name in the brand band. Refreshed after a delete so the band does not keep showing a
    /// name for an account that no longer exists. Optional for the same reason as the badge.
    /// </summary>
    private readonly BrandHeaderViewModel? _brandHeader;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _role = string.Empty;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _phoneNumber = string.Empty;

    /// <summary>
    /// The avatar image name for the hero. Seeded from the email before any fetch, so the hero is never an empty
    /// circle while the profile loads — <c>AvatarSource</c> derives a stable mark from the address.
    /// </summary>
    [ObservableProperty]
    private string _avatarAsset = string.Empty;

    [ObservableProperty]
    private bool _hasAcceptedTerms;

    [ObservableProperty]
    private bool _hasAcceptedPrivacy;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isDeactivating;

    [ObservableProperty]
    private bool _isDeleting;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool IsProvider => _session.IsProvider;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>The name to show under the avatar, falling back to the address for a profile with no name yet.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Email : FullName;

    /// <summary>
    /// Whether the hero should prompt the user to finish setting up.
    /// </summary>
    /// <remarks>
    /// An account with no name is the half-created state registration can leave behind, and the profile editor is
    /// the only thing that can repair it — so the screen has to say so rather than showing an email address where
    /// a name goes and leaving the user to guess.
    /// </remarks>
    public bool NeedsProfileSetup => !IsLoading && string.IsNullOrWhiteSpace(FullName);

    /// <summary>Both documents accepted. What the Edit Profile row's status line reports.</summary>
    public bool HasAcceptedLegal => HasAcceptedTerms && HasAcceptedPrivacy;

    /// <summary>The language row's value. One language is available, so this is a statement, not a choice yet.</summary>
    public string LanguageLabel => AppLanguages.Resolve(AppLanguages.DefaultCode).Name;

    public event EventHandler? DeactivationSucceeded;
    public event EventHandler? LoggedOut;

    /// <summary>Raised once the account is gone from both databases, so the view can leave for the login screen.</summary>
    public event EventHandler? AccountDeleted;

    public ProfileViewModel(
        IProviderApiService providerApiService,
        ICustomerApiService customerApiService,
        IAuthService authService,
        IUserSessionService session,
        NotificationBadgeViewModel? notificationBadge = null,
        BrandHeaderViewModel? brandHeader = null)
    {
        _providerApiService = providerApiService;
        _customerApiService = customerApiService;
        _authService = authService;
        _session = session;
        _notificationBadge = notificationBadge;
        _brandHeader = brandHeader;
    }

    /// <inheritdoc cref="AvatarAsset"/>
    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = string.Empty;
        IsLoading = true;

        try
        {
            await _session.RefreshAsync();
            Email = _session.Email;
            Role = _session.Role;
            OnPropertyChanged(nameof(IsProvider));

            // Before the fetch, so the hero draws a real mark immediately rather than an empty circle that fills
            // in a moment later.
            AvatarAsset = AvatarSource.FromEmail(Email);

            // Wrapped, and it always was: a thrown HttpRequestException/JsonException out of an AsyncRelayCommand
            // is unobserved, and on this screen it crashed the app outright (SIGABRT via
            // xamarin_process_managed_exception) rather than surfacing.
            try
            {
                var profile = IsProvider
                    ? await _providerApiService.GetProfileAsync(Email)
                    : await _customerApiService.GetProfileAsync(Email);

                if (profile is not null)
                {
                    FullName = profile.FullName;
                    PhoneNumber = profile.PhoneNumber;
                    AvatarAsset = profile.AvatarAsset;
                    HasAcceptedTerms = profile.TermsAcceptedAt is not null;
                    HasAcceptedPrivacy = profile.PrivacyAcceptedAt is not null;
                }

                // A provider's availability window is generated in THEIR timezone, so the server needs to know
                // it. Taken from the device rather than asked for. Failure is deliberately silent: it is a
                // background correction, and a provider whose zone is stale still has a working profile screen.
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
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(NeedsProfileSetup));
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync();

        // Before the navigation, not after: the badge is a singleton, so a count left standing here is the
        // previous account's count shown to whoever signs in next.
        _notificationBadge?.Clear();
        _brandHeader?.Clear();

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

    /// <summary>
    /// Deletes the account: the domain profile first, then the credential.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The order is the whole design.</b> The credential is what authorises the domain delete, so removing it
    /// first would leave a live profile — with the user's name, address and phone on it — that nothing could reach
    /// to finish erasing, and no way to sign back in and retry. Profile first means a failure at either step
    /// leaves an account the user can still sign into and delete again.
    /// </para>
    /// <para>
    /// If the profile delete fails, this <b>stops</b> and says so, rather than deleting the credential anyway to
    /// make the screen look like it worked. Locking somebody out of an account whose data is still stored is the
    /// one outcome worse than a failed delete.
    /// </para>
    /// <para>
    /// A credential delete that fails after the profile is gone leaves an account that can sign in to nothing.
    /// The message says exactly that and names the retry, because silently reporting success would leave a
    /// credential nobody knows about.
    /// </para>
    /// </remarks>
    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        IsDeleting = true;
        ErrorMessage = string.Empty;

        try
        {
            bool profileDeleted;
            try
            {
                profileDeleted = IsProvider
                    ? await _providerApiService.DeleteAccountAsync(Email)
                    : await _customerApiService.DeleteAccountAsync(Email);
            }
            catch (Exception)
            {
                profileDeleted = false;
            }

            if (!profileDeleted)
            {
                ErrorMessage = "Could not delete your account. Nothing has been removed — try again.";
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            var credentialDeleted = await _authService.DeleteAccountAsync();

            _notificationBadge?.Clear();
            _brandHeader?.Clear();

            if (!credentialDeleted)
            {
                // Deliberately not silent. The profile is gone, so the account is unusable either way, but a
                // credential left behind is a sign-in that reaches nothing — and only the user can tell us it
                // happened.
                await ToastNotifier.ShowAsync(
                    "Your data was deleted, but your sign-in could not be removed. Sign in once more and delete "
                    + "again to finish.");
            }
            else
            {
                await ToastNotifier.ShowAsync("Your account has been deleted.");
            }

            AccountDeleted?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsDeleting = false;
        }
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnFullNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(NeedsProfileSetup));
    }

    partial void OnEmailChanged(string value) => OnPropertyChanged(nameof(DisplayName));

    partial void OnHasAcceptedTermsChanged(bool value) => OnPropertyChanged(nameof(HasAcceptedLegal));

    partial void OnHasAcceptedPrivacyChanged(bool value) => OnPropertyChanged(nameof(HasAcceptedLegal));
}
