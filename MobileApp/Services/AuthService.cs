namespace MobileApp.Services;

public class AuthService : IAuthService
{
    public Task<bool> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task LogoutAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
