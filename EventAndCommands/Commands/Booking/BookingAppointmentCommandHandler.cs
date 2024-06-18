using EventAndCommands.Events.Booking;

namespace EventAndCommands.Commands.Booking;

public class BookingAppointmentCommandHandler(
    IMediator mediator,
    KafkaClient kafkaClient,
    ProviderService providerService,
    CalendarService calendarService,
    AppointmentEntity appointmentEntity)
    : IRequestHandler<BookAppointmentCommand, string>
{
    [InjectService] private IEventStore EventStore { get; } = new EventStore();

    public async Task<string> Handle(BookAppointmentCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new BookAppointmentEvent { AppointmentEntity = appointmentEntity }, cancellationToken);
        var provider = await providerService.FindProviders(GetFilterByEmail(appointmentEntity.EmailProvider));
        throw new NotImplementedException();
    }

    private static BsonDocument GetFilterByEmail(string email)
    {
        return SupportTools<ProviderEntity>.FilterByEmail(email);
    }

    private static void UpdateProviderAppointments()
    {
        
    }

    private static async Task AddAppointmentToCalendar(AppointmentEntity appointmentEntity, CalendarService calendarService)
    {
        
    }
}