using System.Text;
using System.Text.Json;
using Library.Entities;
using Library.Tools;
using MobileApp.Models;
using MobileApp.Routing;

namespace MobileApp.Services;

public class BookingApiService : IBookingApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICalendarApiService _calendarApiService;

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    // Booking registers Library.Tools.ObjectIdJsonConverter server-side (F-014), so NoteEntity/PaymentEntity
    // ids always arrive as plain hex strings here — safe to bind straight into ObjectId, unlike Calendar's
    // AppointmentEntity (see CalendarApiService.ParseAppointments).
    private static readonly JsonSerializerOptions EntityJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new ObjectIdJsonConverter() }
    };

    public BookingApiService(IHttpClientFactory httpClientFactory, ICalendarApiService calendarApiService)
    {
        _httpClientFactory = httpClientFactory;
        _calendarApiService = calendarApiService;
    }

    // ── Reads ──────────────────────────────────────────────────────────────────────────────────────────
    //
    // Booking has no GET route for a single appointment or a list, and never has (see the deviation note
    // on MobileApp.Routing.BookingRouteBuilder). Both reads compose with Calendar's real
    // GET /api/v1/calendar/appointments/{email} instead of building a Booking path that would 404.

    public async Task<List<AppointmentSummary>> GetTodayAppointmentsAsync(CancellationToken ct = default)
    {
        var appointments = await _calendarApiService.GetAppointmentsAsync(ct);
        var today = DateTime.UtcNow.Date;

        return appointments
            .Where(a => a.ScheduledAt.Date == today)
            .Select(ToSummary)
            .ToList();
    }

    public async Task<AppointmentDetail?> GetAppointmentAsync(string id, CancellationToken ct = default)
    {
        var appointments = await _calendarApiService.GetAppointmentsAsync(ct);
        return appointments.FirstOrDefault(a => a.Id == id);
    }

    private static AppointmentSummary ToSummary(AppointmentDetail detail) => new()
    {
        Id = detail.Id,
        CustomerEmail = detail.CustomerEmail,
        ProviderEmail = detail.ProviderEmail,
        DisplayName = detail.DisplayName,
        ScheduledAt = detail.ScheduledAt,
        Status = detail.Status,
        ServiceId = detail.ServiceId,
        ServiceName = detail.ServiceName,
        CustomerNotes = detail.CustomerNotes
    };

    // ── Status transition (F-014's dedicated route; F-015-T07 AC7) ───────────────────────────────────

    public async Task<AppointmentDetail?> UpdateStatusAsync(string id, AppointmentStatus status, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = BookingRouteBuilder.UpdateAppointmentStatus(id);
        var body = JsonSerializer.Serialize(BookingRouteBuilder.BuildUpdateStatusPayload(status), JsonOptions);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(route.Path, content, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        // The status route returns AppointmentStatusResponse(Identifier, Status), not a full entity — the
        // richer AppointmentDetail the ViewModel already holds is refreshed by re-reading, not by binding
        // this response directly into it.
        return await GetAppointmentAsync(id, ct);
    }

    // ── F-014 session notes (new to the client — F-015-T07) ──────────────────────────────────────────

    public async Task<List<NoteEntity>> GetNotesAsync(string identifier, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = BookingRouteBuilder.GetNotes(identifier);
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<NoteEntity>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<NoteEntity>>(json, EntityJsonOptions) ?? new List<NoteEntity>();
    }

    public async Task<NoteEntity?> CreateNoteAsync(string identifier, string content, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = BookingRouteBuilder.CreateNote(identifier);
        var body = JsonSerializer.Serialize(BookingRouteBuilder.BuildNotePayload(content), JsonOptions);
        var response = await client.PostAsync(route.Path, new StringContent(body, Encoding.UTF8, "application/json"), ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<NoteEntity>(json, EntityJsonOptions);
    }

    public async Task<NoteEntity?> UpdateNoteAsync(string noteId, string content, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = BookingRouteBuilder.UpdateNote(noteId);
        var body = JsonSerializer.Serialize(BookingRouteBuilder.BuildNotePayload(content), JsonOptions);
        var response = await client.PutAsync(route.Path, new StringContent(body, Encoding.UTF8, "application/json"), ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<NoteEntity>(json, EntityJsonOptions);
    }

    // ── Payments (new to the client — F-015-T07) ─────────────────────────────────────────────────────

    public async Task<PaymentEntity?> GetPaymentAsync(string identifier, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = BookingRouteBuilder.GetPayment(identifier);
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<PaymentEntity>(json, EntityJsonOptions);
    }

    public async Task<PaymentEntity?> CreatePaymentAsync(string identifier, decimal amount, string? currency, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = BookingRouteBuilder.CreatePayment(identifier);
        var body = JsonSerializer.Serialize(BookingRouteBuilder.BuildPaymentPayload(amount, currency), JsonOptions);
        var response = await client.PostAsync(route.Path, new StringContent(body, Encoding.UTF8, "application/json"), ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<PaymentEntity>(json, EntityJsonOptions);
    }
}
