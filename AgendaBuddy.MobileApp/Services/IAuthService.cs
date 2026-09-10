namespace AgendaBuddy.MobileApp.Services;

public interface IAuthService
{
    bool EmailVerificationRequired { get; }
    Task<bool> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<bool> RegisterAsync(string email, string password, string role, CancellationToken ct = default);
    Task<bool> ConfirmEmailAsync(string token, CancellationToken ct = default);
    Task<bool> RequestEmailVerificationAsync(string email, CancellationToken ct = default);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();

    /// <summary>
    /// Always succeeds from the caller's point of view — the real route answers 202 whether or not the
    /// address matched an account (anti-enumeration, AuthModule.cs). Returns false only on a transport/5xx
    /// failure.
    /// </summary>
    Task<bool> RequestPasswordResetAsync(string email, CancellationToken ct = default);

    Task<bool> ConfirmPasswordResetAsync(string email, string token, string newPassword, CancellationToken ct = default);

    /// <summary>
    /// Deletes the caller's own credential, so sign-in stops working, then clears the local session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <b>credential half</b> of account deletion, and it must be called <b>after</b>
    /// <c>ICustomerApiService.DeleteAccountAsync</c> / <c>IProviderApiService.DeleteAccountAsync</c>: this
    /// credential is what authorises those, so deleting it first strands a live profile nothing can reach.
    /// </para>
    /// <para>
    /// Returns <c>true</c> when the credential is gone. The local session is cleared either way, because a device
    /// left holding a token for an account the user has asked to delete is the worse failure.
    /// </para>
    /// </remarks>
    Task<bool> DeleteAccountAsync(CancellationToken ct = default);
}
