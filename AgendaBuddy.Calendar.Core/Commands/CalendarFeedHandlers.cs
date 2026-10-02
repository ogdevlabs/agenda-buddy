using AgendaBuddy.Calendar.Domain.Commands;

namespace AgendaBuddy.Calendar.Core.Commands;

public class EnableCalendarFeedCommandHandler(
    ICalendarFeedService calendarFeedService,
    IEventStore eventStore)
    : IRequestHandler<EnableCalendarFeedCommand, Result<EnabledCalendarFeed>>
{
    public async Task<Result<EnabledCalendarFeed>> Handle(
        EnableCalendarFeedCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var enabled = await calendarFeedService.EnableAsync(request.OwnerEmail, request.Language);

        // The owner and the time, never the token: an audit row must not hold a live subscription URL.
        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(EnableCalendarFeedCommand),
            Data = JsonSerializer.Serialize(new { request.OwnerEmail, enabled.CreatedAt })
        });

        return Result.Ok(enabled);
    }
}

public class DisableCalendarFeedCommandHandler(
    ICalendarFeedService calendarFeedService,
    IEventStore eventStore)
    : IRequestHandler<DisableCalendarFeedCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DisableCalendarFeedCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var removed = await calendarFeedService.DisableAsync(request.OwnerEmail);

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(DisableCalendarFeedCommand),
            Data = JsonSerializer.Serialize(new { request.OwnerEmail, Removed = removed })
        });

        return Result.Ok(removed);
    }
}
