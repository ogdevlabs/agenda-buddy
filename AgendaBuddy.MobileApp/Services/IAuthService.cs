namespace AgendaBuddy.MobileApp.Services;

public interface IAuthService
{
    Task<bool> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<bool> RegisterAsync(string email, string password, string role, CancellationToken ct = default);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();
}
