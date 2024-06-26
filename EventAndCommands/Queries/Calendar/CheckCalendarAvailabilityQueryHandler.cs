namespace EventAndCommands.Queries.Calendar;

[RegisterService(ServiceLifetime.Scoped)]
public class CheckCalendarAvailabilityQueryHandler(
    IMediator mediator,
    ProviderService providerService,
    string email)
    : IRequestHandler<CheckCalendarAvailabilityQuery, List<DateTime>>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();

    public async Task<List<DateTime>> Handle(CheckCalendarAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new CheckCalendarAvailabilityEvent { Email = email }, cancellationToken);

        var filterProvider = SupportTools<ProviderEntity>.FilterByEmail(email);
        var providerEntity = await providerService.FindProviders(filterProvider);
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
            await EventStore!.SaveAsync(successEvent);
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
        await EventStore!.SaveAsync(failEvent);
        return null!;
    }
}