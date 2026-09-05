namespace AgendaBuddy.Booking.Core.Commands;

// Constructor takes only DI-resolvable services; the per-request identifier
// comes from the command, and the handler returns Result<AppointmentEntity> instead of a
// string-sniffed convention. Typed against IBookingService/IProviderService (both interfaces already
// cover this handler's calls -- no AppendAppointmentAsync/ChangeStatusAsync needed here), so it's
// fully Moq-mockable with zero Library changes. AgendaBuddy.Booking.Api's route discards the success Value and
// answers 204 No Content unchanged (AC10) -- a JSON body cannot ride a 204 by HTTP semantics, so
// Requirement 10's blanket "every route returns DataResponse<T>" is a disclosed, deliberate exception
// here rather than silently unmet; see verification.md.
public class CancelAppointmentCommandHandler(
    IMediator mediator,
    IProviderService providerService,
    IBookingService bookingService,
    IEventStore eventStore,
    INotificationDispatcher notificationDispatcher) : IRequestHandler<CancelAppointmentCommand, Result<AppointmentEntity>>
{
    public async Task<Result<AppointmentEntity>> Handle(CancelAppointmentCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var appointmentIdentifier = request.Identifier;
        await mediator.Publish(new CancelAppointmentEvent { Identifier = appointmentIdentifier },
            cancellationToken);
        var appointmentEntity = await bookingService.SearchAppointmentAsync(appointmentIdentifier);
        if (appointmentEntity != null)
            if (await SearchAndCancelAppointment(appointmentIdentifier))
            {
                var successEvent = new Event
                {
                    Id = ObjectId.GenerateNewId(),
                    TimeStamp = DateTime.UtcNow,
                    Status = "Success",
                    Type = "CancelAppointmentCommand",
                    Data = JsonSerializer.Serialize(appointmentEntity)
                };
                await eventStore.SaveAsync(successEvent);

                // BOTH parties, because the command does not record who cancelled — either may, and the
                // one who did not needs to know. Telling the canceller as well leaves them a record rather
                // than guessing wrong about which side to inform.
                //
                // A body each, naming the OTHER party. One shared body left both sides unable to tell which
                // of their appointments it was about; neither body claims who cancelled, because the command
                // genuinely does not carry that, and inventing it would be worse than omitting it.
                await NotifyAsync(
                    appointmentEntity.EmailCustomer,
                    BuildCancelBody(appointmentEntity, counterparty: appointmentEntity.EmailProvider),
                    appointmentIdentifier,
                    cancellationToken);
                await NotifyAsync(
                    appointmentEntity.EmailProvider,
                    BuildCancelBody(appointmentEntity, counterparty: appointmentEntity.EmailCustomer),
                    appointmentIdentifier,
                    cancellationToken);

                return Result.Ok(appointmentEntity);
            }

        var failEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "CancelAppointmentCommand",
            Data = JsonSerializer.Serialize(appointmentEntity ?? new AppointmentEntity
            {
                EmailProvider = "",
                EmailCustomer = ""
            })
        };
        await eventStore.SaveAsync(failEvent);
        return Result.Fail<AppointmentEntity>(
            $"Error when trying to cancel appointment identifier: {appointmentIdentifier}");
    }

    /// <summary>
    /// Cancels the appointment in both places it is stored: the <c>appointments</c> collection and the
    /// provider's embedded copy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <b>soft</b> delete on both sides now. This used to delete the document and remove the array element,
    /// which left no record that the slot had ever been booked and made a cancellation notification name an
    /// appointment nothing could fetch.
    /// </para>
    /// <para>
    /// The embedded side is a positional <c>$set</c> rather than the read-mutate-replace it was: replacing the
    /// whole provider document to change one appointment's status is the lost-update shape ADR D-9 removed from
    /// booking, and it would silently discard a concurrent edit to the provider's services or hours.
    /// </para>
    /// <para>
    /// The "is it cancellable" rule is no longer checked here. It lives in
    /// <c>BookingService.CancelAppointmentAsync</c>'s filter, so the check and the write are one atomic
    /// operation — a preceding read could see <c>Booked</c>, be overtaken by a completion, and then cancel work
    /// that had already been delivered. Requested and Booked are both cancellable; Completed is not.
    /// </para>
    /// </remarks>
    private async Task<bool> SearchAndCancelAppointment(string identifier)
    {
        var appointment = await bookingService.SearchAppointmentAsync(identifier);
        if (appointment is null) return false;

        if (!await bookingService.CancelAppointmentAsync(identifier)) return false;

        await providerService.ChangeEmbeddedAppointmentStatusAsync(
            appointment.EmailProvider,
            identifier,
            AppointmentStatus.Cancelled,
            EnumHelper<AppointmentStatus>.GetEnumDescription(AppointmentStatus.Cancelled));

        return true;
    }
    private static string BuildCancelBody(AppointmentEntity appointment, string counterparty)
    {
        var service = string.IsNullOrWhiteSpace(appointment.ServiceName) ? "A session" : appointment.ServiceName;
        return $"{service} with {counterparty} on {appointment.Start.ToLocalTime():dddd d MMMM} at "
             + $"{appointment.Start.ToLocalTime():h:mm tt} was cancelled.";
    }

    /// <summary>
    /// Never lets an undelivered notification undo a cancellation that already succeeded. Belt and braces over
    /// <see cref="INotificationDispatcher"/>'s own per-channel absorption — the invariant protected here is the
    /// cancellation.
    /// </summary>
    private async Task NotifyAsync(
        string recipientEmail, string body, string appointmentIdentifier, CancellationToken cancellationToken)
    {
        try
        {
            await notificationDispatcher.DispatchAsync(
                new NotificationEntity(
                    recipientEmail, "Appointment cancelled", body,
                    NotificationType.AppointmentCancelled, appointmentIdentifier),
                cancellationToken);
        }
        catch (Exception) { /* the cancellation stands regardless */ }
    }
}
