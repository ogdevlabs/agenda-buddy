namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Hosted by the Customer service under a top-level <c>/api/v1/notifications</c> group (ADR D-2).
/// </summary>
public static class NotificationRouteBuilder
{
    /// <summary>
    /// The newest notifications, newest first.
    /// </summary>
    /// <param name="limit">
    /// Page size. The route clamps to 1–200, so an out-of-range value costs nothing — it is sent as given
    /// rather than pre-clamped here, so the clamp has one home and not two that can disagree.
    /// </param>
    public static RouteSpec Notifications(int? limit = null, bool unreadOnly = false)
    {
        var query = new List<string>();
        if (limit.HasValue) query.Add($"limit={limit.Value}");
        if (unreadOnly) query.Add("unreadOnly=true");

        var path = "api/v1/notifications";
        if (query.Count > 0) path += "?" + string.Join("&", query);

        return new RouteSpec(HttpMethod.Get, path);
    }

    /// <summary>The unread count on its own, so a badge does not have to fetch the list to render.</summary>
    public static RouteSpec UnreadCount() => new(HttpMethod.Get, "api/v1/notifications/unread-count");

    /// <summary>
    /// <c>POST</c>, not <c>PATCH</c> — the real route is <c>notifications.MapPost("/{id}/read", …)</c>
    /// (Customer's NotificationModule). No body.
    /// </summary>
    public static RouteSpec MarkRead(string id) =>
        new(HttpMethod.Post, $"api/v1/notifications/{id}/read");

    /// <summary>
    /// Marks every unread notification read. <c>read-all</c>, not <c>read</c>, so it cannot collide with
    /// <see cref="MarkRead"/>'s <c>{id}/read</c>. No body: the route takes the account from the caller's own
    /// token, so there is nothing here for a caller to substitute.
    /// </summary>
    public static RouteSpec MarkAllRead() => new(HttpMethod.Post, "api/v1/notifications/read-all");
}
