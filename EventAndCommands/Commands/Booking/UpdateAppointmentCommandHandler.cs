#pragma warning disable CS9113 // Primary constructor parameter unused — kafkaClient reserved for future Kafka publishing
namespace EventAndCommands.Commands.Booking;

public class UpdateAppointmentCommandHandler(
    IMediator mediator,
    KafkaClient? kafkaClient,
    ProviderService providerService,
    BookingService bookingService,
    AppointmentEntity appointmentEntity,
    IEventStore eventStore) : IRequestHandler<UpdateAppointmentCommand, string>
{

    public async Task<string> Handle(UpdateAppointmentCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new UpdateAppointmentEvent { AppointmentEntity = appointmentEntity },
            cancellationToken);
        if (await SearchAndUpdateAppointment())
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "UpdateAppointmentCommand",
                Data = JsonSerializer.Serialize(appointmentEntity)
            };
            await eventStore.SaveAsync(successEvent);
            return await Task.FromResult(appointmentEntity.ToJson());
        }

        var failEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "UpdateAppointmentCommand",
            Data = JsonSerializer.Serialize(appointmentEntity)
        };
        await eventStore.SaveAsync(failEvent);
        return null!;
    }

    private async Task<bool> SearchAndUpdateAppointment()
    {
        var identifier = appointmentEntity.Identifier;
        var filter = SupportTools<ProviderEntity>.FilterByEmail(appointmentEntity.EmailProvider);
        var provider = await providerService.FindProvidersAsync(filter);
        if (provider == null) return false;
        var appointment = provider.AppointmentEntities.FirstOrDefault(ap => ap.Identifier == identifier);
        if (appointment == null) return false;
        appointment.AppointmentStatus = appointmentEntity.AppointmentStatus;
        appointment.AppointmentDescription = appointmentEntity.AppointmentDescription;
        appointment.Start = appointmentEntity.Start;
        appointment.End = appointmentEntity.End;
        var updateAppointment = await UpdateAppointment(identifier, appointment);
        if (!updateAppointment) return false;
        return await providerService.UpdateProviderAsync(provider.Id.ToString(), provider);
    }

    private async Task<bool> UpdateAppointment(string identifier, AppointmentEntity appointment)
    {
        return await bookingService.UpdateAppointmentAsync(identifier, appointment);
    }
}