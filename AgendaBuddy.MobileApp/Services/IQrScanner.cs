namespace AgendaBuddy.MobileApp.Services;

public enum CameraAccess
{
    Granted,
    Denied,

    /// <summary>No camera, or no barcode reader on this build. Typing the code is the only way in.</summary>
    Unavailable
}

/// <summary>
/// The camera side of scanning a provider's code: whether it can run, and the permission it needs. The live
/// preview itself is a view; this is what a view model can ask without one.
/// </summary>
public interface IQrScanner
{
    Task<CameraAccess> EnsureCameraAccessAsync();

    /// <summary>Opens this app's page in the system settings, where a denied camera can be allowed.</summary>
    void OpenAppSettings();
}

public sealed class UnavailableQrScanner : IQrScanner
{
    public Task<CameraAccess> EnsureCameraAccessAsync() => Task.FromResult(CameraAccess.Unavailable);

    public void OpenAppSettings()
    {
    }
}
