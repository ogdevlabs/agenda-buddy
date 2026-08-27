using System.Text;
using System.Text.Json;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

public class MessagingApiService : IMessagingApiService
{
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public MessagingApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<MessageThreadStub>> GetInboxAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = MessagingRouteBuilder.Inbox();
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<MessageThreadStub>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<MessageThreadStub>>(json, JsonOptions)
               ?? new List<MessageThreadStub>();
    }

    // F-015-T07: the real backend route keys on the counterpart's EMAIL, not an opaque thread id — see
    // MessagingRouteBuilder.Thread. Callers of this method must pass the other party's email; wiring the
    // ViewModel layer to supply it (rather than the fabricated ThreadId it currently holds) is SeedDataProvider
    // removal's concern (F-015-T08), not this route correction.
    public async Task<List<MessageSummary>> GetThreadAsync(string counterpartEmail, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = MessagingRouteBuilder.Thread(counterpartEmail);
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<MessageSummary>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<MessageSummary>>(json, JsonOptions)
               ?? new List<MessageSummary>();
    }

    public async Task<MessageSummary?> SendMessageAsync(string recipientEmail, string body, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = MessagingRouteBuilder.SendMessage();
        var payload = JsonSerializer.Serialize(
            MessagingRouteBuilder.BuildSendMessagePayload(recipientEmail, body), JsonOptions);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(route.Path, content, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<MessageSummary>(json, JsonOptions);
    }

    // F-015-T07: the real route (Customer/Program.cs, messages.MapPost("/{id}/read", ...)) answers
    // 204 No Content, not the updated entity. See NotificationApiService.MarkReadAsync for the identical
    // reasoning.
    public async Task<MessageSummary?> MarkReadAsync(string id, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = MessagingRouteBuilder.MarkRead(id);
        var response = await client.PostAsync(route.Path, null, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<MessageSummary>(json, JsonOptions);
    }
}
