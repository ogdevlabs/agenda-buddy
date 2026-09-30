using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// The provider's code, as a QR on screen and as an image to post. The code is fetched (or issued) on open; the
/// image is composed only when a format is chosen, since composing all three up front is wasted work.
/// </summary>
public partial class ShowcaseShareViewModel : ObservableObject
{
    private readonly IShowcaseApiService _api;
    private readonly IShareImageService _share;
    private readonly IProviderApiService _providers;
    private readonly IUserSessionService _session;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayCode), nameof(HasCode))]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private byte[]? _qrPng;

    [ObservableProperty]
    private string _providerName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCode))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShare))]
    private bool _isPreparing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    public ShowcaseShareViewModel(
        IShowcaseApiService api,
        IShareImageService share,
        IProviderApiService providers,
        IUserSessionService session)
    {
        _api = api;
        _share = share;
        _providers = providers;
        _session = session;
    }

    public string DisplayCode => ShowcaseCodeParser.FormatForDisplay(Code);
    public bool HasCode => !string.IsNullOrEmpty(Code) && !IsLoading;
    public bool CanShare => HasCode && !IsPreparing;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _api.GetPublicCodeAsync();
            if (!result.IsSuccess || result.Value is null)
            {
                ErrorMessage = ShowcaseErrorCopy.Describe(result);
                return;
            }

            Code = result.Value.Code;
            Url = result.Value.Url;
            QrPng = string.IsNullOrWhiteSpace(Url) ? null : ShowcaseQr.Png(Url);

            if (string.IsNullOrEmpty(ProviderName) && !string.IsNullOrEmpty(_session.Email))
            {
                var profile = await _providers.GetProfileAsync(_session.Email);
                ProviderName = profile?.FullName?.Trim() ?? string.Empty;
            }
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(CanShare));
        }
    }

    /// <summary>
    /// What the QR encodes: the <c>/go/{code}</c> link, which a phone camera opens and the in-app scanner reads the
    /// code out of. A bare code is not a scannable payload.
    /// </summary>
    public string QrPayload => Url;

    [RelayCommand]
    private Task ShareStoryAsync() => ShareAsync(ShareFormat.Story);

    [RelayCommand]
    private Task ShareSquareAsync() => ShareAsync(ShareFormat.Square);

    [RelayCommand]
    private Task ShareCardAsync() => ShareAsync(ShareFormat.Card);

    public async Task ShareAsync(ShareFormat format)
    {
        if (!HasCode || IsPreparing)
            return;

        ErrorMessage = string.Empty;
        IsPreparing = true;
        try
        {
            var text = ShareCardText.For(AppBrand.Name, ProviderName, Code);
            ShareOutcome outcome;
            try
            {
                outcome = await _share.ShareAsync(format, text, QrPayload);
            }
            catch (Exception)
            {
                outcome = ShareOutcome.Failed;
            }

            if (outcome == ShareOutcome.Failed)
                ErrorMessage = AppResources.GetString("Share_ImageFailed");
        }
        finally
        {
            IsPreparing = false;
        }
    }
}
