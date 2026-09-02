using System.Text;
using System.Text.Json;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

public class MessagingApiService : IMessagingApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUserSessionService _session;

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public MessagingApiService(IHttpClientFactory httpClientFactory, IUserSessionService session)
    {
        _httpClientFactory = httpClientFactory;
        _session = session;
    }

    /// <summary>
    /// <c>GetInbox</c> (MessageModule.cs) answers a FLAT list of individual <c>MessageEntity</c> objects —
    /// every message in the caller's inbox — not a per-thread summary shape at all. Deserializing that
    /// straight into <see cref="MessageThreadStub"/> (the previous implementation) left every field but
    /// <c>ThreadId</c> at its default (blank sender, blank preview, one row per MESSAGE rather than per
    /// THREAD), because none of the other JSON property names match. This groups the flat list by
    /// <c>ThreadId</c> itself and derives each thread's summary client-side.
    /// </summary>
    public async Task<List<MessageThreadStub>> GetInboxAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
        var route = MessagingRouteBuilder.Inbox();
        var response = await client.GetAsync(route.Path, ct);

        if (!response.IsSuccessStatusCode)
            return new List<MessageThreadStub>();

        var json = await response.Content.ReadAsStringAsync(ct);
        var messages = JsonSerializer.Deserialize<List<MessageSummary>>(json, JsonOptions) ?? new List<MessageSummary>();
        return GroupIntoThreads(messages, _session.Email);
    }

    internal static List<MessageThreadStub> GroupIntoThreads(List<MessageSummary> messages, string callerEmail)
    {
        return messages
            .GroupBy(m => m.ThreadId)
            .Select(group =>
            {
                var latest = group.OrderByDescending(m => m.SentAt).First();
                var otherParty = string.Equals(latest.SenderEmail, callerEmail, StringComparison.OrdinalIgnoreCase)
                    ? latest.RecipientEmail
                    : latest.SenderEmail;

                return new MessageThreadStub
                {
                    ThreadId = group.Key,
                    OtherPartyEmail = otherParty,
                    LastMessageBody = latest.Body,
                    LastMessageAt = latest.SentAt,
                    UnreadCount = group.Count(m => !m.IsRead
                        && string.Equals(m.RecipientEmail, callerEmail, StringComparison.OrdinalIgnoreCase))
                };
            })
            .OrderByDescending(t => t.LastMessageAt)
            .ToList();
    }

    // The backend route keys on the counterpart's EMAIL, not an opaque thread id — see
    // MessagingRouteBuilder.Thread. Callers of this method must pass the other party's email.
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

    // The real route (Customer/Program.cs, messages.MapPost("/{id}/read", ...)) answers
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
