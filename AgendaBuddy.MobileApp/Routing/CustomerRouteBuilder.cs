namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Customer's list route requires the Provider role and returns a paginated envelope (ADR-023).
/// </summary>
public static class CustomerRouteBuilder
{
    public static RouteSpec Customers() => new(HttpMethod.Get, "api/v1/customers");
}
