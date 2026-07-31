using System.Text;
using System.Text.Json;
using MobileApp.Models;

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
        var url = $"booking?date={DateTime.UtcNow:yyyy-MM-dd}";

        var response = await client.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
            return new List<AppointmentSummary>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<AppointmentSummary>>(json, JsonOptions)
               ?? new List<AppointmentSummary>();
    }

    public async Task<AppointmentSummary?> UpdateStatusAsync(string id, string status, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var body = JsonSerializer.Serialize(new { status }, JsonOptions);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.PutAsync($"booking/{id}", content, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<AppointmentSummary>(json, JsonOptions);
    }
}
