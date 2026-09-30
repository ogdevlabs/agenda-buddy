using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;
using AgendaBuddy.MobileApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaBuddy.MobileApp.ViewModels;

public sealed record ShowcasePhotoRequestedEventArgs(IReadOnlyList<PortfolioTile> Tiles, int Index);

/// <summary>
/// A provider's showcase as a customer sees it: who they are, their work, what they offer, and the three things
/// to do next — book, subscribe, message — pinned where they cannot scroll away.
/// </summary>
/// <remarks>
/// The showcase body deliberately carries no email, but booking, subscribing and messaging are all keyed on one.
/// The caller passes it when it has it (the directory, an appointment, a thread); otherwise it is matched from the
/// provider directory by <c>providerRef</c>, which the caller can see anyway.
/// </remarks>
public partial class ProviderShowcaseViewModel : ObservableObject
{
    private readonly IShowcaseApiService _api;
    private readonly IUserSessionService _session;
    private readonly IProviderApiService _providers;
    private readonly ICustomerApiService _customers;

    [ObservableProperty]
    private string _providerRef = string.Empty;

    [ObservableProperty]
    private ShowcaseSource _source = ShowcaseSource.Directory;

    [ObservableProperty]
    private string _providerEmail = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasShowcase), nameof(FullName), nameof(FirstName), nameof(ProfessionsLine), nameof(HasProfessions),
        nameof(Tagline), nameof(HasTagline), nameof(About), nameof(HasAbout), nameof(HeroHash), nameof(HasHeroImage),
        nameof(BadgeHash), nameof(HasBadge), nameof(AvatarAsset), nameof(NextSession), nameof(HasNextSession),
        nameof(Services), nameof(HasServices), nameof(IsSelf), nameof(IsSubscribed), nameof(CanAct),
        nameof(CanBook), nameof(CanSubscribe), nameof(CanMessage), nameof(ActionsReason), nameof(HasActionsReason),
        nameof(SubscribeLabel), nameof(CanHide), nameof(CanReport), nameof(HasPortfolio), nameof(IsEmptyShowcase))]
    private ShowcaseView? _showcase;

    [ObservableProperty]
    private List<PortfolioTile> _tiles = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyShowcase))]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBook), nameof(CanSubscribe), nameof(CanMessage))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailure))]
    private bool _isNotFound;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailure))]
    private bool _isNetworkError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    public ProviderShowcaseViewModel(
        IShowcaseApiService api,
        IUserSessionService session,
        IProviderApiService providers,
        ICustomerApiService customers)
    {
        _api = api;
        _session = session;
        _providers = providers;
        _customers = customers;
    }

    public event EventHandler<BookRequestedEventArgs>? BookRequested;
    public event EventHandler<CustomerSummary>? MessageRequested;
    public event EventHandler<ShowcasePhotoRequestedEventArgs>? PhotoRequested;

    /// <summary>Raised after a successful hide; the page leaves and offers Undo.</summary>
    public event EventHandler<string>? Hidden;

    public bool HasShowcase => Showcase is not null;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasFailure => IsNotFound || IsNetworkError;

    public string FullName => Showcase?.FullName ?? string.Empty;
    public string FirstName => Showcase?.FirstName ?? string.Empty;
    public string ProfessionsLine => Showcase is null ? string.Empty : string.Join(" · ", Showcase.Professions);
    public bool HasProfessions => Showcase?.Professions.Count > 0;
    public string Tagline => Showcase?.Tagline?.Trim() ?? string.Empty;
    public bool HasTagline => !string.IsNullOrEmpty(Tagline);
    public string About => Showcase?.About?.Trim() ?? string.Empty;
    public bool HasAbout => !string.IsNullOrEmpty(About);
    public bool HasPortfolio => Showcase?.Portfolio.Count > 0;

    /// <summary>The first portfolio image, else the photo; with neither the hero is the brand gradient.</summary>
    public string HeroHash => Showcase is null
        ? string.Empty
        : Showcase.Portfolio.FirstOrDefault()?.Hash ?? Showcase.PhotoHash ?? string.Empty;

    public bool HasHeroImage => !string.IsNullOrEmpty(HeroHash);

    /// <summary>The round mark over the hero: the logo, else the photo.</summary>
    public string BadgeHash => Showcase?.LogoHash ?? Showcase?.PhotoHash ?? string.Empty;

    public bool HasBadge => !string.IsNullOrEmpty(BadgeHash);
    public string AvatarAsset => AvatarSource.For(
        Showcase?.AvatarId,
        string.IsNullOrWhiteSpace(ProviderEmail) ? ProviderRef : ProviderEmail);

    public string NextSession => Showcase?.Relationship.NextAppointment is { } next
        ? ShowcaseText.NextSession(next)
        : string.Empty;

    public bool HasNextSession => !string.IsNullOrEmpty(NextSession);

    public IReadOnlyList<ShowcaseServiceItem> Services => Showcase?.Services ?? [];
    public bool HasServices => Services.Count > 0;
    public bool IsEmptyShowcase => HasShowcase && !IsLoading && !HasPortfolio && !HasAbout && !HasTagline;

    public bool IsSelf => Showcase?.Relationship.IsSelf == true;
    public bool IsSubscribed => Showcase?.Relationship.IsSubscribed == true;

    /// <summary>Book, subscribe and message are a customer's actions on somebody else.</summary>
    public bool CanAct => HasShowcase && !IsSelf && _session.IsCustomer;

    public bool CanBook => CanAct && !IsBusy;
    public bool CanSubscribe => CanAct && !IsBusy;

    /// <summary>Messaging opens with the subscription, the same rule the directory applies.</summary>
    public bool CanMessage => CanAct && IsSubscribed && !IsBusy;

    public string ActionsReason
    {
        get
        {
            if (!HasShowcase)
                return string.Empty;
            if (IsSelf)
                return AppResources.GetString("Showcase_SelfReason");
            if (!_session.IsCustomer)
                return AppResources.GetString("Showcase_CustomersOnlyReason");
            return IsSubscribed ? string.Empty : AppResources.GetString("Showcase_SubscribeToMessage");
        }
    }

    public bool HasActionsReason => !string.IsNullOrEmpty(ActionsReason);

    public string SubscribeLabel => AppResources.GetString(IsSubscribed ? "Showcase_Unsubscribe" : "Showcase_Subscribe");

    public bool CanHide => HasShowcase && !IsSelf && _session.IsCustomer;
    public bool CanReport => HasShowcase && !IsSelf;

    /// <summary>Opens on a showcase already fetched — by a scan — so the visit is not recorded a second time.</summary>
    public void Preload(ShowcaseView view, ShowcaseSource source)
    {
        Source = source;
        ProviderRef = view.ProviderRef;
        Apply(view);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (HasShowcase)
            return;

        await FetchAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            await FetchAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private Task RetryAsync() => FetchAsync();

    private async Task FetchAsync()
    {
        if (string.IsNullOrWhiteSpace(ProviderRef))
        {
            IsNotFound = true;
            return;
        }

        IsLoading = true;
        IsNotFound = false;
        IsNetworkError = false;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _api.GetShowcaseAsync(ProviderRef, Source);
            if (result.IsSuccess && result.Value is not null)
            {
                Apply(result.Value);
                return;
            }

            if (result.ErrorCode == ShowcaseErrorCodes.ShowcaseNotFound || result.StatusCode == 404)
            {
                Showcase = null;
                IsNotFound = true;
                ErrorMessage = ShowcaseErrorCopy.For(ShowcaseErrorCodes.ShowcaseNotFound);
            }
            else
            {
                IsNetworkError = !HasShowcase;
                ErrorMessage = ShowcaseErrorCopy.Describe(result);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Apply(ShowcaseView view)
    {
        Showcase = view;
        Tiles = PortfolioTile.From(view.ProviderRef, view.Portfolio);
        IsNotFound = false;
        IsNetworkError = false;
    }

    [RelayCommand]
    private void OpenPhoto(PortfolioTile? tile)
    {
        var index = tile is null ? -1 : Tiles.IndexOf(tile);
        if (index >= 0)
            PhotoRequested?.Invoke(this, new ShowcasePhotoRequestedEventArgs(Tiles, index));
    }

    [RelayCommand]
    private async Task BookAsync()
    {
        if (!CanBook)
            return;

        var email = await ResolveEmailAsync();
        if (email is null)
            return;

        BookRequested?.Invoke(this, new BookRequestedEventArgs
        {
            CounterpartEmail = email,
            CounterpartName = FullName,
            Profession = Showcase?.Professions.FirstOrDefault()
        });
    }

    [RelayCommand]
    private async Task MessageAsync()
    {
        if (!CanMessage)
            return;

        var email = await ResolveEmailAsync();
        if (email is null)
            return;

        MessageRequested?.Invoke(this, new CustomerSummary
        {
            Email = email,
            FullName = FullName,
            IsProvider = true,
            ProviderRef = ProviderRef,
            PhotoHash = Showcase?.PhotoHash ?? string.Empty,
            AvatarId = Showcase?.AvatarId ?? string.Empty
        });
    }

    [RelayCommand]
    private async Task ToggleSubscriptionAsync()
    {
        if (!CanSubscribe || Showcase is null)
            return;

        var email = await ResolveEmailAsync();
        if (email is null)
            return;

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var wasSubscribed = IsSubscribed;
            var ok = wasSubscribed
                ? await _customers.UnsubscribeAsync(_session.Email, email)
                : await _customers.SubscribeAsync(_session.Email, email);
            if (!ok)
            {
                ErrorMessage = AppResources.GetString(wasSubscribed ? "Error_Unsubscribe" : "Error_Subscribe");
                return;
            }

            Showcase.Relationship.IsSubscribed = !wasSubscribed;
            OnPropertyChanged(nameof(IsSubscribed));
            OnPropertyChanged(nameof(CanMessage));
            OnPropertyChanged(nameof(SubscribeLabel));
            OnPropertyChanged(nameof(ActionsReason));
            OnPropertyChanged(nameof(HasActionsReason));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Sends a report. Returns the message to show — thanks, or why it was not sent — or null when the input
    /// needs correcting first, with the reason in <see cref="ErrorMessage"/>.
    /// </summary>
    public async Task<string?> ReportAsync(ShowcaseReportReason reason, string? detail, string? portfolioHash = null)
    {
        ErrorMessage = string.Empty;
        var trimmed = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
        if (reason == ShowcaseReportReason.Other && trimmed is null)
        {
            ErrorMessage = AppResources.GetString("ShowcaseReport_DetailRequired");
            return null;
        }

        if (ShowcaseText.VisibleLength(trimmed) > ShowcaseReportReasons.DetailMaxLength)
        {
            ErrorMessage = AppResources.Format("ShowcaseReport_DetailTooLong", ShowcaseReportReasons.DetailMaxLength);
            return null;
        }

        var result = await _api.ReportAsync(ProviderRef, reason, trimmed, portfolioHash);
        return AppResources.GetString(result.IsSuccess ? "ShowcaseReport_Thanks" : "ShowcaseReport_Failed");
    }

    public static string ReasonLabel(ShowcaseReportReason reason) => AppResources.GetString(reason switch
    {
        ShowcaseReportReason.Inappropriate => "ShowcaseReport_Reason_Inappropriate",
        ShowcaseReportReason.NotTheirWork => "ShowcaseReport_Reason_NotTheirWork",
        ShowcaseReportReason.Spam => "ShowcaseReport_Reason_Spam",
        _ => "ShowcaseReport_Reason_Other"
    });

    public string HidePrompt => AppResources.Format("ShowcaseHide_Confirm", string.IsNullOrWhiteSpace(FirstName)
        ? AppResources.GetString("ShowcaseHide_ThisProvider")
        : FirstName);

    /// <summary>Hides the provider after the page has confirmed. Returns whether it was hidden.</summary>
    public async Task<bool> HideAsync()
    {
        if (!CanHide)
            return false;

        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            var result = await _api.HideAsync(ProviderRef);
            if (!result.IsSuccess)
            {
                ErrorMessage = ShowcaseErrorCopy.Describe(result);
                return false;
            }

            Hidden?.Invoke(this, FirstName);
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> UnhideAsync()
    {
        var result = await _api.UnhideAsync(ProviderRef);
        return result.IsSuccess;
    }

    private async Task<string?> ResolveEmailAsync()
    {
        if (!string.IsNullOrWhiteSpace(ProviderEmail))
            return ProviderEmail;

        try
        {
            var providers = await _providers.GetProvidersAsync();
            var match = providers.FirstOrDefault(p =>
                string.Equals(p.ProviderRef, ProviderRef, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !string.IsNullOrWhiteSpace(match.Email))
            {
                ProviderEmail = match.Email;
                return ProviderEmail;
            }
        }
        catch (Exception)
        {
        }

        ErrorMessage = AppResources.GetString("Showcase_ContactUnavailable");
        return null;
    }
}
