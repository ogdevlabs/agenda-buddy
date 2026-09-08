namespace AgendaBuddy.Calendar.Core.Queries;

// Constructor takes only DI-resolvable services; the per-request email comes from the query, not a
// constructor parameter (the previous shape -- RequestCollection.cs constructed this handler by
// hand, once per call, passing `email` into the constructor -- meant the handler could never be
// dispatched through a real mediator.Send). Typed against IProviderService, not the concrete class:
// it already covers everything this handler calls.
public class CheckCalendarAvailabilityQueryHandler(
    IMediator mediator,
    IProviderService providerService,
    ICalendarBlockService calendarBlockService,
    IEventStore eventStore) : IRequestHandler<CheckCalendarAvailabilityQuery, Result<List<DateTime>>>
{
    public async Task<Result<List<DateTime>>> Handle(CheckCalendarAvailabilityQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new CheckCalendarAvailabilityEvent { Email = request.Email }, cancellationToken);

        var filterProvider = SupportTools<ProviderEntity>.FilterByEmail(request.Email);
        var providerEntity = await providerService.FindProvidersAsync(filterProvider);
        if (providerEntity is null)
        {
            await eventStore.SaveAsync(QueryAudit.Failure(nameof(CheckCalendarAvailabilityQuery)));
            return Result.Fail<List<DateTime>>("No provider found for this email.");
        }

        // Slot length follows the chosen service, so a 90-minute service is never offered a start time
        // that would run into the next appointment. An unknown/absent service falls back to the default
        // length rather than yielding an empty calendar.
        var duration = providerEntity.ServiceEntities
            ?.FirstOrDefault(service => string.Equals(service.Name, request.ServiceName, StringComparison.OrdinalIgnoreCase))
            ?.DurationMinutes
            ?? AvailabilityCalculator.DefaultDurationMinutes;

        // The provider's time off is subtracted here, so a blocked afternoon is never offered. The calculator
        // takes blocks as an optional argument for the sake of callers that have no block store; this route is
        // the customer-facing one, so omitting them is exactly the bug the blocks exist to prevent.
        var nowUtc = DateTime.UtcNow;
        var blocks = await calendarBlockService.GetBlocksAsync(request.Email, nowUtc);

        var slots = AvailabilityCalculator.GetAvailability(
            providerEntity, nowUtc, request.Days, duration, blocks);
        await eventStore.SaveAsync(QueryAudit.Success(nameof(CheckCalendarAvailabilityQuery), slots.Count));
        return Result.Ok(slots);
    }
}
