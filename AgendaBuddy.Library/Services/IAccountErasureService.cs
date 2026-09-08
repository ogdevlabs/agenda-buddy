using AgendaBuddy.Library.Accounts;

namespace AgendaBuddy.Library.Services;

/// <summary>
/// Erases an account: removes the profile and every trace of who it belonged to, while leaving the
/// counterparty's records standing.
/// </summary>
/// <remarks>
/// <para>
/// One implementation for both roles and one place the policy lives, for the same reason
/// <c>NotificationDispatcher</c> is one place: a scrub that is 90% complete is not a scrub, and the way it comes
/// to be 90% complete is a second copy of the list of collections that somebody forgot to extend.
/// </para>
/// <para>
/// <b>It deletes the profile and the caller deletes the credential, in that order.</b> The credential is what
/// authorises this operation, so removing it first would leave a live profile that nothing can reach to finish
/// erasing. See <c>IdentityService.DeleteAccountAsync</c> for the other half.
/// </para>
/// </remarks>
public interface IAccountErasureService
{
    /// <summary>Erases a customer account.</summary>
    Task<AccountErasureSummary> EraseCustomerAsync(string email);

    /// <summary>
    /// Erases a provider account.
    /// </summary>
    /// <remarks>
    /// The provider document carries the services and the embedded appointment list, so both go with it. Their
    /// customers' own records are scrubbed of the provider's address but otherwise kept — a customer's history of
    /// sessions they attended is theirs, not the provider's.
    /// </remarks>
    Task<AccountErasureSummary> EraseProviderAsync(string email);
}
