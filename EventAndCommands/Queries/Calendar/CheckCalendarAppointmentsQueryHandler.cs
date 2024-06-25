namespace EventAndCommands.Queries.Calendar;

[RegisterService(ServiceLifetime.Scoped)]
    
public class
    CheckCalendarAppointmentsQueryHandler(IMediator mediator,
        ProviderService providerService,
        string email) : IRequestHandler<CheckCalendarAppointmentsQuery, List<AppointmentEntity>>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();
    
    public async Task<List<AppointmentEntity>> Handle(CheckCalendarAppointmentsQuery request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new CheckCalendarAppointmentsEvent { Email = email }, cancellationToken);
        var filterProvider = SupportTools<ProviderEntity>.FilterByEmail(email);
        var providerEntity = await providerService.FindProviders(filterProvider);
        if (providerEntity != null)
        {
            var providerAppointmentCollection = providerEntity.AppointmentEntities;
            var @successEvent = new Event()
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "CheckCalendarAppointmentsQuery",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await EventStore!.SaveAsync(@successEvent);
            return providerAppointmentCollection;
        }
        var @failEvent = new Event()
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "CheckCalendarAppointmentsQuery",
            Data = JsonSerializer.Serialize(new List<AppointmentEntity>())
        };
        await EventStore!.SaveAsync(@failEvent);
        return null!;
    }
}