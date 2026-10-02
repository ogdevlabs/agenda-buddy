using System.Diagnostics.CodeAnalysis;

namespace AgendaBuddy.Calendar.Requests;

[ExcludeFromCodeCoverage]
public class CalendarFeedRequest
{
    /// <summary>The language event titles are written in, e.g. <c>es-MX</c>. Anything not Spanish is English.</summary>
    public string? Language { get; set; }
}

[ExcludeFromCodeCoverage]
public sealed record CalendarFeedLinkResponse(string Url, DateTime CreatedAt);
