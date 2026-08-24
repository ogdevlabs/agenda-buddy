namespace MobileApp.Routing;

/// <summary>
/// Route-building logic extracted from <see cref="Services.CalendarApiService"/> (F-015-T06).
/// </summary>
public static class CalendarRouteBuilder
{
    public static RouteSpec Availability(DateOnly from, int days) =>
        new(HttpMethod.Get, $"calendar?from={from:yyyy-MM-dd}&days={days}");
}
