using System.Text.Json;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

public class CalendarApiService : ICalendarApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUserSessionService _session;

    public CalendarApiService(IHttpClientFactory httpClientFactory, IUserSessionService session)
    {
        _httpClientFactory = httpClientFactory;
        _session = session;
    }

    /// <summary>
    /// The real endpoint (<c>AgendaBuddy.Calendar.Api/Modules/CalendarModule.cs</c>) wraps its body in
    /// <c>DataResponse&lt;List&lt;DateTime&gt;&gt;</c> and returns a flat list of free hourly slots
    /// (<c>SupportTools.GetThirtyDaysCalendarAvailability</c> — already excludes booked start times), not
    /// the day-grouped <c>{date,availableSlots,bookedSlots}</c> shape docs/pdlc/design/mobile-app/api-contracts.md
    /// describes. This groups the flat slot list by date itself and folds in the real booked appointments
    /// (from <see cref="GetAppointmentsAsync"/>) to populate <c>BookedSlots</c> — the backend has no route
    /// that returns booked slots grouped by day on its own.
    /// </summary>
    public async Task<List<CalendarDaySummary>> GetAvailabilityAsync(int days = 30, CancellationToken ct = default)
    {
        // "Availability" (free slots for someone else to book) is a Provider concept — a Customer has
        // none of their own, so CheckCalendarAvailabilityQuery always comes back empty for a Customer's
        // email, which CalendarModule.cs's /availability/{email} route (by design) answers 404. Calling
        // it here anyway meant a Customer's calendar always got !IsSuccessStatusCode and bailed out with
        // zero day-tiles instead of a real (if slot-less) week/month view of their own appointments.
        var appointments = await GetAppointmentsAsync(ct);
        var bookedByDate = appointments.ToLookup(a => a.ScheduledAt.Date);
        var noSlots = Enumerable.Empty<DateTime>().ToLookup(s => s);

        if (_session.IsCustomer)
            return BuildDayTiles(days, noSlots, bookedByDate);

        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CalendarRouteBuilder.Availability(_session.Email, DateOnly.FromDateTime(DateTime.UtcNow), days);

        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return BuildDayTiles(days, noSlots, bookedByDate);

        var json = await response.Content.ReadAsStringAsync(ct);
        var slotsByDate = ParseAvailabilitySlots(json).ToLookup(s => s.Date);

        return BuildDayTiles(days, slotsByDate, bookedByDate);
    }

    // Every day in [today, today+days) gets an entry, even with zero slots and zero bookings — the
    // day-selector strip on CalendarPage needs a contiguous run of `days` tiles, not just the dates the
    // flat slot list happened to mention. Grouping the flat list directly (its earlier shape here) drops
    // any day with nothing in either lookup, which collapses the strip to a sparse, unevenly-spaced set
    // of tiles instead of a real week/month view.
    private List<CalendarDaySummary> BuildDayTiles(
        int days, ILookup<DateTime, DateTime> slotsByDate, ILookup<DateTime, AppointmentDetail> bookedByDate)
    {
        var today = DateTime.Today;
        var result = new List<CalendarDaySummary>(days);
        for (var i = 0; i < days; i++)
        {
            var date = today.AddDays(i);
            result.Add(new CalendarDaySummary
            {
                Date = date.ToString("yyyy-MM-dd"),
                AvailableSlots = slotsByDate[date].OrderBy(s => s).Select(s => s.ToString("h:mm tt")).ToList(),
                BookedSlots = bookedByDate[date]
                    .OrderBy(a => a.ScheduledAt)
                    .Select(a => $"{a.ScheduledAt:h:mm tt} — {(_session.IsProvider ? a.CustomerEmail : a.ProviderEmail)}")
                    .ToList()
            });
        }

        return result;
    }

    /// <summary>Unwraps <c>{"data": [ISO-8601 datetimes], "errors": []}</c> into a flat list.</summary>
    internal static List<DateTime> ParseAvailabilitySlots(string json)
    {
        var result = new List<DateTime>();

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var element in data.EnumerateArray())
            if (element.TryGetDateTime(out var dt))
                result.Add(dt);

        return result;
    }

    public async Task<List<AppointmentDetail>> GetAppointmentsAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CalendarRouteBuilder.Appointments(_session.Email);

        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<AppointmentDetail>();

        var json = await response.Content.ReadAsStringAsync(ct);
        var appointments = ParseAppointments(json);

        // ParseAppointments is static and role-blind, so it cannot know which side of the appointment is
        // "the other party" — it left DisplayName empty, which rendered as a nameless row wherever an
        // appointment is listed. Fill it in here, where the session role is known: a Provider sees their
        // customer, a Customer sees their provider.
        foreach (var appointment in appointments)
        {
            appointment.ContactEmail = _session.IsProvider ? appointment.CustomerEmail : appointment.ProviderEmail;
            if (string.IsNullOrWhiteSpace(appointment.DisplayName))
                appointment.DisplayName = appointment.ContactEmail;
        }

        return appointments;
    }

    /// <summary>
    /// Deliberately NOT <c>JsonSerializer.Deserialize&lt;List&lt;AppointmentEntity&gt;&gt;</c>. Calendar does
    /// not register <c>ObjectIdJsonConverter</c> (Booking/Customer/Provider do; Calendar/Services/Profession
    /// are filed, pre-existing debt), so its <c>id</c> field is the broken
    /// <c>{timestamp,machine,pid,increment,creationTime}</c> shape — binding it to any property throws.
    /// This reads field-by-field and never touches <c>id</c>/<c>_id</c> at all, using <c>identifier</c>
    /// (a plain string, always) as the client-side id instead. Best-effort: full field fidelity between
    /// <see cref="AppointmentEntity"/> and <see cref="AppointmentDetail"/> is out of this task's scope.
    /// </summary>
    /// <remarks>
    /// The real endpoint wraps its body in <c>DataResponse&lt;List&lt;AppointmentEntity&gt;&gt;</c>
    /// (<c>{"data": [...], "errors": []}</c>) — this unwraps "data" before walking the array, rather than
    /// requiring the root itself to be an array.
    /// </remarks>
    internal static List<AppointmentDetail> ParseAppointments(string json)
    {
        var result = new List<AppointmentDetail>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
            root = data;

        if (root.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var element in root.EnumerateArray())
            result.Add(MapAppointment(element));

        return result;
    }

    private static AppointmentDetail MapAppointment(JsonElement element)
    {
        var customerEmail = GetString(element, "emailCustomer");

        return new AppointmentDetail
        {
            Id = GetString(element, "identifier"),
            CustomerEmail = customerEmail,
            ProviderEmail = GetString(element, "emailProvider"),
            ContactEmail = customerEmail,
            ScheduledAt = GetDateTime(element, "start"),
            Status = GetStatus(element)
        };
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static DateTime GetDateTime(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetDateTime(out var dt)
            ? dt
            : default;

    private static AppointmentStatus GetStatus(JsonElement element)
    {
        if (!element.TryGetProperty("appointmentStatus", out var value))
            return AppointmentStatus.Requested;

        if (value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var numeric)
            && Enum.IsDefined(typeof(AppointmentStatus), numeric))
            return (AppointmentStatus)numeric;

        if (value.ValueKind == JsonValueKind.String
            && Enum.TryParse<AppointmentStatus>(value.GetString(), ignoreCase: true, out var parsed))
            return parsed;

        return AppointmentStatus.Requested;
    }
}
