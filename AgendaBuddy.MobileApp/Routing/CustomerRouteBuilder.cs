namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Customer's list route requires the Provider role and returns a paginated envelope (ADR-023).
/// </summary>
public static class CustomerRouteBuilder
{
    public static RouteSpec Customers() => new(HttpMethod.Get, "api/v1/customers");

    public static RouteSpec GetCustomer(string email) => new(HttpMethod.Get, $"api/v1/customers/{email}");

    /// <summary>
    /// <c>{email}</c> must be the caller's own claim (<c>OwnershipGuard.AssertOwner</c>, CustomerModule.cs).
    /// <c>CustomerEntity</c> requires <c>FirstName</c>/<c>LastName</c>/<c>Email</c> — all three must be
    /// present in the body or <c>MiniValidator.TryValidate</c> rejects it.
    /// </summary>
    public static RouteSpec UpdateCustomer(string email) => new(HttpMethod.Put, $"api/v1/customers/{email}");

    /// <summary>
    /// <c>POST /api/v1/customers</c> — creates the domain profile that registration alone does not. Until
    /// it exists, subscribing to a provider answers 404: the repository never upserts, so a PUT against a
    /// missing customer has nothing to update.
    /// </summary>
    public static RouteSpec CreateCustomer() => new(HttpMethod.Post, "api/v1/customers");

    public static object BuildCreateCustomerPayload(
        string email, string firstName, string lastName, string? phoneNumber) =>
        new { email, firstName, lastName, phoneNumber };

    public static object BuildUpdateCustomerPayload(string email, string firstName, string lastName) =>
        new { email, firstName, lastName };

    /// <summary><c>{email}</c> must be the caller's own claim — CustomerModule.cs's OwnershipGuard.AssertOwner.</summary>
    public static RouteSpec Subscribe(string email, string providerEmail) =>
        new(HttpMethod.Post, $"api/v1/customers/{email}/subscriptions/{providerEmail}");

    public static RouteSpec Unsubscribe(string email, string providerEmail) =>
        new(HttpMethod.Delete, $"api/v1/customers/{email}/subscriptions/{providerEmail}");

    public static RouteSpec Subscriptions(string email) =>
        new(HttpMethod.Get, $"api/v1/customers/{email}/subscriptions");
}
