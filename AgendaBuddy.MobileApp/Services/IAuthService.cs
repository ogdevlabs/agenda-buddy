namespace AgendaBuddy.MobileApp.Services;

public interface IAuthService
{
    Task<bool> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<bool> RegisterAsync(string email, string password, string role, CancellationToken ct = default);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();

    /// <summary>
    /// Always succeeds from the caller's point of view — the real route answers 202 whether or not the
    /// address matched an account (anti-enumeration, AuthModule.cs). Returns false only on a transport/5xx
    /// failure.
    /// </summary>
    Task<bool> RequestPasswordResetAsync(string email, CancellationToken ct = default);

    Task<bool> ConfirmPasswordResetAsync(string email, string token, string newPassword, CancellationToken ct = default);
}
