using System.Diagnostics;
using EventAndCommands.Events.Booking;

namespace EventAndCommands.Commands.Booking;

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
        if (await UpdateProviderAppointments())
        {
            await AddAppointmentToCalendar();
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

    private static BsonDocument GetFilterByEmail(string email)
    {
        return SupportTools<ProviderEntity>.FilterByEmail(email);
    }

    private async Task<bool> UpdateProviderAppointments()
    {
        try
        {
            var provider = await providerService.FindProviders(GetFilterByEmail(appointmentEntity.EmailProvider));
            if (provider.Email == appointmentEntity.EmailProvider)
            {
                return await providerService.UpdateProvider(provider.Id.ToString(), provider);
            }
        }
        catch (Exception)
        {
            return false;
        }
        return false;
    }

    private async Task AddAppointmentToCalendar()
    {
        await bookingService.BookAppointment(appointmentEntity);
    }
}