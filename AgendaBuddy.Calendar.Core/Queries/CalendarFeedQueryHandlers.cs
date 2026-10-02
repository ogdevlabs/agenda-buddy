namespace AgendaBuddy.Calendar.Core.Queries;

public class GetCalendarFeedStatusQueryHandler(
    ICalendarFeedService calendarFeedService,
    IEventStore eventStore)
    : IRequestHandler<GetCalendarFeedStatusQuery, Result<CalendarFeedStatus>>
{
    public async Task<Result<CalendarFeedStatus>> Handle(
        GetCalendarFeedStatusQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var feed = await calendarFeedService.GetAsync(request.OwnerEmail);

        await eventStore.SaveAsync(QueryAudit.Success(nameof(GetCalendarFeedStatusQuery), feed is null ? 0 : 1));
        return Result.Ok(new CalendarFeedStatus(feed is not null, feed?.CreatedAt));
    }
}
