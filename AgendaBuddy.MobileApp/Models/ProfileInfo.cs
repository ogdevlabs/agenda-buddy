using AgendaBuddy.MobileApp.Infrastructure;

namespace AgendaBuddy.MobileApp.Models;

/// <summary>Minimal editable profile fields shared by Customer and Provider accounts.</summary>
public class ProfileInfo
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Optional contact number. The only fallback channel either party has when a session is about to be
    /// missed, which is why registration asks for it rather than leaving it to be discovered later.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// The avatar the server has on record, or empty for an account that has never been assigned one.
    /// </summary>
    /// <remarks>
    /// Empty is normal, not an error: every account predating avatar assignment has none, and
    /// <see cref="AvatarAsset"/> falls back to a stable derivation from the address rather than a blank circle.
    /// </remarks>
    public string AvatarId { get; set; } = string.Empty;

    /// <summary>When this account last confirmed the Terms and Conditions, or <c>null</c> for never.</summary>
    public DateTime? TermsAcceptedAt { get; set; }

    /// <summary>When this account last confirmed the Privacy Policy, or <c>null</c> for never.</summary>
    public DateTime? PrivacyAcceptedAt { get; set; }

    /// <summary>Whether both documents are currently accepted.</summary>
    public bool HasAcceptedLegal => TermsAcceptedAt is not null && PrivacyAcceptedAt is not null;

    /// <summary>The image name to bind to an avatar. Resolved through the one shared resolver.</summary>
    public string AvatarAsset => AvatarSource.For(AvatarId, Email);

    /// <summary>First and last name joined, or an empty string when neither is set.</summary>
    public string FullName =>
        string.Join(' ', new[] { FirstName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
}
