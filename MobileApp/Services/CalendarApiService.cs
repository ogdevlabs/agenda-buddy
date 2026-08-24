using System.Text.Json;
using MobileApp.Models;
using MobileApp.Routing;

namespace MobileApp.Services;

public class CalendarApiService : ICalendarApiService
{
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CalendarApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<CalendarDaySummary>> GetAvailabilityAsync(int days = 30, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = CalendarRouteBuilder.Availability(DateOnly.FromDateTime(DateTime.UtcNow), days);

        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<CalendarDaySummary>();

        var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<List<CalendarDaySummary>>(stream, _jsonOptions, ct)
               ?? new List<CalendarDaySummary>();
    }
}
