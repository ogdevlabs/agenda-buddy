#pragma warning disable CS9113 // Primary constructor parameter unused — kafkaClient reserved for future Kafka publishing
namespace EventAndCommands.Commands.Booking;

public class CancelAppointmentCommandHandler(
    IMediator mediator,
    KafkaClient? kafkaClient,
    ProviderService providerService,
    BookingService bookingService,
    string appointmentIdentifier,
    IEventStore eventStore) : IRequestHandler<CancelAppointmentCommand, string>
{

    public async Task<string> Handle(CancelAppointmentCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new CancelAppointmentEvent { Identifier = appointmentIdentifier },
            cancellationToken);
        var appointmentEntity = await bookingService.SearchAppointmentAsync(appointmentIdentifier);
        if (appointmentEntity != null)
            if (await SearchAndCancelAppointment(appointmentIdentifier))
            {
                var successEvent = new Event
                {
                    Id = ObjectId.GenerateNewId(),
                    TimeStamp = DateTime.UtcNow,
                    Status = "Success",
                    Type = "CancelAppointmentCommand",
                    Data = JsonSerializer.Serialize(appointmentEntity)
                };
                await eventStore.SaveAsync(successEvent);
                return appointmentEntity.ToJson();
            }

        var failEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "CancelAppointmentCommand",
            Data = JsonSerializer.Serialize(appointmentEntity ?? new AppointmentEntity
            {
                EmailProvider = "",
                EmailCustomer = ""
            })
        };
        await eventStore.SaveAsync(failEvent);
        return null!;
    }

    private async Task<bool> SearchAndCancelAppointment(string identifier)
    {
        var appointment = await bookingService.SearchAppointmentAsync(identifier);
        var filter = SupportTools<ProviderEntity>.FilterByEmail(appointment.EmailProvider);
        var provider = await providerService.FindProvidersAsync(filter);
        if (provider == null) return false;
        var appointmentToRemove = provider.AppointmentEntities.SingleOrDefault(ap => ap.Identifier == identifier);
        if (appointmentToRemove == null) return false;
        var cancelAppointment = await CancelAppointment(identifier);
        if (!cancelAppointment) return false;
        provider.AppointmentEntities.Remove(appointmentToRemove);
        return await providerService.UpdateProviderAsync(provider.Id.ToString(), provider);
    }

    private async Task<bool> CancelAppointment(string identifier)
    {
        var appointment = await bookingService.SearchAppointmentAsync(identifier);
        if (appointment.AppointmentStatus == AppointmentStatus.Booked) return false;
        if (appointment.AppointmentStatus == AppointmentStatus.Completed) return false;
        return await bookingService.CancelAppointmentAsync(identifier);
    }
}