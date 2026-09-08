namespace AgendaBuddy.Booking.Core.Commands;

/// <summary>
/// Answering an outstanding proposal: approve and the session moves, decline and it stands.
/// </summary>
/// <remarks>
/// <para>
/// <b>The answer must come from the other party.</b> Approving one's own proposal would make the whole
/// ask-and-agree shape decorative — a customer could move a provider's session by proposing it and then
/// accepting.
/// </para>
/// <para>
/// <b>Approving re-checks nothing about availability here, and that is not an omission.</b> The write goes
/// through <c>ApplyRescheduleAsync</c> with the expected current status in its filter, so it cannot land on an
/// appointment cancelled in between; whether the proposed slot is still FREE is the availability question, and it
/// is answered where every other booking answers it — an overlapping appointment would have to have been booked
/// into a slot the provider's own availability response no longer offered. The client withholds the approve
/// action when the slot has gone, so the common case never reaches here.
/// </para>
/// </remarks>
public class AnswerRescheduleCommandHandler(
    IMediator mediator,
    IProviderService providerService,
    IBookingService bookingService,
    IEventStore eventStore,
    INotificationDispatcher notificationDispatcher)
    : IRequestHandler<AnswerRescheduleCommand, Result<AppointmentEntity>>
{
    public async Task<Result<AppointmentEntity>> Handle(
        AnswerRescheduleCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var appointment = await bookingService.SearchAppointmentAsync(request.Identifier);
        if (appointment is null)
        {
            await FailAsync(request.Identifier);
            return Result.Fail<AppointmentEntity>("No appointment found for this identifier.");
        }

        if (!appointment.HasPendingReschedule)
        {
            await FailAsync(request.Identifier);
            return Result.Fail<AppointmentEntity>("There is no reschedule request to answer.");
        }

        if (string.Equals(request.AnsweredByEmail, appointment.ProposedBy, StringComparison.OrdinalIgnoreCase))
        {
            await FailAsync(request.Identifier);
            return Result.Fail<AppointmentEntity>(
                "The other party has to answer a reschedule request, not the one who made it.");
        }

        var proposedStart = appointment.ProposedStart!.Value;
        var previousStart = appointment.Start.ToUniversalTime();
        var proposer = appointment.ProposedBy!;

        return request.Approve
            ? await ApproveAsync(request, appointment, previousStart, proposer, cancellationToken)
            : await DeclineAsync(request, appointment, proposer, cancellationToken);
    }

    private async Task<Result<AppointmentEntity>> ApproveAsync(
        AnswerRescheduleCommand request,
        AppointmentEntity appointment,
        DateTime previousStart,
        string proposer,
        CancellationToken cancellationToken)
    {
        try
        {
            appointment.ApproveReschedule();
        }
        catch (InvalidOperationException exception)
        {
            await FailAsync(request.Identifier);
            return Result.Fail<AppointmentEntity>(exception.Message);
        }

        if (!await bookingService.ApplyRescheduleAsync(
                request.Identifier, appointment, AppointmentStatus.RescheduleRequested))
        {
            await FailAsync(request.Identifier);
            return Result.Fail<AppointmentEntity>(
                "This request could not be approved — the session may have been cancelled since it was made.");
        }

        await providerService.ChangeEmbeddedAppointmentScheduleAsync(
            appointment.EmailProvider, request.Identifier, appointment.Start, appointment.End, previousStart);

        await mediator.Publish(
            new AnswerRescheduleEvent { Identifier = request.Identifier }, cancellationToken);
        await SucceedAsync(request.Identifier, appointment);

        // The PROPOSER is told: they are the one waiting on the answer.
        await NotifyAsync(
            proposer,
            "New time approved",
            RescheduleNarrative.Approved(appointment, previousStart, request.AnsweredByEmail),
            NotificationType.RescheduleApproved,
            request.Identifier,
            cancellationToken);

        return Result.Ok(appointment);
    }

    private async Task<Result<AppointmentEntity>> DeclineAsync(
        AnswerRescheduleCommand request,
        AppointmentEntity appointment,
        string proposer,
        CancellationToken cancellationToken)
    {
        try
        {
            appointment.DeclineReschedule();
        }
        catch (InvalidOperationException exception)
        {
            await FailAsync(request.Identifier);
            return Result.Fail<AppointmentEntity>(exception.Message);
        }

        if (!await bookingService.ClearRescheduleProposalAsync(request.Identifier))
        {
            await FailAsync(request.Identifier);
            return Result.Fail<AppointmentEntity>(
                "This request could not be declined — the session may have been cancelled since it was made.");
        }

        // Unsets the proposal as well as restoring the status. Setting the status back alone would leave a
        // Booked appointment still carrying a proposed time, which reads as an outstanding request against a
        // status saying there is none.
        await providerService.ClearEmbeddedRescheduleProposalAsync(
            appointment.EmailProvider, request.Identifier);

        await mediator.Publish(
            new AnswerRescheduleEvent { Identifier = request.Identifier }, cancellationToken);
        await SucceedAsync(request.Identifier, appointment);

        // Declining is news too. Silence here reads as an unanswered request, and the proposer would keep waiting
        // on a session that is staying exactly where it was.
        await NotifyAsync(
            proposer,
            "New time declined",
            RescheduleNarrative.Declined(appointment, request.AnsweredByEmail),
            NotificationType.RescheduleDeclined,
            request.Identifier,
            cancellationToken);

        return Result.Ok(appointment);
    }

    private async Task SucceedAsync(string identifier, AppointmentEntity appointment) =>
        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(AnswerRescheduleCommand),
            Data = JsonSerializer.Serialize(appointment)
        });

    private async Task FailAsync(string identifier) =>
        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = nameof(AnswerRescheduleCommand),
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
        catch (Exception) { /* the answer stands regardless */ }
    }
}
