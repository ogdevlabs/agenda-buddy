using CommunityToolkit.Mvvm.ComponentModel;
using AgendaBuddy.Library.Avatars;

namespace AgendaBuddy.MobileApp.Models;

public partial class CustomerSummary : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string LastSession { get; set; } = string.Empty;
    public int TotalSessions { get; set; }
    public bool IsProvider { get; set; }
    public string Availability { get; set; } = string.Empty;

    /// <summary>
    /// Whether this contact can be messaged. Only a subscribed provider, or a customer the provider is
    /// subscribed to — the directory is browsable so customers can find someone to book, but browsable must
    /// not imply messageable. The server enforces the same rule; this only keeps the button off a row where
    /// tapping it would be refused.
    /// </summary>
    [ObservableProperty]
    private bool _canMessage;

    /// <summary>Only meaningful when <see cref="IsProvider"/> — their own selected Professions.</summary>
    public List<string> Professions { get; set; } = new();

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>Only meaningful when <see cref="IsProvider"/> — whether the signed-in Customer is subscribed.</summary>
    [ObservableProperty]
    private bool _isSubscribed;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Whatever the server assigned this account, empty when it has none.</summary>
    public string AvatarId { get; set; } = string.Empty;

    /// <summary>
    /// The image to draw for this contact.
    /// </summary>
    /// <remarks>
    /// Replaces an initial in a coloured circle, which was the name again, smaller — and made every contact
    /// sharing a first letter look identical down a list. Keyed on the email rather than the name when the
    /// server assigned nothing, so it is stable across devices and does not change when somebody edits their
    /// profile. <c>.png</c> because MAUI rasterises the committed <c>.svg</c> at build time and the asset is
    /// referenced by its raster name.
    /// </remarks>
    public string AvatarAsset => $"{AvatarCatalog.Resolve(AvatarId, Email)}.png";
    public string SessionsLabel => IsProvider ? $"{TotalSessions} services" : $"{TotalSessions} sessions";
}
