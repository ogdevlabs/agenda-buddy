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

    /// <summary>Creates the domain profile that <c>POST api/v1/auth/register</c> does not.</summary>
    Task<bool> CreateProfileAsync(string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default);

    Task<bool> UpdateProfileAsync(string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default);

    /// <summary>
    /// Sets which built-in mark this account is drawn with, through the dedicated avatar route.
    /// </summary>
    /// <remarks>
    /// A targeted write, so changing an avatar cannot disturb the subscription or appointment lists the way the
    /// fetch-merge-PUT in <see cref="UpdateProfileAsync"/> could.
    /// </remarks>
    Task<bool> SetAvatarAsync(string email, string avatarId, CancellationToken ct = default);

    /// <summary>
    /// Records acceptance — or non-acceptance — of the Terms and the Privacy Policy. The server stamps the time.
    /// </summary>
    Task<bool> SetConsentAsync(string email, bool acceptedTerms, bool acceptedPrivacy, CancellationToken ct = default);

    /// <summary>
    /// Erases the domain profile and scrubs the address from every dependent record.
    /// </summary>
    /// <remarks>
    /// The domain half only — <see cref="IAuthService.DeleteAccountAsync"/> removes the credential, and must be
    /// called second because this call authorises off it.
    /// </remarks>
    Task<bool> DeleteAccountAsync(string email, CancellationToken ct = default);
}
