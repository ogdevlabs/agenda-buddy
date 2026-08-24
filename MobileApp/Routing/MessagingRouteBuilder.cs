namespace MobileApp.Routing;

/// <summary>
/// Route-building logic extracted from <see cref="Services.MessagingApiService"/> (F-015-T06), corrected to
/// the real backend contract (F-015-T07, api-contracts.md §2). Hosted by the Customer service under a
/// top-level <c>/api/v1/messages</c> group (F-014, ADR D-2) — not nested under <c>/api/v1/customers</c>.
/// </summary>
public static class MessagingRouteBuilder
{
    public static RouteSpec Inbox() => new(HttpMethod.Get, "api/v1/messages");

    /// <summary>
    /// The real route keys on the OTHER PARTY'S EMAIL (<c>GET /api/v1/messages/thread/{counterpartEmail}</c>),
    /// not an opaque thread id — <c>MessageService</c> derives the thread id server-side by sorting both
    /// addresses. The caller must pass the counterpart's email.
    /// </summary>
    public static RouteSpec Thread(string counterpartEmail) =>
        new(HttpMethod.Get, $"api/v1/messages/thread/{counterpartEmail}");

    public static RouteSpec SendMessage() => new(HttpMethod.Post, "api/v1/messages");

    /// <summary>Payload shape Customer's <c>MessageRequest(RecipientEmail, Body)</c> binds.</summary>
    public static object BuildSendMessagePayload(string recipientEmail, string body) =>
        new { recipientEmail, body };

    /// <summary>
    /// <c>POST</c>, not <c>PATCH</c> — the real route is <c>messages.MapPost("/{id}/read", …)</c>
    /// (Customer/Program.cs). No body.
    /// </summary>
    public static RouteSpec MarkRead(string id) =>
        new(HttpMethod.Post, $"api/v1/messages/{id}/read");
}
