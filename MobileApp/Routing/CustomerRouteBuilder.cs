namespace MobileApp.Routing;

/// <summary>
/// Route-building logic extracted from <see cref="Services.CustomerApiService"/> (F-015-T06).
/// </summary>
public static class CustomerRouteBuilder
{
    public static RouteSpec Customers() => new(HttpMethod.Get, "customer");
}
