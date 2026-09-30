using AgendaBuddy.Library.Showcase;

namespace AgendaBuddy.Provider.Domain.Showcase;

public sealed record PortfolioItemView(
    string Hash, string? Caption, string? ServiceId, int Width, int Height, DateTime AddedAt)
{
    public static PortfolioItemView From(PortfolioItem item) =>
        new(item.Hash, item.Caption, item.ServiceId?.ToString(), item.Width, item.Height, item.AddedAt);
}

/// <summary>The owner's view of their own showcase. Holds no customer address: the funnel is counts only.</summary>
public sealed record ShowcaseOwnerView(
    string ProviderRef,
    string? PublicCode,
    string? Tagline,
    string? About,
    string? PhotoHash,
    string? LogoHash,
    IReadOnlyList<PortfolioItemView> Portfolio,
    ShowcaseCompleteness Completeness,
    ShowcaseFunnel Funnel,
    bool TakenDown);

public sealed record PublicPortfolioItemView(
    string Hash, string? Caption, string? ServiceId, int Width, int Height, bool IsNew);

public sealed record ShowcaseServiceView(
    string Id, string Name, decimal Fee, string FeeType, int? DurationMinutes);

public sealed record NextAppointmentView(string Identifier, DateTime ScheduledAt, string? ServiceName);

public sealed record RelationshipView(
    bool IsSelf, bool IsSubscribed, NextAppointmentView? NextAppointment, bool HasBookedBefore);

/// <summary>
/// A showcase as any signed-in user sees it. By construction it has no field for the provider's email, phone,
/// appointments or customers.
/// </summary>
public sealed record ShowcaseView(
    string ProviderRef,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Professions,
    string? Tagline,
    string? About,
    string? PhotoHash,
    string? LogoHash,
    string AvatarId,
    IReadOnlyList<PublicPortfolioItemView> Portfolio,
    IReadOnlyList<ShowcaseServiceView> Services,
    RelationshipView Relationship);

public sealed record PublicCodeView(string Code, string Url);

public sealed record MediaUploadView(string Hash, int Width, int Height, bool Deduplicated);
