using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.Library.Avatars;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// Choosing an avatar from the built-in set.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue is <see cref="AvatarCatalog"/>'s, not a list declared here — the server assigns from the same
/// one, and two lists would drift into the client offering an id the server refuses, or storing an id the client
/// has no asset for. The latter renders as an empty circle rather than an error.
/// </para>
/// <para>
/// ⚠️ <b>Uploading a photo is deliberately not here yet</b> (agenda-buddy-5qr). There is no image storage in this
/// product, so it needs a storage decision first; the write path this screen uses is the same one a photo would
/// hang off.
/// </para>
/// </remarks>
public partial class AvatarPickerViewModel : ObservableObject
{
    private readonly IProviderApiService _providerApiService;
    private readonly ICustomerApiService _customerApiService;
    private readonly IUserSessionService _session;
    private readonly BrandHeaderViewModel? _brandHeader;

    /// <summary>What the server had when this screen opened, so Save can skip a write that changes nothing.</summary>
    private string _originalAvatarId = string.Empty;

    public ObservableCollection<AvatarChoice> Choices { get; } = [];

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _selectedAvatarId = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>The large preview above the grid — the mark that will be saved, not the one currently stored.</summary>
    public string PreviewAsset => AvatarSource.For(SelectedAvatarId, Email);

    /// <summary>Raised on a successful save, so the view can go back.</summary>
    public event EventHandler? Saved;

    public AvatarPickerViewModel(
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

            if (Choices.Count == 0)
            {
                foreach (var id in AvatarCatalog.Ids)
                    Choices.Add(new AvatarChoice { Id = id });
            }

            string stored;
            try
            {
                var profile = IsProvider
                    ? await _providerApiService.GetProfileAsync(Email)
                    : await _customerApiService.GetProfileAsync(Email);

                stored = profile?.AvatarId ?? string.Empty;
            }
            catch (Exception)
            {
                // The grid is still usable without knowing the current mark — it just opens with nothing ringed.
                // Refusing to draw 24 local images because one request failed would be the worse answer.
                stored = string.Empty;
                ErrorMessage = AppResources.GetString("Error_LoadAvatar");
            }

            // An unknown stored id (a row from a build with a larger catalogue) resolves to the email-derived
            // mark, which is the tile the rest of the app is already drawing — so that is the one to ring.
            _originalAvatarId = stored;
            SelectedAvatarId = AvatarCatalog.Resolve(stored, Email);
            ApplySelection();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Rings a tile. Selection only — nothing is written until Save.
    /// </summary>
    /// <remarks>
    /// Deliberately not save-on-tap: the grid is 24 tiles and a mistap would be a write, and the preview above it
    /// only means something if there is a moment between choosing and committing.
    /// </remarks>
    [RelayCommand]
    private void Select(AvatarChoice? choice)
    {
        if (choice is null) return;

        SelectedAvatarId = choice.Id;
        ApplySelection();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        IsSaving = true;
        ErrorMessage = string.Empty;

        try
        {
            var succeeded = IsProvider
                ? await _providerApiService.SetAvatarAsync(Email, SelectedAvatarId)
                : await _customerApiService.SetAvatarAsync(Email, SelectedAvatarId);

            if (!succeeded)
            {
                ErrorMessage = AppResources.GetString("Error_SaveAvatar");
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            _originalAvatarId = SelectedAvatarId;

            // The header draws the name, not the avatar, but contacts and messaging draw the avatar from a cached
            // profile read — dropping the cached name forces the next read, which carries the new id with it.
            _brandHeader?.InvalidateName();

            await ToastNotifier.ShowAsync(AppResources.GetString("Action_AvatarUpdated"));
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.Error_ServerUnavailable;
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Nothing to save until something actually changed.
    /// </summary>
    /// <remarks>
    /// Compared against the <b>resolved</b> original, so an account with no stored avatar can save the mark it was
    /// already being drawn with — making the derived fallback its real, recorded choice. Without that, the tile
    /// ringed on open would be unsavable, which reads as the button being broken.
    /// </remarks>
    private bool CanSave() =>
        !string.IsNullOrWhiteSpace(SelectedAvatarId)
        && (!AvatarCatalog.IsKnown(_originalAvatarId) || SelectedAvatarId != _originalAvatarId);

    private void ApplySelection()
    {
        foreach (var choice in Choices)
            choice.IsSelected = choice.Id == SelectedAvatarId;

        OnPropertyChanged(nameof(PreviewAsset));
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnEmailChanged(string value) => OnPropertyChanged(nameof(PreviewAsset));
}
