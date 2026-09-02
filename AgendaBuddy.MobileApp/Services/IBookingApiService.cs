using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

public interface IBookingApiService
{
    Task<List<AppointmentSummary>> GetTodayAppointmentsAsync(CancellationToken ct = default);

    /// <summary>Completed or cancelled sessions, most recent first — a Customer's dashboard history view.</summary>
    Task<List<AppointmentSummary>> GetPastAppointmentsAsync(CancellationToken ct = default);
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
    Task<bool> CancelAppointmentAsync(string identifier, string emailProvider, string emailCustomer, CancellationToken ct = default);

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
    Task<PaymentEntity?> CreatePaymentAsync(string identifier, decimal amount, string? currency, CancellationToken ct = default);
}
