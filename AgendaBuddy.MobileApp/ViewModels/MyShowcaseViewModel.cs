using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// The provider's showcase hub: what is done, what customers did with it, and the three places to change it.
/// </summary>
public partial class MyShowcaseViewModel : ObservableObject
{
    /// <summary>How many portfolio images the hub previews; the editor shows them all.</summary>
    public const int PreviewCount = 6;

    private readonly IShowcaseApiService _api;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasShowcase), nameof(ProviderRef), nameof(PhotoHash), nameof(LogoHash), nameof(HasPhoto),
        nameof(HasLogo), nameof(TaglineText), nameof(HasTagline), nameof(AboutText), nameof(HasAbout),
        nameof(CompletenessLabel), nameof(CompletenessProgress), nameof(IsComplete), nameof(FunnelSentence),
        nameof(PortfolioCountLabel), nameof(HasPortfolio), nameof(ShowPortfolioPrompt), nameof(PublicCodeDisplay))]
    private MyShowcase? _showcase;

    [ObservableProperty]
    private List<PortfolioTile> _preview = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPortfolioPrompt))]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    public MyShowcaseViewModel(IShowcaseApiService api) => _api = api;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasShowcase => Showcase is not null;
    public string ProviderRef => Showcase?.ProviderRef ?? string.Empty;
    public string PhotoHash => Showcase?.PhotoHash ?? string.Empty;
    public string LogoHash => Showcase?.LogoHash ?? string.Empty;
    public bool HasPhoto => !string.IsNullOrEmpty(PhotoHash);
    public bool HasLogo => !string.IsNullOrEmpty(LogoHash);

    public string TaglineText => string.IsNullOrWhiteSpace(Showcase?.Tagline)
        ? AppResources.GetString("Showcase_TaglineEmpty")
        : Showcase!.Tagline!;

    public bool HasTagline => !string.IsNullOrWhiteSpace(Showcase?.Tagline);

    public string AboutText => string.IsNullOrWhiteSpace(Showcase?.About)
        ? AppResources.GetString("Showcase_AboutEmpty")
        : Showcase!.About!;

    public bool HasAbout => !string.IsNullOrWhiteSpace(Showcase?.About);

    public string CompletenessLabel => ShowcaseText.Completeness(Showcase?.Completeness);
    public double CompletenessProgress => ShowcaseText.CompletenessProgress(Showcase?.Completeness);
    public bool IsComplete => Showcase?.Completeness is { } c && c.Total > 0 && c.Done >= c.Total;
    public string FunnelSentence => ShowcaseText.FunnelSentence(Showcase?.Funnel);
    public string PortfolioCountLabel => ShowcaseText.PortfolioCount(Showcase?.Portfolio.Count ?? 0);
    public bool HasPortfolio => Showcase?.Portfolio.Count > 0;

    /// <summary>The empty-portfolio prompt, shown only once the load has answered — never as a loading flash.</summary>
    public bool ShowPortfolioPrompt => HasShowcase && !HasPortfolio && !IsLoading;

    public string PublicCodeDisplay => ShowcaseCodeParser.FormatForDisplay(Showcase?.PublicCode);

    [RelayCommand]
    private Task LoadAsync() => LoadCoreAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            await LoadCoreAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task LoadCoreAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _api.GetMineAsync();
            if (!result.IsSuccess || result.Value is null)
            {
                ErrorMessage = ShowcaseErrorCopy.Describe(result);
                return;
            }

            Apply(result.Value);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Apply(MyShowcase showcase)
    {
        Showcase = showcase;
        Preview = PortfolioTile.From(showcase.ProviderRef, showcase.Portfolio).Take(PreviewCount).ToList();
    }
}
