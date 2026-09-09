namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Hosted by the Customer service under a top-level <c>/api/v1/messages</c> group (ADR D-2) —
/// not nested under <c>/api/v1/customers</c>.
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

    /// <summary>
    /// Marks a whole conversation read in one request — <c>POST /api/v1/messages/thread/{counterpartEmail}/read</c>,
    /// keyed on the counterpart's email for the same reason <see cref="Thread"/> is. No body; answers the number
    /// of messages it changed.
    /// </summary>
    /// <remarks>
    /// Three segments, so it cannot collide with the two-segment <see cref="MarkRead"/> pattern.
    /// </remarks>
    public static RouteSpec MarkThreadRead(string counterpartEmail) =>
        new(HttpMethod.Post, $"api/v1/messages/thread/{counterpartEmail}/read");

    /// <summary>
    /// The longest body the server accepts (<c>MessageService.MaxBodyLength</c>). Declared here so the compose
    /// box can stop a message the server would refuse, rather than sending it and reporting a failure.
    /// </summary>
    public const int MaxBodyLength = 4000;
}
