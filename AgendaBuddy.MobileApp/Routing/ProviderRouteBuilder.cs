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
    /// <remarks>
    /// <c>bookableOnly=true</c> is sent because this is the customer-facing directory: a provider with no
    /// active, profession-classified service cannot be booked, so offering one dead-ends the flow. It is a
    /// query parameter rather than the route's default — the endpoint itself is a general paginated list
    /// whose <c>totalCount</c> and paging other callers rely on.
    /// </remarks>
    public static RouteSpec Providers(int page = 1, int pageSize = 25, bool bookableOnly = true) =>
        new(HttpMethod.Get,
            $"api/v1/providers?page={page}&pageSize={pageSize}&bookableOnly={(bookableOnly ? "true" : "false")}");

    public static RouteSpec GetProvider(string email) => new(HttpMethod.Get, $"api/v1/providers/{email}");

    /// <summary>
    /// <c>{email}</c> must be the caller's own claim (<c>OwnershipGuard.AssertOwner</c>, ProviderModule.cs).
    /// <c>ProviderEntity</c> requires <c>FirstName</c>/<c>LastName</c>/<c>Email</c>.
    /// </summary>
    public static RouteSpec UpdateProvider(string email) => new(HttpMethod.Put, $"api/v1/providers/{email}");

    /// <summary>
    /// <c>POST /api/v1/providers</c> — creates the domain profile that registration alone does not.
    /// </summary>
    /// <remarks>
    /// The body is a FLAT ProviderEntity, not <c>{"providerEntity": …}</c> as <c>AddProviderCommand</c>'s
    /// single property name suggests; the wrapped form is rejected with 400 for missing Email/FirstName/LastName.
    /// </remarks>
    public static RouteSpec CreateProvider() => new(HttpMethod.Post, "api/v1/providers");

    public static object BuildCreateProviderPayload(
        string email, string firstName, string lastName, string? phoneNumber) =>
        new { email, firstName, lastName, phoneNumber, timeZoneId = TimeZoneInfo.Local.Id };

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

    /// <summary>
    /// <c>PUT /api/v1/providers/{email}/work-hours</c> — the provider's own working-day bounds. A dedicated
    /// route, not a field on <see cref="UpdateProvider"/>, because that one replaces the whole document.
    /// <c>{email}</c> must be the caller's own claim.
    /// </summary>
    public static RouteSpec WorkHours(string email) =>
        new(HttpMethod.Put, $"api/v1/providers/{email}/work-hours");

    /// <summary><c>endHour</c> is exclusive: 8–17 means the last session finishes at 17:00.</summary>
    public static object BuildWorkHoursPayload(int startHour, int endHour) =>
        new { startHour, endHour };
}
