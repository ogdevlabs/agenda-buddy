using Library.Entities;
using MobileApp.Models;

namespace MobileApp.Services;

public interface IBookingApiService
{
    Task<List<AppointmentSummary>> GetTodayAppointmentsAsync(CancellationToken ct = default);
    Task<AppointmentDetail?> GetAppointmentAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// F-014's dedicated <c>POST .../status</c> route (F-015-T07, AC7) — replaces the legacy
    /// <c>PUT booking/{id}</c> call, which F-014 now ignores entirely.
    /// </summary>
    Task<AppointmentDetail?> UpdateStatusAsync(string id, AppointmentStatus status, CancellationToken ct = default);

    // ── F-014 session notes — new to the client (F-015-T07) ──────────────────────────────────────────

    Task<List<NoteEntity>> GetNotesAsync(string identifier, CancellationToken ct = default);
    Task<NoteEntity?> CreateNoteAsync(string identifier, string content, CancellationToken ct = default);
    Task<NoteEntity?> UpdateNoteAsync(string noteId, string content, CancellationToken ct = default);

    // ── F-014 payments — new to the client (F-015-T07) ────────────────────────────────────────────────

    Task<PaymentEntity?> GetPaymentAsync(string identifier, CancellationToken ct = default);
    Task<PaymentEntity?> CreatePaymentAsync(string identifier, decimal amount, string? currency, CancellationToken ct = default);
}
