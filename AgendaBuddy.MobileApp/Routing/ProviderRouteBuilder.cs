namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Route-building logic for <see cref="Services.ProviderApiService"/>'s report and deactivation routes
/// (api-contracts.md §2).
/// </summary>
public static class ProviderRouteBuilder
{
    /// <summary>
    /// <c>{email}</c> must be the caller's own claim — Provider/Program.cs guards role and ownership, not a
    /// selector. See <c>GET /api/v1/providers/{email}/report</c>.
    /// </summary>
    public static RouteSpec Report(string email) =>
        new(HttpMethod.Get, $"api/v1/providers/{email}/report");

    /// <summary>A provider deactivating themselves — no administrative bypass exists.</summary>
    public static RouteSpec Deactivate(string email) =>
        new(HttpMethod.Post, $"api/v1/providers/{email}/deactivate");
}
