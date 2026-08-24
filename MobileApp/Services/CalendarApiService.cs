using System.Text.Json;
using Library.Entities;
using MobileApp.Models;
using MobileApp.Routing;

namespace MobileApp.Services;

public class CalendarApiService : ICalendarApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUserSessionService _session;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CalendarApiService(IHttpClientFactory httpClientFactory, IUserSessionService session)
    {
        _httpClientFactory = httpClientFactory;
        _session = session;
    }

    public async Task<List<CalendarDaySummary>> GetAvailabilityAsync(int days = 30, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CalendarRouteBuilder.Availability(_session.Email, DateOnly.FromDateTime(DateTime.UtcNow), days);

        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<CalendarDaySummary>();

        var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<List<CalendarDaySummary>>(stream, _jsonOptions, ct)
               ?? new List<CalendarDaySummary>();
    }

    public async Task<List<AppointmentDetail>> GetAppointmentsAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CalendarRouteBuilder.Appointments(_session.Email);

        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<AppointmentDetail>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseAppointments(json);
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
    internal static List<AppointmentDetail> ParseAppointments(string json)
    {
        var result = new List<AppointmentDetail>();

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var element in doc.RootElement.EnumerateArray())
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
