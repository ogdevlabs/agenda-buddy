using System.Text.Json;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

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
        var route = NotificationRouteBuilder.Notifications();
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<NotificationSummary>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<NotificationSummary>>(json, JsonOptions)
               ?? new List<NotificationSummary>();
    }

    // The real route (Customer/Program.cs, notifications.MapPost("/{id}/read", ...)) answers
    // 204 No Content, not the updated entity. Returning null on an empty body (rather than throwing on
    // an empty-string deserialize) keeps the existing caller contract
    // (NotificationsViewModel.MarkReadAsync already no-ops on null).
    public async Task<NotificationSummary?> MarkReadAsync(string id, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = NotificationRouteBuilder.MarkRead(id);
        var response = await client.PostAsync(route.Path, null, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<NotificationSummary>(json, JsonOptions);
    }
}
