using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// Editing your own personal information, and recording consent to the Terms and the Privacy Policy.
/// </summary>
/// <remarks>
/// <para>
/// <b>The email address is shown but not editable.</b> It is the account's identity — the <c>sub</c> claim every
/// route authorises against, the key the profile document is matched on, and the address the credential lives
/// under in a different database. Changing it is not an edit, it is a migration across two databases plus a
/// re-issued token, so the field is rendered read-only with the reason stated rather than left out (a missing
/// email reads as "we lost it").
/// </para>
/// <para>
/// Extracted from the old <c>AccountViewModel</c>, which carried this form inline alongside sign-out and
/// deactivation. The account-repair behaviour below is the part that must not be lost in the move.
/// </para>
/// </remarks>
public partial class EditProfileViewModel : ObservableObject
{
    private readonly IProviderApiService _providerApiService;
    private readonly ICustomerApiService _customerApiService;
    private readonly IUserSessionService _session;

    /// <summary>
    /// The brand band's cached name. Invalidated after a successful save, or the header keeps showing the old
    /// name until the app restarts. Optional so tests need no change.
    /// </summary>
    private readonly BrandHeaderViewModel? _brandHeader;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _firstName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _lastName = string.Empty;

    /// <summary>Optional contact number, editable here as well as captured at registration.</summary>
    [ObservableProperty]
    private string _phoneNumber = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _acceptedTerms;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _acceptedPrivacy;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>The effective date shown beside the two checkboxes, so a reader knows which version they accepted.</summary>
    public string LegalEffectiveDate => LegalDocuments.EffectiveDate;

    /// <summary>Raised on a successful save, so the view can go back to the profile.</summary>
    public event EventHandler? Saved;

    public EditProfileViewModel(
        IProviderApiService providerApiService,
        ICustomerApiService customerApiService,
        IUserSessionService session,
        BrandHeaderViewModel? brandHeader = null)
    {
        _providerApiService = providerApiService;
        _customerApiService = customerApiService;
        _session = session;
        _brandHeader = brandHeader;
    }

    private bool IsProvider => _session.IsProvider;

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = string.Empty;
        IsLoading = true;

        try
        {
            await _session.RefreshAsync();
            Email = _session.Email;

            var profile = IsProvider
                ? await _providerApiService.GetProfileAsync(Email)
                : await _customerApiService.GetProfileAsync(Email);

            if (profile is not null)
            {
                FirstName = profile.FirstName;
                LastName = profile.LastName;
                PhoneNumber = profile.PhoneNumber;
                AcceptedTerms = profile.TermsAcceptedAt is not null;
                AcceptedPrivacy = profile.PrivacyAcceptedAt is not null;
            }
        }
        catch (Exception)
        {
            // Not fatal: an account with no profile is the state this screen exists to repair, and a failed read
            // must still leave a usable form — otherwise the one screen that can fix the account is the screen
            // that will not open.
            ErrorMessage = "Could not load your details. You can still enter them and save.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Saves the personal details and the consent state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It creates the profile when there is none, and that fallback is the whole point.</b>
    /// <c>UpdateProfileAsync</c> reads the profile before writing and gives up if the read 404s, and the route
    /// behind it answers <c>NotFound</c> for a profile that does not exist — because <c>FindOneAndUpdateAsync</c>
    /// never upserts (ADR-032), which is deliberate and load-bearing elsewhere. So for an account whose profile
    /// was never created, every Save failed with "try again", and trying again could not possibly help.
    /// </para>
    /// <para>
    /// That is the account-stranding hole: registration creates an Identity credential and then a domain profile,
    /// and if the second call fails the user is signed in with a valid token and no profile. The credential
    /// existed, sign-in worked, and nothing in the app could repair it.
    /// </para>
    /// <para>
    /// Update is still attempted first: it is the overwhelmingly common case, and creating first would mean a
    /// duplicate-name rejection for every ordinary edit (the create handlers match existing records by first and
    /// last name).
    /// </para>
    /// <para>
    /// <b>Consent is written second, through its own route, and a failure there is reported without losing the
    /// name change.</b> The two are separate writes because they are separate records — the consent route is a
    /// targeted <c>$set</c> of two timestamps, while the profile save is a whole-document replace. Rolling the
    /// name back because a consent write failed would discard work the user can see succeeded.
    /// </para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        IsSaving = true;
        ErrorMessage = string.Empty;

        try
        {
            var phone = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim();

            var succeeded = IsProvider
                ? await _providerApiService.UpdateProfileAsync(Email, FirstName, LastName, PhoneNumber)
                : await _customerApiService.UpdateProfileAsync(Email, FirstName, LastName, PhoneNumber);

            if (!succeeded)
            {
                succeeded = IsProvider
                    ? await _providerApiService.CreateProfileAsync(Email, FirstName.Trim(), LastName.Trim(), phone)
                    : await _customerApiService.CreateProfileAsync(Email, FirstName.Trim(), LastName.Trim(), phone);
            }

            if (!succeeded)
            {
                ErrorMessage = "Could not save your profile — try again.";
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            var consentSaved = IsProvider
                ? await _providerApiService.SetConsentAsync(Email, AcceptedTerms, AcceptedPrivacy)
                : await _customerApiService.SetConsentAsync(Email, AcceptedTerms, AcceptedPrivacy);

            _brandHeader?.InvalidateName();

            if (!consentSaved)
            {
                ErrorMessage = "Your details were saved, but your agreement was not recorded — try again.";
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            await ToastNotifier.ShowAsync("Profile saved.");
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            ErrorMessage = "Could not reach the server. Check your connection and try again.";
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// A name is required, and so is agreement to both documents.
    /// </summary>
    /// <remarks>
    /// Gating Save on the two checkboxes rather than letting the form submit and then complaining: an unticked box
    /// is not an error to report, it is a decision the user has not made yet, and a disabled button next to two
    /// unticked boxes says what is missing without a message.
    /// </remarks>
    private bool CanSave() =>
        !string.IsNullOrWhiteSpace(FirstName)
        && !string.IsNullOrWhiteSpace(LastName)
        && AcceptedTerms
        && AcceptedPrivacy;

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
