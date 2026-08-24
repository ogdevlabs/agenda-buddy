using System.Text;
using System.Text.Json;
using Library.Entities;
using MobileApp.Models;
using MobileApp.Routing;

namespace MobileApp.Services;

public class BookingApiService : IBookingApiService
{
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public BookingApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<AppointmentSummary>> GetTodayAppointmentsAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = BookingRouteBuilder.TodayAppointments(DateOnly.FromDateTime(DateTime.UtcNow));

        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<AppointmentSummary>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<AppointmentSummary>>(json, JsonOptions)
               ?? new List<AppointmentSummary>();
    }

    public async Task<AppointmentDetail?> GetAppointmentAsync(string id, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = BookingRouteBuilder.Appointment(id);
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<AppointmentDetail>(json, JsonOptions);
    }

    public async Task<AppointmentDetail?> UpdateStatusAsync(string id, AppointmentStatus status, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = BookingRouteBuilder.UpdateAppointmentStatus(id);
        var body = JsonSerializer.Serialize(BookingRouteBuilder.BuildUpdateStatusPayload(status), JsonOptions);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.PutAsync(route.Path, content, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<AppointmentDetail>(json, JsonOptions);
    }
}
