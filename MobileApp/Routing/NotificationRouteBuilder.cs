namespace MobileApp.Routing;

/// <summary>
/// Route-building logic extracted from <see cref="Services.NotificationApiService"/> (F-015-T06).
/// </summary>
public static class NotificationRouteBuilder
{
    public static RouteSpec Notifications() => new(HttpMethod.Get, "notifications");

    public static RouteSpec MarkRead(string id) =>
        new(HttpMethod.Patch, $"notifications/{id}/read");
}
