using System.Globalization;

namespace AgendaBuddy.Library.Showcase;

public static class ShowcaseRules
{
    public const int MaxPortfolioItems = 20;
    public const int MaxTagline = 80;
    public const int MaxAbout = 600;
    public const int MaxCaption = 140;
    public const int MaxReportDetail = 500;
    public const int MaxLookupEmails = 50;
    public const int UploadsPerHour = 60;
    public static readonly TimeSpan VisitDebounce = TimeSpan.FromHours(24);
    public static readonly TimeSpan ReportDebounce = TimeSpan.FromHours(24);
    public static readonly TimeSpan FunnelBookingWindow = TimeSpan.FromDays(7);

    /// <summary>Trimmed, and an empty result is <c>null</c> so "cleared" has one representation.</summary>
    public static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// Counted in grapheme clusters, not UTF-16 units, so an emoji or an accented letter counts as the one
    /// character a person sees.
    /// </summary>
    public static bool FitsWithin(string? value, int max) =>
        value is null || new StringInfo(value).LengthInTextElements <= max;

    public static bool IsHash(string? value) =>
        value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
