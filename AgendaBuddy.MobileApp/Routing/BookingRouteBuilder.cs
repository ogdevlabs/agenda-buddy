using AgendaBuddy.Library.Entities;

namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Route-building logic extracted from <see cref="Services.BookingApiService"/>, corrected to
/// the real backend contract (api-contracts.md §2).
/// </summary>
/// <remarks>
/// <b>Known deviation from api-contracts.md §2:</b> the design doc lists
/// <c>GET api/v1/booking/appointments?from=&amp;to=</c> and <c>GET api/v1/booking/appointments/{identifier}</c>
/// as Booking routes. <c>Booking/Program.cs</c> has no such routes — Booking exposes only
/// POST/PUT/DELETE on <c>/appointments</c>, plus the status/notes/payment families. There is no
/// GET anywhere on Booking, and there never has been (see <c>01-api-surface.md</c>, "What is missing").
/// Reads live on Calendar instead (<see cref="CalendarRouteBuilder.Appointments"/>) — <see
/// cref="Services.BookingApiService"/> now composes with <see cref="Services.ICalendarApiService"/> for its
/// two read methods rather than building a Booking path that would 404. This keeps AC2's promise ("resolves
/// … not a 404") true; following api-contracts.md's literal text here would not.
/// </remarks>
public static class BookingRouteBuilder
{
    /// <summary>
    /// The dedicated status-transition route. Replaces the legacy
    /// <c>PUT booking/{id}</c> call, which is now ignored entirely.
    /// </summary>
    public static RouteSpec UpdateAppointmentStatus(string identifier) =>
        new(HttpMethod.Post, $"api/v1/booking/appointments/{identifier}/status");

    /// <summary>Payload shape Booking's <c>AppointmentStatusRequest(string Status)</c> binds: <c>{"status": "…"}</c>.</summary>
    public static object BuildUpdateStatusPayload(AppointmentStatus status) =>
        new { status = status.ToString() };

    // ── Session notes (api-contracts.md §2) ──────────────────────────────────────────────────────────

    public static RouteSpec GetNotes(string identifier) =>
        new(HttpMethod.Get, $"api/v1/booking/appointments/{identifier}/notes");

    public static RouteSpec CreateNote(string identifier) =>
        new(HttpMethod.Post, $"api/v1/booking/appointments/{identifier}/notes");

    /// <summary>Payload shape Booking's <c>NoteRequest(string Content)</c> binds.</summary>
    public static object BuildNotePayload(string content) => new { content };

    /// <summary>
    /// Booking updates a note by the note's own id, not the appointment identifier
    /// (<c>PUT /api/v1/booking/notes/{id}</c>) — api-contracts.md §2 simplifies this to "PUT …/notes"; the
    /// real route keys on the note id returned by <see cref="CreateNote"/>'s <c>201</c>.
    /// </summary>
    public static RouteSpec UpdateNote(string noteId) =>
        new(HttpMethod.Put, $"api/v1/booking/notes/{noteId}");

    // ── Payments (api-contracts.md §2) ───────────────────────────────────────────────────────────────

    public static RouteSpec GetPayment(string identifier) =>
        new(HttpMethod.Get, $"api/v1/booking/appointments/{identifier}/payment");

    public static RouteSpec CreatePayment(string identifier) =>
        new(HttpMethod.Post, $"api/v1/booking/appointments/{identifier}/payment");

    /// <summary>Payload shape Booking's <c>PaymentRequest(decimal Amount, string? Currency)</c> binds.</summary>
    public static object BuildPaymentPayload(decimal amount, string? currency) =>
        new { amount, currency };
}
