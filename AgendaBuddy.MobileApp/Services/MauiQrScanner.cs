#if MOBILE
namespace AgendaBuddy.MobileApp.Services;

/// <inheritdoc cref="IQrScanner"/>
public sealed class MauiQrScanner : IQrScanner
{
    public async Task<CameraAccess> EnsureCameraAccessAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
                return CameraAccess.Unavailable;

            var status = await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var current = await Permissions.CheckStatusAsync<Permissions.Camera>();
                return current == PermissionStatus.Granted
                    ? current
                    : await Permissions.RequestAsync<Permissions.Camera>();
            });

            return status == PermissionStatus.Granted ? CameraAccess.Granted : CameraAccess.Denied;
        }
        catch (Exception)
        {
            return CameraAccess.Unavailable;
        }
    }

    public void OpenAppSettings()
    {
        try
        {
            AppInfo.Current.ShowSettingsUI();
        }
        catch (Exception)
        {
            // Nothing more can be offered; the screen still has "Type a code instead".
        }
    }
}
#endif
