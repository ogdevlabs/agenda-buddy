using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

public interface IBookingApiService
{
    Task<List<AppointmentSummary>> GetTodayAppointmentsAsync(CancellationToken ct = default);
    Task<AppointmentDetail?> GetAppointmentAsync(string id, CancellationToken ct = default);

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
