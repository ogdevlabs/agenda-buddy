using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

/// <summary>
/// The account's subscribed calendar feed. Every method answers <c>null</c> (or <c>false</c>) when the server could
/// not be reached, which is distinct from "off": a dropped connection must not read as the feed being turned off.
/// </summary>
public interface ICalendarFeedApiService
{
    Task<CalendarFeedStatus?> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Turns the feed on or replaces its link. The returned link is the only time the server sends it.</summary>
    Task<CalendarFeedLink?> EnableAsync(string language, CancellationToken ct = default);

    Task<bool> DisableAsync(CancellationToken ct = default);
}
