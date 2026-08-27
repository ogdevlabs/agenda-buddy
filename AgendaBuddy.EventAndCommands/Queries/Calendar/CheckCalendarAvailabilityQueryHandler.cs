namespace AgendaBuddy.EventAndCommands.Queries.Calendar;

public class CheckCalendarAvailabilityQueryHandler(
    IMediator mediator,
    ProviderService providerService,
    string email,
    IEventStore eventStore)
    : IRequestHandler<CheckCalendarAvailabilityQuery, List<DateTime>>
{

    public async Task<List<DateTime>> Handle(CheckCalendarAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new CheckCalendarAvailabilityEvent { Email = email }, cancellationToken);

        var filterProvider = SupportTools<ProviderEntity>.FilterByEmail(email);
        var providerEntity = await providerService.FindProvidersAsync(filterProvider);
        if (providerEntity != null)
        {
            var res = SupportTools<ProviderEntity>.GetThirtyDaysCalendarAvailability(providerEntity);
            res.ForEach(timeslot => Console.WriteLine(timeslot));

            // Audited after the result exists, so the count reflects the slots disclosed.
            await eventStore.SaveAsync(QueryAudit.Success("CheckCalendarAvailabilityQuery", res.Count));
            return res;
        }

        await eventStore.SaveAsync(QueryAudit.Failure("CheckCalendarAvailabilityQuery"));
        return null!;
    }
}
