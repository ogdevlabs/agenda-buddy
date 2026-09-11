using LocalAuthentication;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp;

public sealed class IosBiometricAuthenticationService : IBiometricAuthenticationService
{
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        using var context = new LAContext();
        return Task.FromResult(context.CanEvaluatePolicy(
            LAPolicy.DeviceOwnerAuthenticationWithBiometrics,
            out _));
    }

    public async Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        using var context = new LAContext
        {
            LocalizedCancelTitle = AppResources.General_Cancel
        };

        if (!context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _))
            return false;

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() =>
        {
            context.Invalidate();
            completion.TrySetResult(false);
        });

        context.EvaluatePolicy(
            LAPolicy.DeviceOwnerAuthenticationWithBiometrics,
            AppResources.Biometric_Reason,
            (success, _) => completion.TrySetResult(success));

        return await completion.Task;
    }
}