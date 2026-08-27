namespace AgendaBuddy.Booking.Core.Commands;

/// <summary>
/// Applies an appointment status transition, in both places the status is stored, and audits the outcome.
/// </summary>
/// <remarks>
/// <para>
/// F-014 requirement 14 / threat T-203. <b>The transition is applied by the entity</b>
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
/// F-019-T05: authored fresh in AgendaBuddy.Booking.Core, returning <c>Result&lt;AppointmentEntity&gt;</c> rather than
/// the string convention its AgendaBuddy.EventAndCommands predecessor used — the predecessor stays in place until T06
/// rewires AgendaBuddy.Booking.Api's status route onto this one and T10 deletes it.
/// </para>
/// </remarks>
public class ChangeAppointmentStatusCommandHandler(
    ProviderService providerService,
    BookingService bookingService,
    IEventStore eventStore) : IRequestHandler<ChangeAppointmentStatusCommand, Result<AppointmentEntity>>
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

        return Result.Ok(updated);
    }

    private async Task Audit(string status, ChangeAppointmentStatusCommand request, string detail) =>
        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = status,
            Type = nameof(ChangeAppointmentStatusCommand),
            // Identifier and target only. The appointment carries two email addresses, and F-016-T18
            // established that audit payloads do not serialise entity data (QueryAudit / ADR-027).
            Data = JsonSerializer.Serialize(new { request.Identifier, request.TargetStatus, detail })
        });
}
