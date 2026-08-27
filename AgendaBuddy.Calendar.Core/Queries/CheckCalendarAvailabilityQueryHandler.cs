namespace AgendaBuddy.Calendar.Core.Queries;

// F-020-T08. Constructor takes only DI-resolvable services; the per-request email comes from the
// query, not a constructor parameter (the previous shape -- RequestCollection.cs constructed this
// handler by hand, once per call, passing `email` into the constructor -- meant the handler could
// never be dispatched through a real mediator.Send). Typed against IProviderService, not the
// concrete class: it already covers everything this handler calls. Unlike Booking's Book/ChangeStatus
// handlers, this one has no genuine gap against the interface -- and, unlike the route wiring in
// Calendar/Program.cs before this task, it never touched ICalendarService/CalendarService at all; that
// parameter was threaded through Program.cs -> EventHelper -> RequestCollection and used by neither
// query handler, so it has no call site here.
public class CheckCalendarAvailabilityQueryHandler(
    IMediator mediator,
    IProviderService providerService,
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

        var slots = SupportTools<ProviderEntity>.GetThirtyDaysCalendarAvailability(providerEntity);
        await eventStore.SaveAsync(QueryAudit.Success(nameof(CheckCalendarAvailabilityQuery), slots.Count));
        return Result.Ok(slots);
    }
}
