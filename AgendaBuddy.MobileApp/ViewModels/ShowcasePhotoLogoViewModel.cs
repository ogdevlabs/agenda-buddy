using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// The provider's photo and logo. Each is uploaded as media first and then set by hash; removing one sets it to
/// null, and the avatar falls back to the catalogue mark.
/// </summary>
public partial class ShowcasePhotoLogoViewModel : ObservableObject
{
    private readonly IShowcaseApiService _api;
    private readonly IImagePicker _picker;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoto))]
    private string _photoHash = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogo))]
    private string _logoHash = string.Empty;

    [ObservableProperty]
    private string _providerRef = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isSavingPhoto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isSavingLogo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    public ShowcasePhotoLogoViewModel(IShowcaseApiService api, IImagePicker picker)
    {
        _api = api;
        _picker = picker;
    }

    public bool HasPhoto => !string.IsNullOrEmpty(PhotoHash);
    public bool HasLogo => !string.IsNullOrEmpty(LogoHash);
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsIdle => !IsSavingPhoto && !IsSavingLogo;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _api.GetMineAsync();
            if (result.IsSuccess && result.Value is not null)
                Apply(result.Value);
            else
                ErrorMessage = ShowcaseErrorCopy.Describe(result);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task ChoosePhotoAsync() => ChooseAsync(isLogo: false);

    [RelayCommand]
    private Task ChooseLogoAsync() => ChooseAsync(isLogo: true);

    [RelayCommand]
    private Task RemovePhotoAsync() => SetAsync(isLogo: false, hash: null);

    [RelayCommand]
    private Task RemoveLogoAsync() => SetAsync(isLogo: true, hash: null);

    private async Task ChooseAsync(bool isLogo)
    {
        if (!IsIdle)
            return;

        ErrorMessage = string.Empty;
        IReadOnlyList<byte[]> picked;
        try
        {
            picked = await _picker.PickAsync(1);
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.GetString("Showcase_PickFailed");
            return;
        }

        if (picked.Count == 0)
            return;

        SetBusy(isLogo, true);
        try
        {
            var upload = await _api.UploadImageAsync(picked[0]);
            if (!upload.IsSuccess || upload.Value is null)
            {
                ErrorMessage = ShowcaseErrorCopy.Describe(upload);
                return;
            }

            await SetCoreAsync(isLogo, upload.Value.Hash);
        }
        finally
        {
            SetBusy(isLogo, false);
        }
    }

    private async Task SetAsync(bool isLogo, string? hash)
    {
        if (!IsIdle)
            return;

        ErrorMessage = string.Empty;
        SetBusy(isLogo, true);
        try
        {
            await SetCoreAsync(isLogo, hash);
        }
        finally
        {
            SetBusy(isLogo, false);
        }
    }

    private async Task SetCoreAsync(bool isLogo, string? hash)
    {
        var result = isLogo ? await _api.SetLogoAsync(hash) : await _api.SetPhotoAsync(hash);
        if (result.IsSuccess && result.Value is not null)
            Apply(result.Value);
        else
            ErrorMessage = ShowcaseErrorCopy.Describe(result);
    }

    private void SetBusy(bool isLogo, bool busy)
    {
        if (isLogo)
            IsSavingLogo = busy;
        else
            IsSavingPhoto = busy;
    }

    private void Apply(MyShowcase showcase)
    {
        ProviderRef = showcase.ProviderRef;
        PhotoHash = showcase.PhotoHash ?? string.Empty;
        LogoHash = showcase.LogoHash ?? string.Empty;
    }
}
