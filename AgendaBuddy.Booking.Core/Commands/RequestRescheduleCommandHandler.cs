namespace AgendaBuddy.Booking.Core.Commands;

/// <summary>
/// A customer proposing a new time. The appointment does not move; the provider owes an answer.
/// </summary>
/// <remarks>
/// The proposal is recorded through <c>RecordRescheduleProposalAsync</c>, whose filter requires the appointment to
/// still be <c>Booked</c> and to start in the future — so a second proposal cannot be recorded over an outstanding
/// one, and a session already under way cannot be proposed away.
/// </remarks>
public class RequestRescheduleCommandHandler(
    IMediator mediator,
    IProviderService providerService,
    IBookingService bookingService,
    IEventStore eventStore,
    INotificationDispatcher notificationDispatcher)
    : IRequestHandler<RequestRescheduleCommand, Result<AppointmentEntity>>
{
    public async Task<Result<AppointmentEntity>> Handle(
        RequestRescheduleCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var appointment = await bookingService.SearchAppointmentAsync(request.Identifier);
        if (appointment is null)
        {
            await FailAsync(request.Identifier);
            return Result.Fail<AppointmentEntity>("No appointment found for this identifier.");
        }

        var nowUtc = DateTime.UtcNow;

        // Validated in memory first so the caller gets the specific reason — "already awaiting an answer", "in the
        // past", "the time already booked" — rather than the single generic refusal a filter miss produces.
        try
        {
            appointment.RequestReschedule(request.ProposedStartUtc, request.RequestedByEmail, nowUtc);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            await FailAsync(request.Identifier);
            return Result.Fail<AppointmentEntity>(exception.Message);
        }

        if (!await bookingService.RecordRescheduleProposalAsync(
                request.Identifier, request.ProposedStartUtc, request.RequestedByEmail, nowUtc))
        {
            await FailAsync(request.Identifier);
            return Result.Fail<AppointmentEntity>(
                "This session could not be rescheduled — it may have been cancelled, completed, or already have "
                + "a request awaiting an answer.");
        }

        // The proposal itself, not just the status. GET /api/v1/calendar/appointments/{email} serves the
        // provider's EMBEDDED list for both roles, so a proposal written only to the appointments collection
        // reaches no screen: the status arrives as RescheduleRequested with no proposed time, which every reader
        // correctly reads as "no proposal outstanding".
        await providerService.SetEmbeddedRescheduleProposalAsync(
            appointment.EmailProvider,
            request.Identifier,
            request.ProposedStartUtc,
            request.RequestedByEmail);

        await mediator.Publish(
            new RequestRescheduleEvent { Identifier = request.Identifier }, cancellationToken);

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(RequestRescheduleCommand),
            Data = JsonSerializer.Serialize(appointment)
        });

        // The OTHER party is told, because they are the one who has to answer. Whoever proposed already knows.
        var recipient = string.Equals(
            request.RequestedByEmail, appointment.EmailCustomer, StringComparison.OrdinalIgnoreCase)
            ? appointment.EmailProvider
            : appointment.EmailCustomer;

        await NotifyAsync(
            recipient,
            "New time requested",
            RescheduleNarrative.Proposed(appointment, request.ProposedStartUtc, request.RequestedByEmail),
            NotificationType.RescheduleRequested,
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
            Type = nameof(RequestRescheduleCommand),
            Data = JsonSerializer.Serialize(new { Identifier = identifier })
        });

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
        catch (Exception) { /* the recorded proposal stands regardless */ }
    }
}
