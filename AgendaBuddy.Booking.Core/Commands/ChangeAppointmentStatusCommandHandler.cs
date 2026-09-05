namespace AgendaBuddy.Booking.Core.Commands;

/// <summary>
/// Applies an appointment status transition, in both places the status is stored, and audits the outcome.
/// </summary>
/// <remarks>
/// <para>
/// <b>The transition is applied by the entity</b>
/// (<see cref="AppointmentEntity.TransitionTo"/>), so <c>Book()</c> and <c>Complete()</c> — which held the
/// rules and were never called by anything outside a test — become the only path to a status change. An
/// illegal transition throws <see cref="InvalidOperationException"/>, which AgendaBuddy.Booking.Api maps to <b>409</b>
/// with nothing written.
/// </para>
/// <para>
/// <b>Both copies are written, and that is not redundancy.</b> An appointment exists in the `appointments`
/// collection *and* embedded in the provider document, and <c>ReportingService</c> counts statuses from the
/// <b>embedded</b> list. Writing one and not the other would leave the provider's dashboard reporting the
/// old status indefinitely. Both writes are targeted <c>$set</c>s (ADR-032) rather than document
/// replacements — the embedded one uses the positional operator, so it cannot disturb a sibling appointment.
/// </para>
/// <para>
/// ⚠️ <b>The two writes are not atomic together.</b> They are separate documents in separate collections, and
/// this project has no multi-document transaction (it would need a replica set). A fault between them leaves
/// the collection updated and the embedded copy stale. That is a **known, bounded** inconsistency —
/// re-issuing the same transition repairs it, because the second attempt fails the entity's guard only if the
/// *collection* copy already moved, which is the copy the guard reads. Recorded rather than hidden; the
/// alternative is a transaction this deployment cannot provide.
/// </para>
/// <para>
/// Authored fresh in AgendaBuddy.Booking.Core, returning <c>Result&lt;AppointmentEntity&gt;</c> rather than
/// the string convention its AgendaBuddy.EventAndCommands predecessor used — the predecessor stays in place until
/// AgendaBuddy.Booking.Api's status route is rewired onto this one and the predecessor is deleted.
/// </para>
/// </remarks>
public class ChangeAppointmentStatusCommandHandler(
    ProviderService providerService,
    BookingService bookingService,
    IEventStore eventStore,
    INotificationDispatcher notificationDispatcher) : IRequestHandler<ChangeAppointmentStatusCommand, Result<AppointmentEntity>>
{
    public async Task<Result<AppointmentEntity>> Handle(ChangeAppointmentStatusCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var appointment = await bookingService.SearchAppointmentAsync(request.Identifier);
        if (appointment is null)
        {
            await Audit("Failed", request, "no such appointment");
            return Result.Fail<AppointmentEntity>($"No appointment found with identifier {request.Identifier}");
        }

        // Throws InvalidOperationException on an illegal transition — deliberately NOT caught here.
        // AgendaBuddy.Booking.Api maps it to 409, and catching it to return Result.Fail would lose the distinction
        // between "illegal transition" (409) and "no such appointment" (this method's own Fail, 404).
        appointment.TransitionTo(request.TargetStatus);

        var updated = await bookingService.ChangeStatusAsync(
            request.Identifier, appointment.AppointmentStatus, appointment.AppointmentDescription);

        if (updated is null)
        {
            await Audit("Failed", request, "appointment vanished between read and write");
            return Result.Fail<AppointmentEntity>($"No appointment found with identifier {request.Identifier}");
        }

        await providerService.ChangeEmbeddedAppointmentStatusAsync(
            appointment.EmailProvider,
            request.Identifier,
            appointment.AppointmentStatus,
            appointment.AppointmentDescription);

        await Audit("Success", request, appointment.AppointmentStatus.ToString());

        // The CUSTOMER is told, because only the provider can move an appointment out of Requested — so
        // every transition that reaches here is news to the other party. Non-fatal: the status has already
        // been written, and losing the notification must not undo it.
        await NotifyAsync(new NotificationEntity(
            recipientEmail: appointment.EmailCustomer,
            subject: appointment.AppointmentStatus switch
            {
                AppointmentStatus.Booked => "Appointment confirmed",
                AppointmentStatus.Completed => "Session completed",
                _ => "Appointment updated"
            },
            body: BuildStatusBody(appointment),
            type: appointment.AppointmentStatus switch
            {
                AppointmentStatus.Completed => NotificationType.AppointmentCompleted,
                _ => NotificationType.AppointmentUpdated
            },
            appointmentIdentifier: request.Identifier), cancellationToken);

        return Result.Ok(updated);
    }

    private static string BuildStatusBody(AppointmentEntity appointment)
    {
        var service = string.IsNullOrWhiteSpace(appointment.ServiceName) ? "Your session" : appointment.ServiceName;
        var when = $"{appointment.Start.ToLocalTime():dddd d MMMM} at {appointment.Start.ToLocalTime():h:mm tt}";
        return appointment.AppointmentStatus switch
        {
            AppointmentStatus.Booked => $"{appointment.EmailProvider} confirmed {service} on {when}.",
            AppointmentStatus.Completed => $"{service} on {when} was marked complete.",
            _ => $"{service} on {when} is now {appointment.AppointmentStatus}."
        };
    }

    /// <summary>
    /// Never lets an undelivered notification undo a status change that already succeeded. Belt and braces
    /// over <see cref="INotificationDispatcher"/>'s own per-channel absorption — the invariant protected here
    /// is the transition.
    /// </summary>
    private async Task NotifyAsync(NotificationEntity notification, CancellationToken cancellationToken)
    {
        try { await notificationDispatcher.DispatchAsync(notification, cancellationToken); }
        catch (Exception) { /* the transition stands regardless */ }
    }

    private async Task Audit(string status, ChangeAppointmentStatusCommand request, string detail) =>
        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = status,
            Type = nameof(ChangeAppointmentStatusCommand),
            // Identifier and target only. The appointment carries two email addresses, and
            // audit payloads do not serialise entity data (QueryAudit / ADR-027).
            Data = JsonSerializer.Serialize(new { request.Identifier, request.TargetStatus, detail })
        });
}
