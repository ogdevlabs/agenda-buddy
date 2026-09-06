using System.Net;
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

    public async Task<List<NotificationSummary>> GetNotificationsAsync(
        int? limit = null, bool unreadOnly = false, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = NotificationRouteBuilder.Notifications(limit, unreadOnly);
        var response = await client.GetAsync(route.Path, ct);

        // 401 is not this method's to report: JwtDelegatingHandler raises UnauthorizedAccess on it and the
        // Shell navigates to login, so an empty list here just avoids a banner on a screen that is going away.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return [];

        // Anything else that failed is thrown, so the view model's catch shows the error banner. Returning an
        // empty list would render the "No notifications yet" empty state instead — telling a user their inbox
        // is empty when the truth is that it could not be read.
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<NotificationSummary>>(json, JsonOptions) ?? [];
    }

    public async Task<long?> GetUnreadCountAsync(CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
            var route = NotificationRouteBuilder.UnreadCount();
            var response = await client.GetAsync(route.Path, ct);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<UnreadCountPayload>(json, JsonOptions)?.UnreadCount;
        }
        catch (Exception)
        {
            // A badge is decoration. It must never be the reason a screen shows an error, so unlike the list
            // this one absorbs its own failure — but it reports "unknown", not "nothing unread". Zero here used
            // to clear the badge on any network blip, which is the badge lying about the one thing it is for.
            return null;
        }
    }

    public async Task<bool> MarkReadAsync(string id, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = NotificationRouteBuilder.MarkRead(id);
        var response = await client.PostAsync(route.Path, null, ct);

        // The route answers 204 No Content, so success is the whole result. The caller must not mark its own
        // copy read on a failure, or the unread count drifts from what the next reload reports.
        return response.IsSuccessStatusCode;
    }

    public async Task<long?> MarkAllReadAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = NotificationRouteBuilder.MarkAllRead();
        var response = await client.PostAsync(route.Path, null, ct);

        // Null, not zero: "the request failed" and "there was nothing left to mark" need different words on
        // screen, and collapsing them here leaves the caller unable to choose.
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<MarkAllReadPayload>(json, JsonOptions)?.MarkedRead ?? 0;
    }

    // Mirrors Customer's UnreadCountResponse/MarkAllReadResponse. Private because nothing outside this class
    // has any use for the envelope — callers get the number.
    private sealed record UnreadCountPayload(long UnreadCount);

    private sealed record MarkAllReadPayload(long MarkedRead);
}
