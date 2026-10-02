namespace AgendaBuddy.Calendar.Domain.Queries;

/// <summary>Whether the caller has a calendar feed, and since when. Never carries the token or its URL.</summary>
[ExcludeFromCodeCoverage]
public class GetCalendarFeedStatusQuery : IRequest<Result<CalendarFeedStatus>>
{
    public required string OwnerEmail { get; set; }
}

[ExcludeFromCodeCoverage]
public sealed record CalendarFeedStatus(bool Enabled, DateTime? CreatedAt);
