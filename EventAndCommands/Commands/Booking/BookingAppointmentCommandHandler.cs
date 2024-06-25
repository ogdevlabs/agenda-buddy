namespace EventAndCommands.Commands.Booking;

[RegisterService(ServiceLifetime.Scoped)]
public class BookingAppointmentCommandHandler(
    IMediator mediator,
    KafkaClient? kafkaClient,
    ProviderService providerService,
    BookingService bookingService,
    AppointmentEntity appointmentEntity)
    : IRequestHandler<BookAppointmentCommand, string>
{
    [InjectService] private IEventStore EventStore { get; } = new EventStore();

    public async Task<string> Handle(BookAppointmentCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new BookAppointmentEvent { AppointmentEntity = appointmentEntity }, cancellationToken);

        if (await SearchAndUpdateProviderAppointments())
        {
            var @successEvent = new Event()
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "BookAppointmentCommand",
                Data = JsonSerializer.Serialize(appointmentEntity)
            };
            await EventStore!.SaveAsync(@successEvent);
            return await Task.FromResult(appointmentEntity.ToJson());
        }

        var @failEvent = new Event()
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "BookAppointmentCommand",
            Data = JsonSerializer.Serialize(appointmentEntity)
        };
        await EventStore!.SaveAsync(@failEvent);
        return null!;
    }

    private async Task<bool> SearchAndUpdateProviderAppointments()
    {
        var filter = SupportTools<ProviderEntity>.FilterByEmail(appointmentEntity.EmailProvider);
        var providerEntity = await providerService.FindProviders(filter);
        if (providerEntity == null) return false;
        if (providerEntity.Email == appointmentEntity.EmailProvider)
        {
            await AddAppointmentToCalendar();
            providerEntity.AppointmentEntities.Add(await bookingService.SearchAppointment(appointmentEntity.Identifier));
            return await providerService.UpdateProvider(providerEntity.Id.ToString(), providerEntity);
        }

        return false;
    }

    private async Task AddAppointmentToCalendar()
    {
        await bookingService.BookAppointment(appointmentEntity);
    }
}