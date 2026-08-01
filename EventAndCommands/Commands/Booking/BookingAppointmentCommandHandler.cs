#pragma warning disable CS9113 // Primary constructor parameter unused — kafkaClient reserved for future Kafka publishing
namespace EventAndCommands.Commands.Booking;

public class BookingAppointmentCommandHandler(
    IMediator mediator,
    KafkaClient? kafkaClient,
    ProviderService providerService,
    BookingService bookingService,
    AppointmentEntity appointmentEntity,
    IEventStore eventStore)
    : IRequestHandler<BookAppointmentCommand, string>
{

    public async Task<string> Handle(BookAppointmentCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new BookAppointmentEvent { AppointmentEntity = appointmentEntity }, cancellationToken);

        if (await SearchAndUpdateProviderAppointments())
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "BookAppointmentCommand",
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
            Type = "BookAppointmentCommand",
            Data = JsonSerializer.Serialize(appointmentEntity)
        };
        await eventStore.SaveAsync(failEvent);
        return null!;
    }

    private async Task<bool> SearchAndUpdateProviderAppointments()
    {
        var filter = SupportTools<ProviderEntity>.FilterByEmail(appointmentEntity.EmailProvider);
        var providerEntity = await providerService.FindProvidersAsync(filter);
        if (providerEntity == null) return false;
        if (providerEntity.Email == appointmentEntity.EmailProvider)
        {
            await AddAppointmentToCalendar();
            providerEntity.AppointmentEntities.Add(
                await bookingService.SearchAppointmentAsync(appointmentEntity.Identifier));
            return await providerService.UpdateProviderAsync(providerEntity.Id.ToString(), providerEntity);
        }

        return false;
    }

    private async Task AddAppointmentToCalendar()
    {
        await bookingService.BookAppointmentAsync(appointmentEntity);
    }
}