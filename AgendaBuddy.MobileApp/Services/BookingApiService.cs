using System.Text;
using System.Text.Json;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Tools;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

public class BookingApiService : IBookingApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICalendarApiService _calendarApiService;

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    // Booking registers AgendaBuddy.Library.Tools.ObjectIdJsonConverter server-side, so NoteEntity/PaymentEntity
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
    // on AgendaBuddy.MobileApp.Routing.BookingRouteBuilder). Both reads compose with Calendar's real
    // GET /api/v1/calendar/appointments/{email} instead of building a Booking path that would 404.

    public async Task<List<AppointmentSummary>> GetTodayAppointmentsAsync(CancellationToken ct = default)
    {
        var appointments = await _calendarApiService.GetAppointmentsAsync(ct);

        // DateTime.Today, not UtcNow.Date — ScheduledAt arrives already converted to local time, so a
        // device behind UTC was matching against tomorrow's date for the last hours of every evening.
        var today = DateTime.Today;

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

    public async Task<List<AppointmentSummary>> GetPastAppointmentsAsync(CancellationToken ct = default)
    {
        var appointments = await _calendarApiService.GetAppointmentsAsync(ct);

        return appointments
            .Where(a => a.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled)
            .OrderByDescending(a => a.ScheduledAt)
            .Select(ToSummary)
            .ToList();
    }

    public async Task<List<AppointmentSummary>> GetUpcomingAppointmentsAsync(CancellationToken ct = default)
    {
        var appointments = await _calendarApiService.GetAppointmentsAsync(ct);

        // DateTime.Now, not UtcNow: CalendarApiService converts ScheduledAt to local time on the way in
        // (see its GetAppointmentsAsync), so every consumer treats it as wall-clock. Comparing it against
        // UtcNow silently hid every session inside the device's UTC offset — a booking made for later
        // today vanished from the dashboard the moment it was created.
        var now = DateTime.Now;

        // Compared against the current instant rather than the date, so a session earlier today has
        // already dropped off while one later today is still ahead.
        //
        // Cancelled AND Completed are both excluded however they are dated: a session that has been dealt
        // with is not something still to come. A provider can mark a session complete before its scheduled
        // time, which otherwise left it sitting under "Upcoming" reading "Completed".
        return appointments
            .Where(a => a.ScheduledAt >= now
                        && a.Status != AppointmentStatus.Cancelled
                        && a.Status != AppointmentStatus.Completed)
            .OrderBy(a => a.ScheduledAt)
            .Select(ToSummary)
            .ToList();
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
        ServiceDurationMinutes = detail.ServiceDurationMinutes,
        // Carried through so the provider's expanded card can show who to call. Dropping these here left
        // the Phone row permanently blank however well the directory lookup worked upstream.
        CustomerName = detail.CustomerName,
        CustomerPhone = detail.CustomerPhone,
        CustomerNotes = detail.CustomerNotes
    };

    // ── Create / cancel ───────────────────────────────────────────────────────────────────────────────

    public async Task<string?> BookAppointmentAsync(string emailProvider, string emailCustomer, DateTime start, DateTime end, string? serviceName = null, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = BookingRouteBuilder.BookAppointment();
        var body = JsonSerializer.Serialize(
            BookingRouteBuilder.BuildBookAppointmentPayload(emailProvider, emailCustomer, start, end, serviceName), JsonOptions);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(route.Path, content, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return ExtractDataField(json, "identifier");
    }

    public async Task<bool> CancelAppointmentAsync(string identifier, string emailProvider, string emailCustomer, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = BookingRouteBuilder.CancelAppointment();
        var body = JsonSerializer.Serialize(
            BookingRouteBuilder.BuildCancelAppointmentPayload(identifier, emailProvider, emailCustomer), JsonOptions);

        // DELETE with a body has no HttpClient.DeleteAsync(url, content) overload — build the request
        // message directly, matching BookingModule.cs's [FromBody] AppointmentEntity binding on DELETE.
        using var request = new HttpRequestMessage(HttpMethod.Delete, route.Path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Reads one string field out of a <c>DataResponse&lt;T&gt;</c> envelope's <c>data</c> object without
    /// deserializing the whole thing — used for the one field (<c>identifier</c>) a caller needs back from a
    /// create response.
    /// </summary>
    private static string? ExtractDataField(string json, string fieldName)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty(fieldName, out var value)
            || value.ValueKind != JsonValueKind.String)
            return null;

        return value.GetString();
    }

    /// <summary>
    /// Unwraps a <c>DataResponse&lt;T&gt;</c> envelope's <c>data</c> property before deserializing into
    /// <typeparamref name="T"/> — every Booking route wraps its response this way (ADR-049); deserializing the
    /// envelope itself straight into <typeparamref name="T"/> silently produces a default/empty instance
    /// rather than throwing, since none of the envelope's own property names match.
    /// </summary>
    private static T? UnwrapData<T>(string json, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("data", out var data))
            return default;

        return JsonSerializer.Deserialize<T>(data.GetRawText(), options);
    }

    // ── Status transition ─────────────────────────────────────────────────────────────────────────────

    public async Task<AppointmentDetail?> UpdateStatusAsync(string id, AppointmentStatus status, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = BookingRouteBuilder.UpdateAppointmentStatus(id);
        var body = JsonSerializer.Serialize(BookingRouteBuilder.BuildUpdateStatusPayload(status), JsonOptions);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(route.Path, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            // ux-review.md finding 2: a gateway-level failure (destination unreachable) carries a
            // failedService field a domain-level 4xx does not — only surface it as a distinct
            // exception when it's actually present, so an ordinary invalid-transition 400 still just
            // returns null as before.
            var failedService = await response.TryReadFailedServiceAsync(ct);
            if (failedService is not null)
                throw new GatewayServiceUnavailableException(failedService);

            return null;
        }

        // The status route returns AppointmentStatusResponse(Identifier, Status), not a full entity — the
        // richer AppointmentDetail the ViewModel already holds is refreshed by re-reading, not by binding
        // this response directly into it.
        return await GetAppointmentAsync(id, ct);
    }

    // ── Session notes ─────────────────────────────────────────────────────────────────────────────────

    public async Task<List<NoteEntity>> GetNotesAsync(string identifier, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = BookingRouteBuilder.GetNotes(identifier);
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<NoteEntity>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return UnwrapData<List<NoteEntity>>(json, EntityJsonOptions) ?? new List<NoteEntity>();
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
        return UnwrapData<NoteEntity>(json, EntityJsonOptions);
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
        return UnwrapData<NoteEntity>(json, EntityJsonOptions);
    }

    // ── Payments ───────────────────────────────────────────────────────────────────────────────────────

    public async Task<PaymentEntity?> GetPaymentAsync(string identifier, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = BookingRouteBuilder.GetPayment(identifier);
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
        {
            var failedService = await response.TryReadFailedServiceAsync(ct);
            if (failedService is not null)
                throw new GatewayServiceUnavailableException(failedService);

            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return UnwrapData<PaymentEntity>(json, EntityJsonOptions);
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
        return UnwrapData<PaymentEntity>(json, EntityJsonOptions);
    }
}
