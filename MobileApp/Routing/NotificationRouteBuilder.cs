namespace MobileApp.Routing;

/// <summary>
/// Route-building logic extracted from <see cref="Services.NotificationApiService"/> (F-015-T06), corrected
/// to the real backend contract (F-015-T07, api-contracts.md §2). Hosted by the Customer service under a
/// top-level <c>/api/v1/notifications</c> group (F-014, ADR D-2).
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
