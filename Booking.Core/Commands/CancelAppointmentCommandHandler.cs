#pragma warning disable CS9113 // Primary constructor parameter unused — kafkaClient reserved for future Kafka publishing
namespace Booking.Core.Commands;

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

        // F-014 requirement 15 / Discover finding F-3. This used to refuse a BOOKED appointment as well as a
        // completed one, which is backwards: a booked appointment is exactly what a customer needs to be able
        // to cancel, while a completed one is history. The bug was invisible because nothing in production
        // ever set Booked — the status transitions were unenforced (threat T-203), so every appointment sat
        // in Requested forever and cancellation happened to work. Making transitions real activates this,
        // which is why both are fixed in the same feature: shipped separately, the status fix would have
        // looked like the cause of "customers can no longer cancel their appointments".
        if (appointment.AppointmentStatus == AppointmentStatus.Completed) return false;

        return await bookingService.CancelAppointmentAsync(identifier);
    }
}
