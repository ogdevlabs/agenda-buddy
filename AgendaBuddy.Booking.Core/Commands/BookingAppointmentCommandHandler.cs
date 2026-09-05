namespace AgendaBuddy.Booking.Core.Commands;

// Constructor takes only DI-resolvable services -- the per-request AppointmentEntity that
// used to be a constructor parameter (so RequestCollection.cs could hand-construct this handler) now
// comes from the command itself, which is what makes a real mediator.Send(command, ct) dispatch
// possible: MediatR resolves this handler from the container, which has no way to supply a per-request
// value through the constructor. Returns FluentResults.Result<AppointmentEntity> instead of a
// string-sniffed "exception"-prefixed convention (PRD Requirement 5).
//
// Stays on the concrete ProviderService/BookingService, not IProviderService/IBookingService (unlike
// Update/Cancel's handlers, Party Review): this one calls AppendAppointmentAsync, which isn't on
// IProviderService, and adding it would be a Library change out of this feature's scope.
public class BookingAppointmentCommandHandler(
    IMediator mediator,
    ProviderService providerService,
    BookingService bookingService,
    IEventStore eventStore,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notificationDispatcher)
    : IRequestHandler<BookAppointmentCommand, Result<AppointmentEntity>>
{
    public async Task<Result<AppointmentEntity>> Handle(BookAppointmentCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var appointmentEntity = request.AppointmentEntity;

        if (appointmentEntity.Start <= dateTimeProvider.UtcNow)
            return Result.Fail<AppointmentEntity>("Appointments must be booked for a future time.");

        var overlapping = await bookingService.FindOverlappingAppointmentsAsync(
            appointmentEntity.EmailProvider, appointmentEntity.Start, appointmentEntity.End);
        if (overlapping.Any())
            return Result.Fail<AppointmentEntity>(
                $"This time overlaps with an existing appointment for {appointmentEntity.EmailProvider}.");

        // A named service has to be one this provider actually offers. Checked BEFORE any write, unlike
        // the provider lookup further down, which happens after the appointment has already been
        // persisted to its own collection -- validating there would leave an orphan behind.
        //
        // Deliberately not REQUIRED: appointments predate services being selectable and the provider-side
        // booking path has never sent one, so demanding it would break existing callers. The client
        // enforces choosing one; this stops an unmatched or invented name being stored.
        if (!string.IsNullOrWhiteSpace(appointmentEntity.ServiceName))
        {
            var provider = await providerService.FindProvidersAsync(
                SupportTools<ProviderEntity>.FilterByEmail(appointmentEntity.EmailProvider));

            var service = provider?.ServiceEntities?.FirstOrDefault(s =>
                string.Equals(s.Name, appointmentEntity.ServiceName, StringComparison.OrdinalIgnoreCase));

            if (service is null)
                return Result.Fail<AppointmentEntity>(
                    $"{appointmentEntity.EmailProvider} does not offer a service named '{appointmentEntity.ServiceName}'.");

            // Snapshot the length as booked, so editing the service later cannot rewrite what was agreed.
            appointmentEntity.ServiceDurationMinutes ??= service.DurationMinutes;
        }

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

            // Tell the provider someone is waiting on them, on every channel — a request that only lands in
            // the in-app inbox is a request that sits until the provider happens to open it. Non-fatal: the
            // appointment is already persisted, and failing the booking because a notification could not be
            // delivered would be the wrong trade.
            await NotifyAsync(new NotificationEntity(
                recipientEmail: appointmentEntity.EmailProvider,
                subject: "New appointment request",
                body: BuildRequestBody(appointmentEntity),
                type: NotificationType.AppointmentRequested,
                appointmentIdentifier: appointmentEntity.Identifier), cancellationToken);

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

    /// <summary>
    /// Delivers a notification without letting its failure fail the operation that caused it. The appointment
    /// is the thing that had to succeed; an undelivered notification is a degraded experience, not a lost
    /// booking.
    /// </summary>
    /// <remarks>
    /// Belt and braces: <see cref="INotificationDispatcher"/> already absorbs a per-channel failure, but that
    /// is a promise made by an implementation this handler does not own, and the invariant being protected
    /// here is the appointment.
    /// </remarks>
    private async Task NotifyAsync(NotificationEntity notification, CancellationToken cancellationToken)
    {
        try { await notificationDispatcher.DispatchAsync(notification, cancellationToken); }
        catch (Exception) { /* the appointment stands regardless */ }
    }

    private static string BuildRequestBody(AppointmentEntity appointment)
    {
        var service = string.IsNullOrWhiteSpace(appointment.ServiceName) ? "a session" : appointment.ServiceName;
        return $"{appointment.EmailCustomer} requested {service} on "
             + $"{appointment.Start.ToLocalTime():dddd d MMMM} at {appointment.Start.ToLocalTime():h:mm tt}.";
    }

    /// <remarks>
    /// ADR D-9. This used to read the provider, append to its embedded appointment
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
