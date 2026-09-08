namespace AgendaBuddy.Calendar.Core.Queries;

/// <summary>A provider's own upcoming time off.</summary>
public class GetCalendarBlocksQueryHandler(
    ICalendarBlockService calendarBlockService,
    IEventStore eventStore)
    : IRequestHandler<GetCalendarBlocksQuery, Result<List<CalendarBlockEntity>>>
{
    public async Task<Result<List<CalendarBlockEntity>>> Handle(
        GetCalendarBlocksQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        // Bounded by "not finished yet" rather than returning every block ever recorded: a provider with years
        // of history would otherwise pay for all of it, and past time off is not actionable.
        var blocks = (await calendarBlockService.GetBlocksAsync(request.EmailProvider, DateTime.UtcNow)).ToList();

        await eventStore.SaveAsync(QueryAudit.Success(nameof(GetCalendarBlocksQuery), blocks.Count));
        return Result.Ok(blocks);
    }
}

/// <summary>
/// What a proposed block would strand. Read before creating one, so the cost is visible in advance.
/// </summary>
public class GetCalendarBlockConflictsQueryHandler(
    ICalendarBlockService calendarBlockService,
    IEventStore eventStore)
    : IRequestHandler<GetCalendarBlockConflictsQuery, Result<List<AppointmentEntity>>>
{
    public async Task<Result<List<AppointmentEntity>>> Handle(
        GetCalendarBlockConflictsQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        if (request.EndUtc <= request.StartUtc)
        {
            await eventStore.SaveAsync(QueryAudit.Failure(nameof(GetCalendarBlockConflictsQuery)));
            return Result.Fail<List<AppointmentEntity>>("A range must end after it starts.");
        }

        var conflicts = (await calendarBlockService.GetAffectedAppointmentsAsync(
            request.EmailProvider, request.StartUtc, request.EndUtc)).ToList();

        await eventStore.SaveAsync(
            QueryAudit.Success(nameof(GetCalendarBlockConflictsQuery), conflicts.Count));
        return Result.Ok(conflicts);
    }
}
