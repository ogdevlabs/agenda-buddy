namespace EventAndCommands.Queries.Calendar;

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
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "CheckCalendarAvailabilityQuery",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await eventStore.SaveAsync(successEvent);
            var res = SupportTools<ProviderEntity>.GetThirtyDaysCalendarAvailability(providerEntity);
            res.ForEach(timeslot => Console.WriteLine(timeslot));
            return res;
        }

        var failEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "CheckCalendarAvailabilityQuery",
            Data = JsonSerializer.Serialize(new ProviderEntity())
        };
        await eventStore.SaveAsync(failEvent);
        return null!;
    }
}