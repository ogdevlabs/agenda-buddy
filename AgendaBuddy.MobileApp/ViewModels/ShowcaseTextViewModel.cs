using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// The tagline and the About text. Save is always executable and checks the lengths itself, naming what is too
/// long — a disabled button says nothing about why.
/// </summary>
public partial class ShowcaseTextViewModel : ObservableObject
{
    private readonly IShowcaseApiService _api;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TaglineCounter), nameof(IsTaglineTooLong))]
    private string _tagline = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AboutCounter), nameof(IsAboutTooLong))]
    private string _about = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    public event EventHandler? Saved;

    public ShowcaseTextViewModel(IShowcaseApiService api) => _api = api;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string TaglineCounter => $"{ShowcaseText.VisibleLength(Tagline)}/{ShowcaseText.TaglineMaxLength}";
    public string AboutCounter => $"{ShowcaseText.VisibleLength(About)}/{ShowcaseText.AboutMaxLength}";
    public bool IsTaglineTooLong => ShowcaseText.VisibleLength(Tagline) > ShowcaseText.TaglineMaxLength;
    public bool IsAboutTooLong => ShowcaseText.VisibleLength(About) > ShowcaseText.AboutMaxLength;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _api.GetMineAsync();
            if (result.IsSuccess && result.Value is not null)
            {
                Tagline = result.Value.Tagline ?? string.Empty;
                About = result.Value.About ?? string.Empty;
            }
            else
            {
                ErrorMessage = ShowcaseErrorCopy.Describe(result);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsSaving)
            return;

        ErrorMessage = string.Empty;
        if (IsTaglineTooLong)
        {
            ErrorMessage = AppResources.Format("Showcase_TaglineTooLong", ShowcaseText.TaglineMaxLength);
            return;
        }

        if (IsAboutTooLong)
        {
            ErrorMessage = AppResources.Format("Showcase_AboutTooLong", ShowcaseText.AboutMaxLength);
            return;
        }

        IsSaving = true;
        try
        {
            var result = await _api.SetTextAsync(NullIfBlank(Tagline), NullIfBlank(About));
            if (!result.IsSuccess)
            {
                ErrorMessage = ShowcaseErrorCopy.Describe(result);
                return;
            }

            Saved?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
