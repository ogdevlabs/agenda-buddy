using Android.Content;
using Android.Hardware.Biometrics;
using Android.OS;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp;

public sealed class AndroidBiometricAuthenticationService : IBiometricAuthenticationService
{
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var activity = Platform.CurrentActivity;
        if (activity is null)
            return Task.FromResult(false);

        var manager = activity.GetSystemService(Context.BiometricService) as BiometricManager;
        if (manager is null)
            return Task.FromResult(false);

        var result = OperatingSystem.IsAndroidVersionAtLeast(30)
            ? manager.CanAuthenticate((int)BiometricManagerAuthenticators.BiometricStrong)
            : manager.CanAuthenticate();
        return Task.FromResult(result == BiometricCode.Success);
    }

    public async Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        var activity = Platform.CurrentActivity;
        var executor = activity?.MainExecutor;
        if (activity is null || executor is null)
            return false;

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callback = new AuthenticationCallback(completion);
        var cancellationSignal = new CancellationSignal();
        var prompt = new BiometricPrompt.Builder(activity)
            .SetTitle(AppResources.Biometric_Title)
            .SetSubtitle(AppResources.Biometric_Reason)
            .SetNegativeButton(
                AppResources.General_Cancel,
                executor,
                new CancelListener(completion))
            .Build();

        using var registration = cancellationToken.Register(() =>
        {
            cancellationSignal.Cancel();
            completion.TrySetResult(false);
        });

        prompt.Authenticate(cancellationSignal, executor, callback);
        return await completion.Task;
    }

    private sealed class AuthenticationCallback(TaskCompletionSource<bool> completion)
        : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? result)
        {
            base.OnAuthenticationSucceeded(result);
            completion.TrySetResult(true);
        }

        public override void OnAuthenticationError(
            BiometricErrorCode errorCode,
            Java.Lang.ICharSequence? errorMessage)
        {
            base.OnAuthenticationError(errorCode, errorMessage);
            completion.TrySetResult(false);
        }
    }

    private sealed class CancelListener(TaskCompletionSource<bool> completion)
        : Java.Lang.Object, IDialogInterfaceOnClickListener
    {
        public void OnClick(IDialogInterface? dialog, int which) => completion.TrySetResult(false);
    }
}