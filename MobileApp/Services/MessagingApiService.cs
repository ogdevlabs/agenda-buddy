using System.Text;
using System.Text.Json;
using MobileApp.Models;

namespace MobileApp.Services;

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
        var response = await client.GetAsync("messages", ct);

        if (!response.IsSuccessStatusCode)
            return new List<MessageThreadStub>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<MessageThreadStub>>(json, JsonOptions)
               ?? new List<MessageThreadStub>();
    }

    public async Task<List<MessageSummary>> GetThreadAsync(string threadId, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var response = await client.GetAsync($"messages/thread/{threadId}", ct);

        if (!response.IsSuccessStatusCode)
            return new List<MessageSummary>();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<MessageSummary>>(json, JsonOptions)
               ?? new List<MessageSummary>();
    }

    public async Task<MessageSummary?> SendMessageAsync(string recipientEmail, string body, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var payload = JsonSerializer.Serialize(new { recipientEmail, body }, JsonOptions);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("messages", content, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<MessageSummary>(json, JsonOptions);
    }

    public async Task<MessageSummary?> MarkReadAsync(string id, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var response = await client.PatchAsync($"messages/{id}/read", null, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<MessageSummary>(json, JsonOptions);
    }
}
