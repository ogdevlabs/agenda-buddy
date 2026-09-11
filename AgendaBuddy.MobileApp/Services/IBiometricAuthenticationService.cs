namespace AgendaBuddy.MobileApp.Services;

public interface IBiometricAuthenticationService
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default);
}

public sealed class UnavailableBiometricAuthenticationService : IBiometricAuthenticationService
{
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
}