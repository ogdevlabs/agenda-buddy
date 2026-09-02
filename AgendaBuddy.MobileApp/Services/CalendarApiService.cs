using System.Text.Json;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

public class CalendarApiService : ICalendarApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUserSessionService _session;
    private readonly ICustomerApiService _customerApiService;
    private readonly IProviderApiService _providerApiService;

    public CalendarApiService(
        IHttpClientFactory httpClientFactory,
        IUserSessionService session,
        ICustomerApiService customerApiService,
        IProviderApiService providerApiService)
    {
        _httpClientFactory = httpClientFactory;
        _session = session;
        _customerApiService = customerApiService;
        _providerApiService = providerApiService;
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
        var route = CalendarRouteBuilder.Availability(_session.Email, days);

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


    public async Task<ProviderAvailability> GetProviderAvailabilityAsync(
        string providerEmail, string? serviceName, int days = 90, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerEmail))
            return ProviderAvailability.Empty;

        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CalendarRouteBuilder.Availability(providerEmail, days, serviceName);

        var response = await client.GetAsync(route.Path, ct);

        // 404 means "no such provider" now; a fully-booked provider answers 200 with an empty list. Both
        // land here as "nothing to offer", which is what the UI shows -- it must not read either as an error,
        // because a booked-out calendar is a normal state.
        if (!response.IsSuccessStatusCode)
            return ProviderAvailability.Empty;

        var json = await response.Content.ReadAsStringAsync(ct);
        return GroupByDate(ParseAvailabilitySlots(json));
    }

    /// <summary>
    /// Groups free start times by the date they fall on FOR THIS DEVICE, dropping dates with nothing free
    /// so the calendar can tell "bookable" from "full" by presence alone.
    /// </summary>
    /// <remarks>
    /// Grouped on the local date, not the UTC one: a 01:00Z slot belongs to the previous evening for anyone
    /// behind UTC, and grouping by UTC put it on the wrong day's tile. The values keep the UTC instant,
    /// because that is what the booking POST must send back.
    /// </remarks>
    internal static ProviderAvailability GroupByDate(IEnumerable<DateTime> slots) =>
        new()
        {
            SlotsByDate = slots
                .Select(slot => new AvailabilitySlot(slot.Kind == DateTimeKind.Utc ? slot : slot.ToUniversalTime()))
                .GroupBy(slot => DateOnly.FromDateTime(slot.LocalStart))
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(slot => slot.StartUtc).ToList())
        };

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
        // The appointment payload carries only the counterpart's email, so a name and phone have to come
        // from the directory. Best-effort and deliberately non-fatal: a directory that fails to load
        // degrades to the email address, which is what every row showed before, rather than losing the
        // appointments themselves.
        var directory = await LoadCounterpartDirectoryAsync(ct);

        foreach (var appointment in appointments)
        {
            appointment.ContactEmail = _session.IsProvider ? appointment.CustomerEmail : appointment.ProviderEmail;

            if (directory.TryGetValue(appointment.ContactEmail, out var contact))
            {
                if (!string.IsNullOrWhiteSpace(contact.FullName))
                    appointment.DisplayName = contact.FullName;

                // Both, deliberately: the dashboard card reads CustomerPhone and the detail page's contact
                // block reads ContactPhone. Filling only one left whichever screen used the other blank.
                appointment.CustomerPhone = contact.Phone;
                appointment.ContactPhone = contact.Phone;
            }

            if (string.IsNullOrWhiteSpace(appointment.DisplayName))
                appointment.DisplayName = appointment.ContactEmail;

            // Appointments are persisted UTC, but every consumer of ScheduledAt treats it as wall-clock:
            // it is formatted with {0:h:mm tt} for display and compared against DateTime.Today to decide
            // what counts as "today". Left in UTC, a user at UTC-6 saw a 17:00Z session as "5:00 PM" when
            // it is 11:00 AM for them, and a late-evening session counted toward the wrong day. Converting
            // once here fixes display and those comparisons together. Nothing sends ScheduledAt back —
            // booking supplies its own start/end, and cancel/status go by identifier — so this cannot
            // round-trip a shifted instant.
            appointment.ScheduledAt = appointment.ScheduledAt.Kind == DateTimeKind.Utc
                ? appointment.ScheduledAt.ToLocalTime()
                : appointment.ScheduledAt;
        }

        return appointments;
    }

    /// <summary>
    /// The counterparts this session can be booked with, keyed by email: customers for a Provider,
    /// providers for a Customer. Swallows failure and returns empty — this only enriches the display, so a
    /// directory outage must not take the appointment list with it.
    /// </summary>
    private async Task<Dictionary<string, CustomerSummary>> LoadCounterpartDirectoryAsync(CancellationToken ct)
    {
        try
        {
            var contacts = _session.IsProvider
                ? await _customerApiService.GetCustomersAsync(ct)
                : await _providerApiService.GetProvidersAsync(ct);

            return contacts
                .Where(contact => !string.IsNullOrWhiteSpace(contact.Email))
                .GroupBy(contact => contact.Email, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return new Dictionary<string, CustomerSummary>(StringComparer.OrdinalIgnoreCase);
        }
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
            Status = GetStatus(element),
            // Both are on the wire and were simply never read, so every screen bound to ServiceName
            // rendered an empty row even though the appointment records which service it was booked for.
            ServiceName = GetString(element, "serviceName"),
            ServiceDurationMinutes = GetInt(element, "serviceDurationMinutes")
        };
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int? GetInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

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
