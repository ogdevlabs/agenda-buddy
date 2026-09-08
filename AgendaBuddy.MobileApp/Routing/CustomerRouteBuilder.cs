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

    /// <summary>
    /// <c>PUT /api/v1/customers/{email}/avatar</c> — which built-in mark this account is drawn with. A dedicated
    /// route, not a field on <see cref="UpdateCustomer"/>, because that one replaces the whole document.
    /// <c>{email}</c> must be the caller's own claim.
    /// </summary>
    public static RouteSpec Avatar(string email) => new(HttpMethod.Put, $"api/v1/customers/{email}/avatar");

    /// <summary>Payload shape Customer's <c>AvatarRequest</c> binds. An unknown id is answered 400, not stored.</summary>
    public static object BuildAvatarPayload(string avatarId) => new { avatarId };

    /// <summary>
    /// <c>PUT /api/v1/customers/{email}/consent</c> — acceptance of the Terms and the Privacy Policy. Dedicated
    /// for the same reason <see cref="Avatar"/> is.
    /// </summary>
    public static RouteSpec Consent(string email) => new(HttpMethod.Put, $"api/v1/customers/{email}/consent");

    /// <summary>
    /// Payload shape Customer's <c>ConsentRequest</c> binds.
    /// </summary>
    /// <remarks>
    /// Booleans, not dates: the server stamps the time, because a consent record whose timestamp the consenting
    /// party supplies proves nothing. Both are always sent, so <c>false</c> reads as "not accepted" rather than
    /// "unchanged".
    /// </remarks>
    public static object BuildConsentPayload(bool acceptedTerms, bool acceptedPrivacy) =>
        new { acceptedTerms, acceptedPrivacy };

    /// <summary>
    /// <c>DELETE /api/v1/customers/{email}</c> — erases the profile and scrubs the address from every dependent
    /// record. The <b>domain half</b> of account deletion.
    /// </summary>
    /// <remarks>
    /// Call this <b>before</b> <see cref="AuthRouteBuilder.DeleteAccount"/>: the credential that route deletes is
    /// what authorises this one, so the reverse order strands a live profile nothing can reach.
    /// </remarks>
    public static RouteSpec DeleteCustomer(string email) => new(HttpMethod.Delete, $"api/v1/customers/{email}");
}
