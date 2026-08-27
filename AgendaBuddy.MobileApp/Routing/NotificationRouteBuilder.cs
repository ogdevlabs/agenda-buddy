namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Hosted by the Customer service under a top-level <c>/api/v1/notifications</c> group (ADR D-2).
/// </summary>
public static class NotificationRouteBuilder
{
    public static RouteSpec Notifications() => new(HttpMethod.Get, "api/v1/notifications");

    /// <summary>
    /// <c>POST</c>, not <c>PATCH</c> — the real route is <c>notifications.MapPost("/{id}/read", …)</c>
    /// (Customer/Program.cs). No body.
    /// </summary>
    public static RouteSpec MarkRead(string id) =>
        new(HttpMethod.Post, $"api/v1/notifications/{id}/read");
}
