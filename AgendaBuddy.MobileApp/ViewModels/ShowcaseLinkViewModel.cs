using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.Routing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// The "See {FirstName}'s work" link on a screen that knows a provider only by email — an appointment, a booking,
/// a message thread. Resolved through the lookup route, which answers only for the caller's own counterparties,
/// and never by opening the showcase, which would record a visit nobody made.
/// </summary>
public partial class ShowcaseLinkViewModel : ObservableObject
{
    private readonly IShowcaseApiService _api;
    private string _resolvedEmail = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShowcase))]
    private string _providerRef = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoto))]
    private string _photoHash = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SeeWorkLabel))]
    private string _firstName = string.Empty;

    public ShowcaseLinkViewModel(IShowcaseApiService api) => _api = api;

    /// <summary>Which surface the link sits on, recorded with the visit for the provider's funnel.</summary>
    public ShowcaseSource Source { get; set; } = ShowcaseSource.Appointment;

    public event EventHandler<ShowcaseRequestedEventArgs>? ShowcaseRequested;

    [RelayCommand]
    private void Open()
    {
        if (!HasShowcase)
            return;
        ShowcaseRequested?.Invoke(this, new ShowcaseRequestedEventArgs(ProviderRef, Email, Source));
    }

    public bool HasShowcase => !string.IsNullOrEmpty(ProviderRef);
    public bool HasPhoto => !string.IsNullOrEmpty(PhotoHash);
    public string SeeWorkLabel => ShowcaseText.SeeWork(FirstName);

    /// <summary>Uses what the caller already knows; the lookup is only for screens that know nothing but the email.</summary>
    public void Preset(string email, string? fullName, string? providerRef, string? photoHash)
    {
        Email = email;
        FirstName = ShowcaseText.FirstNameOf(fullName);
        ProviderRef = providerRef ?? string.Empty;
        PhotoHash = photoHash ?? string.Empty;
        _resolvedEmail = HasShowcase ? email : string.Empty;
    }

    public async Task LoadAsync(string? email, string? fullName)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        FirstName = ShowcaseText.FirstNameOf(fullName);
        if (string.Equals(_resolvedEmail, email, StringComparison.OrdinalIgnoreCase) && HasShowcase)
            return;

        Email = email;
        try
        {
            var result = await _api.LookupAsync([email]);
            var entry = result.Value?.FirstOrDefault(e =>
                string.Equals(e.Email, email, StringComparison.OrdinalIgnoreCase));
            if (!result.IsSuccess || entry is null || string.IsNullOrWhiteSpace(entry.ProviderRef))
                return;

            ProviderRef = entry.ProviderRef;
            PhotoHash = entry.PhotoHash ?? string.Empty;
            _resolvedEmail = email;
        }
        catch (Exception)
        {
        }
    }
}
