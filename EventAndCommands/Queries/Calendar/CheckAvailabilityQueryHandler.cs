using EventAndCommands.Events.Calendar;

namespace EventAndCommands.Queries.Calendar;

public class CheckAvailabilityQueryHandler(IMediator mediator, ProviderService providerService, string email)
    : IRequestHandler<CheckAvailabilityQuery, List<AppointmentEntity>>
{
    [InjectService] private EventStore? EventStore { get; } = new EventStore();

    public async Task<List<AppointmentEntity>> Handle(CheckAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new CheckAvailabilityEvent(){Email = email }, cancellationToken);
        var filter = SupportTools<ProviderEntity>.FilterByEmail(email);
        var providerEntity = await providerService.FindProviders(filter);
        if (providerEntity != null)
        {
            var @successEvent = new Event()
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "CheckAvailabilityQuery",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await EventStore!.SaveAsync(@successEvent);
            return GetMonthTimeAppointmentEntities(providerEntity);
        }
        var @failEvent = new Event()
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "CheckAvailabilityQuery",
            Data = JsonSerializer.Serialize(new ProviderEntity())
        };
        await EventStore!.SaveAsync(@failEvent);
        return [];
    }

    private static List<AppointmentEntity> GetMonthTimeAppointmentEntities(ProviderEntity providerEntity)
    {
        var appointmentEntities = providerEntity.AppointmentEntities;
        DateTime now = DateTime.Now;
        DateTime thirtyDaysAgo = now.AddDays(-30);
        
        var last30DaysAppointments = appointmentEntities
            .Where(appointment => appointment.Appointment >= thirtyDaysAgo)
            .ToList();
        return last30DaysAppointments;

    }
}