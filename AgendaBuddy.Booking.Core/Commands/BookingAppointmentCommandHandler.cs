namespace AgendaBuddy.Booking.Core.Commands;

// F-019-T04. Constructor takes only DI-resolvable services -- the per-request AppointmentEntity that
// used to be a constructor parameter (so RequestCollection.cs could hand-construct this handler) now
// comes from the command itself, which is what makes a real mediator.Send(command, ct) dispatch
// possible: MediatR resolves this handler from the container, which has no way to supply a per-request
// value through the constructor. Returns FluentResults.Result<AppointmentEntity> instead of a
// string-sniffed "exception"-prefixed convention (PRD Requirement 5).
//
// Stays on the concrete ProviderService/BookingService, not IProviderService/IBookingService (unlike
// Update/Cancel's handlers, Party Review): this one calls AppendAppointmentAsync, which isn't on
// IProviderService, and adding it would be a Library change out of this feature's scope. The
// constructor's own unused KafkaClient/IKafkaClient parameter -- "reserved for future Kafka
// publishing" -- was removed here too (Party Review, Neo's YAGNI finding): nothing consumed it, and
// leaving it in would have had F-020 copy the same dead parameter into six more handlers.
public class BookingAppointmentCommandHandler(
    IMediator mediator,
    ProviderService providerService,
    BookingService bookingService,
    IEventStore eventStore)
    : IRequestHandler<BookAppointmentCommand, Result<AppointmentEntity>>
{
    public async Task<Result<AppointmentEntity>> Handle(BookAppointmentCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var appointmentEntity = request.AppointmentEntity;
        await mediator.Publish(new BookAppointmentEvent { AppointmentEntity = appointmentEntity }, cancellationToken);

        if (await SearchAndUpdateProviderAppointments(appointmentEntity))
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
            return Result.Ok(appointmentEntity);
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
        return Result.Fail<AppointmentEntity>($"No provider found for {appointmentEntity.EmailProvider}");
    }

    /// <remarks>
    /// F-014 requirement 20 / ADR D-9. This used to read the provider, append to its embedded appointment
    /// list, and call <c>UpdateProviderAsync</c> — a whole-document <c>ReplaceOneAsync</c>. Two concurrent
    /// bookings for one provider both read, both appended, and the second replacement silently discarded the
    /// first appointment, which then existed in the `appointments` collection and not in the provider
    /// document. <c>ReportingService</c> counts from the embedded list, so the lost booking was the one that
    /// vanished from the dashboard. <c>AppendAppointmentAsync</c> is a single atomic <c>$push</c> with no
    /// read, so there is no window.
    /// </remarks>
    private async Task<bool> SearchAndUpdateProviderAppointments(AppointmentEntity appointmentEntity)
    {
        var filter = SupportTools<ProviderEntity>.FilterByEmail(appointmentEntity.EmailProvider);
        var providerEntity = await providerService.FindProvidersAsync(filter);
        if (providerEntity == null) return false;
        if (providerEntity.Email == appointmentEntity.EmailProvider)
        {
            await AddAppointmentToCalendar(appointmentEntity);

            var stored = await bookingService.SearchAppointmentAsync(appointmentEntity.Identifier);
            if (stored is null) return false;

            return await providerService.AppendAppointmentAsync(providerEntity.Email, stored) is not null;
        }

        return false;
    }

    private async Task AddAppointmentToCalendar(AppointmentEntity appointmentEntity)
    {
        await bookingService.BookAppointmentAsync(appointmentEntity);
    }
}
