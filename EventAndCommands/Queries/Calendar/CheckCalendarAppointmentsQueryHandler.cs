namespace EventAndCommands.Queries.Calendar;

public class
    CheckCalendarAppointmentsQueryHandler(
        IMediator mediator,
        ProviderService providerService,
        string email,
        IEventStore eventStore) : IRequestHandler<CheckCalendarAppointmentsQuery, List<AppointmentEntity>>
{

    public async Task<List<AppointmentEntity>> Handle(CheckCalendarAppointmentsQuery request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new CheckCalendarAppointmentsEvent { Email = email }, cancellationToken);
        var filterProvider = SupportTools<ProviderEntity>.FilterByEmail(email);
        var providerEntity = await providerService.FindProvidersAsync(filterProvider);
        if (providerEntity != null)
        {
            var providerAppointmentCollection = providerEntity.AppointmentEntities;
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "CheckCalendarAppointmentsQuery",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await eventStore.SaveAsync(successEvent);
            return providerAppointmentCollection;
        }

        var failEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "CheckCalendarAppointmentsQuery",
            Data = JsonSerializer.Serialize(new List<AppointmentEntity>())
        };
        await eventStore.SaveAsync(failEvent);
        return null!;
    }
}