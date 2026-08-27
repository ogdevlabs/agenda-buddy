namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Route-building logic extracted from <see cref="Services.CustomerApiService"/> (F-015-T06), corrected to
/// the real backend contract (F-015-T07, api-contracts.md §2). Customer's list route requires the Provider
/// role and returns a paginated envelope (F-016, ADR-023) — the prefix fix is the only change this route
/// needs; pagination itself is unchanged per api-contracts.md §3.
/// </summary>
public static class CustomerRouteBuilder
{
    public static RouteSpec Customers() => new(HttpMethod.Get, "api/v1/customers");
}
