namespace AgendaBuddy.Library.Showcase;

public enum ShowcaseWriteStatus
{
    Ok,
    AlreadyPresent,
    UnknownMedia,
    InvalidService,
    PortfolioFull,
    PortfolioChanged,
    NotFound
}

public sealed record ShowcaseWriteResult(ShowcaseWriteStatus Status, ProviderShowcaseEntity? Showcase = null,
    PortfolioItem? Item = null)
{
    public bool Succeeded => Status is ShowcaseWriteStatus.Ok or ShowcaseWriteStatus.AlreadyPresent;
}

public sealed record ShowcaseCompleteness(int Done, int Total, IReadOnlyList<string> Missing);

/// <summary>Counts only — the funnel never says who scanned, opened or booked.</summary>
public sealed record ShowcaseFunnel(long Scans, long Opened, long Booked, int WindowDays);

public sealed record ShowcaseAvatarLookup(string Email, string ProviderRef, string? PhotoHash, DateTime? PortfolioChangedAt);

public sealed record ShowcaseDirectoryEntry(string? PhotoHash, DateTime? PortfolioChangedAt);

public sealed record HiddenProvider(string ProviderRef, string FirstName, string LastName);

/// <summary>What a viewer's relationship to the provider is, computed per request.</summary>
public sealed record ShowcaseRelationship(bool IsSelf, bool IsSubscribed, AppointmentEntity? NextAppointment,
    bool HasBookedBefore);
