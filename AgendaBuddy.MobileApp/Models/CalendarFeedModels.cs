namespace AgendaBuddy.MobileApp.Models;

/// <summary>Whether the account's feed is on. The server never returns the link here; it is shown once, on enable.</summary>
public sealed record CalendarFeedStatus(bool Enabled, DateTime? CreatedAt);

/// <summary>The feed's secret link, as issued by enable.</summary>
public sealed record CalendarFeedLink(string Url, DateTime? CreatedAt);
