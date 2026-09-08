namespace AgendaBuddy.Library.Services;

public interface IBookingService
{
    Task BookAppointmentAsync(AppointmentEntity appointmentEntity);
    Task<bool> UpdateAppointmentAsync(string identifier, AppointmentEntity appointmentEntity);
    /// <summary>
    /// Soft-cancels an appointment, atomically.
    /// </summary>
    /// <param name="earliestStartUtc">
    /// The caller's notice requirement: the appointment must start at or after this instant. Joined to the same
    /// filter that carries the cancellable-status rule, so the check and the write cannot be raced apart. Null
    /// for a provider, who may cancel at any notice.
    /// </param>
    Task<bool> CancelAppointmentAsync(string identifier, DateTime? earliestStartUtc = null);

    Task<AppointmentEntity> SearchAppointmentAsync(string identifier);

    /// <summary>
    /// Applies a reschedule to the stored appointment: its new start and end, its previous start, and the
    /// status/proposal fields.
    /// </summary>
    /// <remarks>
    /// A targeted <c>$set</c> rather than replacing the document, and the expected CURRENT status is in the
    /// filter — so approving a proposal cannot overwrite a cancellation that landed in between.
    /// </remarks>
    Task<bool> ApplyRescheduleAsync(string identifier, AppointmentEntity rescheduled, AppointmentStatus expectedStatus);

    /// <summary>
    /// Records a reschedule proposal without moving the appointment.
    /// </summary>
    /// <remarks>
    /// The filter requires the appointment to still be <c>Booked</c> AND to start after
    /// <paramref name="earliestStartUtc"/>, so two proposals cannot both be recorded and a session cannot be
    /// proposed away after it has begun.
    /// </remarks>
    Task<bool> RecordRescheduleProposalAsync(
        string identifier, DateTime proposedStartUtc, string proposedBy, DateTime earliestStartUtc);

    /// <summary>
    /// Clears an outstanding proposal and returns the appointment to <c>Booked</c>, leaving its times alone.
    /// </summary>
    Task<bool> ClearRescheduleProposalAsync(string identifier);

    /// <summary>
    /// The booked sessions that have finished and are still recorded as <c>Booked</c>.
    /// </summary>
    /// <remarks>
    /// Read rather than blind-written so each one can be completed in BOTH stores — the appointments collection
    /// and the provider's embedded copy, which is what <c>ReportingService</c> counts from and what
    /// <c>AvailabilityCalculator</c> reads. A single <c>UpdateMany</c> would leave the embedded copies saying
    /// <c>Booked</c> for ever.
    /// </remarks>
    /// <param name="nowUtc">The instant to treat as now.</param>
    /// <param name="limit">Ceiling on one pass, so a long-neglected backlog cannot make one tick unbounded.</param>
    Task<List<AppointmentEntity>> FindCompletableAppointmentsAsync(DateTime nowUtc, int limit);

    /// <summary>
    /// Writes a status onto the appointment document with a targeted <c>$set</c>.
    /// </summary>
    /// <remarks>
    /// On the interface for the same reason <c>IProviderService.ChangeEmbeddedAppointmentStatusAsync</c> is:
    /// auto-completion needs it, and it runs outside a request scope where only interfaces are resolved.
    /// </remarks>
    /// <returns>The updated appointment, or <c>null</c> when no appointment has that identifier.</returns>
    Task<AppointmentEntity?> ChangeStatusAsync(string identifier, AppointmentStatus status, string description);
}
