using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

public interface IBookingApiService
{
    Task<List<AppointmentSummary>> GetTodayAppointmentsAsync(CancellationToken ct = default);

    /// <summary>Completed or cancelled sessions, most recent first — a Customer's dashboard history view.</summary>
    Task<List<AppointmentSummary>> GetPastAppointmentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Sessions still ahead and not cancelled, soonest first. What a Customer's dashboard leads with — the
    /// question they open the app to answer is "when is my next session", which a completed/cancelled
    /// history can never answer.
    /// </summary>
    Task<List<AppointmentSummary>> GetUpcomingAppointmentsAsync(CancellationToken ct = default);
    Task<AppointmentDetail?> GetAppointmentAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// <c>POST /api/v1/booking/appointments</c>. Either participant may book on their own behalf
    /// (<c>OwnershipGuard.AssertOwnerAny</c>) — <paramref name="emailProvider"/>/<paramref name="emailCustomer"/>
    /// are whichever the caller is booking with, not necessarily their own email. Returns the identifier the
    /// server generated, or <c>null</c> on failure.
    /// </summary>
    Task<string?> BookAppointmentAsync(string emailProvider, string emailCustomer, DateTime start, DateTime end, string? serviceName = null, CancellationToken ct = default);

    /// <summary>
    /// The real cancellation route — <c>DELETE /api/v1/booking/appointments/</c>, body-identified. Distinct
    /// from <see cref="UpdateStatusAsync"/>, which cannot reach <c>Cancelled</c> at all.
    /// </summary>
    /// <remarks>
    /// Returns a result rather than a bool because a customer cancelling inside the 24-hour notice period is a
    /// refusal the server narrates, and wording it as a connection problem sends them to fix a working network.
    /// </remarks>
    Task<AppointmentActionResult> CancelAppointmentAsync(
        string identifier, string emailProvider, string emailCustomer, CancellationToken ct = default);

    /// <summary>
    /// <c>POST .../reschedule</c> — the PROVIDER moving a booked session outright.
    /// </summary>
    /// <remarks>
    /// Provider-only: a customer calling this earns a 403, so the client must not offer the action to them. The
    /// instant must be the server's own UTC value from the availability response, unchanged.
    /// </remarks>
    Task<AppointmentActionResult> RescheduleAsync(
        string identifier, DateTime newStartUtc, CancellationToken ct = default);

    /// <summary>
    /// <c>POST .../reschedule-request</c> — proposing a new time without moving the appointment.
    /// </summary>
    Task<AppointmentActionResult> RequestRescheduleAsync(
        string identifier, DateTime proposedStartUtc, CancellationToken ct = default);

    /// <summary>
    /// <c>POST .../reschedule-answer</c> — approving or declining an outstanding proposal.
    /// </summary>
    /// <remarks>
    /// The server refuses an answer from whoever made the proposal, so the client shows these actions only to
    /// the party who did not.
    /// </remarks>
    Task<AppointmentActionResult> AnswerRescheduleAsync(
        string identifier, bool approve, CancellationToken ct = default);

    /// <summary>
    /// The dedicated <c>POST .../status</c> route — the backend ignores the status field on
    /// <c>PUT booking/{id}</c> entirely.
    /// </summary>
    Task<AppointmentDetail?> UpdateStatusAsync(string id, AppointmentStatus status, CancellationToken ct = default);

    // ── Session notes ─────────────────────────────────────────────────────────────────────────────────

    Task<List<NoteEntity>> GetNotesAsync(string identifier, CancellationToken ct = default);
    Task<NoteEntity?> CreateNoteAsync(string identifier, string content, CancellationToken ct = default);
    Task<NoteEntity?> UpdateNoteAsync(string noteId, string content, CancellationToken ct = default);

    // ── Payments ───────────────────────────────────────────────────────────────────────────────────────

    Task<PaymentEntity?> GetPaymentAsync(string identifier, CancellationToken ct = default);
}
