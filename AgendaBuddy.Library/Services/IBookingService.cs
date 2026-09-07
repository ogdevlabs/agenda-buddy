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
}
