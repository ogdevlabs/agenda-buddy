using EventAndCommands.Events.Calendar;

namespace EventAndCommands.Queries.Calendar;

public class CheckCalendarAvailabilityQueryHandler(IMediator mediator, ProviderService providerService, string email)
    : IRequestHandler<CheckCalendarAvailabilityQuery, List<AppointmentEntity>>
{
    [InjectService] private EventStore? EventStore { get; } = new EventStore();

    public async Task<List<AppointmentEntity>> Handle(CheckCalendarAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new CheckCalendarAvailabilityEvent(){Email = email }, cancellationToken);
        var filter = SupportTools<ProviderEntity>.FilterByEmail(email);
        var providerEntity = await providerService.FindProviders(filter);
        if (providerEntity != null)
        {
            var @successEvent = new Event()
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "CheckCalendarAvailabilityQuery",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await EventStore!.SaveAsync(@successEvent);
            return GetThirtyDaysCalendarAvailability(providerEntity);
        }
        var @failEvent = new Event()
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "CheckCalendarAvailabilityQuery",
            Data = JsonSerializer.Serialize(new ProviderEntity())
        };
        await EventStore!.SaveAsync(@failEvent);
        return [];
    }

    private static List<AppointmentEntity> GetThirtyDaysCalendarAvailability(ProviderEntity providerEntity)
    {
        var appointmentEntities = providerEntity.AppointmentEntities;
        DateTime now = DateTime.Now;
        DateTime thirtyDaysAgo = now.AddDays(+30);
        
        var last30DaysAppointments = appointmentEntities
            .Where(appointment => appointment.Appointment >= thirtyDaysAgo)
            .ToList();
        return last30DaysAppointments;

    }
}