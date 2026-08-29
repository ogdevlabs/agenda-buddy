using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

public interface ICustomerApiService
{
    Task<List<CustomerSummary>> GetCustomersAsync(CancellationToken ct = default);

    /// <summary>
    /// <c>customerEmail</c> must be the caller's own claim (<c>OwnershipGuard.AssertOwner</c>) — Subscribe
    /// and Unsubscribe are idempotent server-side ($addToSet/$pull), so a repeat call is a success.
    /// </summary>
    Task<bool> SubscribeAsync(string customerEmail, string providerEmail, CancellationToken ct = default);

    Task<bool> UnsubscribeAsync(string customerEmail, string providerEmail, CancellationToken ct = default);

    Task<List<string>> GetSubscriptionsAsync(string customerEmail, CancellationToken ct = default);

    Task<ProfileInfo?> GetProfileAsync(string email, CancellationToken ct = default);

    Task<bool> UpdateProfileAsync(string email, string firstName, string lastName, CancellationToken ct = default);
}
