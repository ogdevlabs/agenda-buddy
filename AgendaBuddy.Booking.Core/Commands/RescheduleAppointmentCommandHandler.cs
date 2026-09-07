namespace AgendaBuddy.Booking.Core.Commands;

/// <summary>
/// A provider moving a booked session, and telling the customer that is what happened.
/// </summary>
/// <remarks>
/// <para>
/// A provider could already change an appointment's times through <c>PUT /appointments/</c>. What that path
/// could not do is say a reschedule had occurred: no notification named it as one, and the previous time was
/// overwritten, so afterwards the session read as though it had always been at the new time.
/// </para>
/// <para>
/// The move runs through <see cref="AppointmentEntity.ApproveReschedule"/> so the session's LENGTH is carried
/// across rather than recomputed — moving a 90-minute session must not quietly make it 60 — and the write goes
/// through <c>ApplyRescheduleAsync</c>, whose filter carries the status the handler read. That is what stops the
/// move landing on an appointment cancelled in between.
/// </para>
/// </remarks>
public class RescheduleAppointmentCommandHandler(
    IMediator mediator,
    IProviderService providerService,
    IBookingService bookingService,
    IEventStore eventStore,
    INotificationDispatcher notificationDispatcher)
    : IRequestHandler<RescheduleAppointmentCommand, Result<AppointmentEntity>>
{
    public async Task<Result<AppointmentEntity>> Handle(
        RescheduleAppointmentCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var appointment = await bookingService.SearchAppointmentAsync(request.Identifier);
        if (appointment is null)
        {
            await FailAsync(request.Identifier);
            return Result.Fail<AppointmentEntity>("No appointment found for this identifier.");
        }

        // Only the provider reschedules outright. A customer reaching here would be moving a session on somebody
        // else's calendar without asking, which is what RequestRescheduleCommand exists for.
        if (!string.Equals(request.RequestedByEmail, appointment.EmailProvider, StringComparison.OrdinalIgnoreCase))
        {
            await FailAsync(request.Identifier);
            return Result.Fail<AppointmentEntity>(
                "Only the provider can reschedule a session directly. A customer requests a reschedule.");
        }

        var nowUtc = DateTime.UtcNow;
        var previousStart = appointment.Start.ToUniversalTime();

        // Routed through the entity so the proposal rules and the length-preserving arithmetic are the same ones
        // an approval uses, rather than a second implementation that can disagree with them.
        try
        {
            appointment.RequestReschedule(request.NewStartUtc, appointment.EmailProvider, nowUtc);
            appointment.ApproveReschedule();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            await FailAsync(request.Identifier);
            return Result.Fail<AppointmentEntity>(exception.Message);
        }

        // Booked is what the appointment was BEFORE the in-memory transitions above, and is what must still be
        // stored for this write to be safe to apply.
        if (!await bookingService.ApplyRescheduleAsync(request.Identifier, appointment, AppointmentStatus.Booked))
        {
            await FailAsync(request.Identifier);
            return Result.Fail<AppointmentEntity>(
                "This session could not be rescheduled — it may have been cancelled or completed.");
        }

        await providerService.ChangeEmbeddedAppointmentScheduleAsync(
            appointment.EmailProvider, request.Identifier, appointment.Start, appointment.End);

        await mediator.Publish(
            new RescheduleAppointmentEvent { Identifier = request.Identifier }, cancellationToken);

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(RescheduleAppointmentCommand),
            Data = JsonSerializer.Serialize(appointment)
        });

        // The CUSTOMER is told, because they had no say in it. Both times are named: "moved" with only the new
        // time leaves the reader unsure whether they misremembered the old one.
        await NotifyAsync(
            appointment.EmailCustomer,
            "Session rescheduled",
            RescheduleNarrative.Moved(appointment, previousStart, appointment.EmailProvider),
            NotificationType.AppointmentRescheduled,
            request.Identifier,
            cancellationToken);

        return Result.Ok(appointment);
    }

    private async Task FailAsync(string identifier) =>
        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = nameof(RescheduleAppointmentCommand),
            Data = JsonSerializer.Serialize(new { Identifier = identifier })
        });

    /// <summary>
    /// Belt and braces over <see cref="INotificationDispatcher"/>'s own absorption: the invariant protected here
    /// is the reschedule, and it is not the dispatcher's to keep.
    /// </summary>
    private async Task NotifyAsync(
        string recipientEmail,
        string subject,
        string body,
        NotificationType type,
        string appointmentIdentifier,
        CancellationToken cancellationToken)
    {
        try
        {
            await notificationDispatcher.DispatchAsync(
                new NotificationEntity(recipientEmail, subject, body, type, appointmentIdentifier),
                cancellationToken);
        }
        catch (Exception) { /* the reschedule stands regardless */ }
    }
}
