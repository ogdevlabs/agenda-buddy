namespace MobileApp.Routing;

/// <summary>
/// Route-building logic extracted from <see cref="Services.MessagingApiService"/> (F-015-T06).
/// </summary>
public static class MessagingRouteBuilder
{
    public static RouteSpec Inbox() => new(HttpMethod.Get, "messages");

    public static RouteSpec Thread(string threadId) =>
        new(HttpMethod.Get, $"messages/thread/{threadId}");

    public static RouteSpec SendMessage() => new(HttpMethod.Post, "messages");

    public static object BuildSendMessagePayload(string recipientEmail, string body) =>
        new { recipientEmail, body };

    public static RouteSpec MarkRead(string id) =>
        new(HttpMethod.Patch, $"messages/{id}/read");
}
