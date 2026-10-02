namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// The signed-in account's calendar feed. The server resolves the account from the token, so no route carries an
/// address.
/// </summary>
public static class CalendarFeedRouteBuilder
{
    public const string Path = "api/v1/calendar/feed";

    public static RouteSpec Status() => new(HttpMethod.Get, Path);

    /// <summary>Turns the feed on, or replaces it: the previous link stops working the moment this succeeds.</summary>
    public static RouteSpec Enable() => new(HttpMethod.Post, Path);

    public static RouteSpec Disable() => new(HttpMethod.Delete, Path);

    public static object BuildEnablePayload(string? language) => new { language };
}
