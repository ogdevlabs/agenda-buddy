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
        CustomerNotes = detail.CustomerNotes,

        // Same reason as the phone above: dropped here, the dashboard card would fall back to a deterministic
        // avatar and disagree with Contacts and Messages for anybody the server assigned a mark to.
        ContactEmail = detail.ContactEmail,
        ContactAvatarId = detail.ContactAvatarId
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

    public async Task<AppointmentActionResult> CancelAppointmentAsync(
        string identifier, string emailProvider, string emailCustomer, CancellationToken ct = default)
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

        return await SendAppointmentActionAsync(client, request, ct);
    }

    public Task<AppointmentActionResult> RescheduleAsync(
        string identifier, DateTime newStartUtc, CancellationToken ct = default) =>
        PostAppointmentActionAsync(
            BookingRouteBuilder.RescheduleAppointment(identifier),
            BookingRouteBuilder.BuildReschedulePayload(newStartUtc),
            ct);

    public Task<AppointmentActionResult> RequestRescheduleAsync(
        string identifier, DateTime proposedStartUtc, CancellationToken ct = default) =>
        PostAppointmentActionAsync(
            BookingRouteBuilder.RequestReschedule(identifier),
            BookingRouteBuilder.BuildReschedulePayload(proposedStartUtc),
            ct);

    public Task<AppointmentActionResult> AnswerRescheduleAsync(
        string identifier, bool approve, CancellationToken ct = default) =>
        PostAppointmentActionAsync(
            BookingRouteBuilder.AnswerReschedule(identifier),
            BookingRouteBuilder.BuildAnswerReschedulePayload(approve),
            ct);

    private async Task<AppointmentActionResult> PostAppointmentActionAsync(
        RouteSpec route, object payload, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        using var request = new HttpRequestMessage(route.Method, route.Path)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        return await SendAppointmentActionAsync(client, request, ct);
    }

    /// <summary>
    /// Sends an appointment action and words its outcome.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The server's own message is preferred on a refusal, because these refusals are specific and only the
    /// server holds what makes them so — the cancellation deadline, whether a slot was taken in between. A
    /// generic client string in its place throws away the one thing the reader can act on.
    /// </para>
    /// <para>
    /// A transport failure is reported as unreachable and nothing else, so "the server said no" and "the server
    /// was never reached" cannot arrive worded the same. That distinction is why these return a result rather
    /// than a bool.
    /// </para>
    /// </remarks>
    private static async Task<AppointmentActionResult> SendAppointmentActionAsync(
        HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return AppointmentActionResult.Done();

            // The Gateway names the cluster it could not reach; that is a reachability failure dressed as an HTTP
            // status, so it is worded as one rather than as a refusal.
            var failedService = await response.TryReadFailedServiceAsync(ct);
            if (failedService is not null)
                return new AppointmentActionResult(false, GatewayErrorMapper.Describe(failedService));

            return AppointmentActionResult.Refused(
                response.StatusCode, await ReadRefusalMessageAsync(response, ct));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return AppointmentActionResult.Unreachable();
        }
    }

    /// <summary>
    /// The server's explanation for a refusal, from either shape these routes answer with.
    /// </summary>
    /// <remarks>
    /// A 409 from the reschedule routes is <c>TypedResults.Conflict(string)</c> — a bare JSON string — while a
    /// 400 from cancel is a <c>DataResponse&lt;T&gt;</c> whose <c>errors</c> array holds the message. Both are
    /// read, because picking one would silently lose the other's wording, and the wording is the point.
    /// </remarks>
    private static async Task<string?> ReadRefusalMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(raw)) return null;

            using var document = JsonDocument.Parse(raw);

            if (document.RootElement.ValueKind == JsonValueKind.String)
                return document.RootElement.GetString();

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array)
            {
                var messages = errors.EnumerateArray()
                    .Where(error => error.ValueKind == JsonValueKind.String)
                    .Select(error => error.GetString())
                    .Where(message => !string.IsNullOrWhiteSpace(message));

                var joined = string.Join(" ", messages);
                return string.IsNullOrWhiteSpace(joined) ? null : joined;
            }

            return null;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            // An unparseable body must not turn a refusal into a crash; the caller's generic wording covers it.
            return null;
        }
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
