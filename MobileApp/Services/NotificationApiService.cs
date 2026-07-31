using System.Text.Json;
using MobileApp.Models;

namespace MobileApp.Services;

public class NotificationApiService : INotificationApiService
{
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public NotificationApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<NotificationSummary>> GetNotificationsAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var response = await client.GetAsync("notifications", ct);

        if (!response.IsSuccessStatusCode)
            return new List<NotificationSummary>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<NotificationSummary>>(json, JsonOptions)
               ?? new List<NotificationSummary>();
    }

    public async Task<NotificationSummary?> MarkReadAsync(string id, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var response = await client.PatchAsync($"notifications/{id}/read", null, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<NotificationSummary>(json, JsonOptions);
    }
}
