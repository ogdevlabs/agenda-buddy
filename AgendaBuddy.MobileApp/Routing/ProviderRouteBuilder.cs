namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Route-building logic for <see cref="Services.ProviderApiService"/>'s report and deactivation routes
/// (api-contracts.md §2).
/// </summary>
public static class ProviderRouteBuilder
{
    /// <summary>
    /// <c>GET /api/v1/providers</c> — the browse/directory list. Returns a
    /// <c>DataResponse&lt;PagedResponse&lt;ProviderSummary&gt;&gt;</c> envelope (ADR-023), same shape as
    /// <see cref="CustomerRouteBuilder.Customers"/>'s paged read.
    /// </summary>
    public static RouteSpec Providers(int page = 1, int pageSize = 25) =>
        new(HttpMethod.Get, $"api/v1/providers?page={page}&pageSize={pageSize}");

    public static RouteSpec GetProvider(string email) => new(HttpMethod.Get, $"api/v1/providers/{email}");

    /// <summary>
    /// <c>{email}</c> must be the caller's own claim (<c>OwnershipGuard.AssertOwner</c>, ProviderModule.cs).
    /// <c>ProviderEntity</c> requires <c>FirstName</c>/<c>LastName</c>/<c>Email</c>.
    /// </summary>
    public static RouteSpec UpdateProvider(string email) => new(HttpMethod.Put, $"api/v1/providers/{email}");

    public static object BuildUpdateProviderPayload(string email, string firstName, string lastName) =>
        new { email, firstName, lastName };

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
