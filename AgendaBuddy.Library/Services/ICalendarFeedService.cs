namespace AgendaBuddy.Library.Services;

public sealed record EnabledCalendarFeed(string Token, DateTime CreatedAt);

/// <summary>
/// The per-account iCalendar subscription feed (ADR-073/074).
/// </summary>
public interface ICalendarFeedService
{
    /// <summary>
    /// Issues a new feed token for <paramref name="ownerEmail"/>, replacing any existing one so every previously
    /// shared URL stops working. The plain token is returned here and nowhere else.
    /// </summary>
    Task<EnabledCalendarFeed> EnableAsync(string ownerEmail, string? language);

    Task<CalendarFeedEntity?> GetAsync(string ownerEmail);

    /// <summary>Removes the owner's feed. Idempotent: true only when something was removed.</summary>
    Task<bool> DisableAsync(string ownerEmail);

    /// <summary>
    /// Renders the calendar a token addresses, or null for a malformed, unknown or revoked token — the caller must not
    /// be able to tell those apart.
    /// </summary>
    Task<string?> RenderAsync(string token, DateTime nowUtc);
}
