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

    /// <remarks>
    /// F-014 requirement 20 / ADR D-9. This used to read the provider, append to its embedded appointment
    /// list, and call <c>UpdateProviderAsync</c> — a whole-document <c>ReplaceOneAsync</c>. Two concurrent
    /// bookings for one provider both read, both appended, and the second replacement silently discarded the
    /// first appointment, which then existed in the `appointments` collection and not in the provider
    /// document. `ReportingService` counts from the embedded list, so the lost booking was the one that
    /// vanished from the dashboard. <c>AppendAppointmentAsync</c> is a single atomic <c>$push</c> with no
    /// read, so there is no window.
    /// </remarks>
    private async Task<bool> SearchAndUpdateProviderAppointments()
    {
        var filter = SupportTools<ProviderEntity>.FilterByEmail(appointmentEntity.EmailProvider);
        var providerEntity = await providerService.FindProvidersAsync(filter);
        if (providerEntity == null) return false;
        if (providerEntity.Email == appointmentEntity.EmailProvider)
        {
            await AddAppointmentToCalendar();

            var stored = await bookingService.SearchAppointmentAsync(appointmentEntity.Identifier);
            if (stored is null) return false;

            return await providerService.AppendAppointmentAsync(providerEntity.Email, stored) is not null;
        }

        return false;
    }

    private async Task AddAppointmentToCalendar()
    {
        await bookingService.BookAppointmentAsync(appointmentEntity);
    }
}