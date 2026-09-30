using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;
using AgendaBuddy.MobileApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgendaBuddy.MobileApp.ViewModels;

public sealed record ShowcaseResolvedEventArgs(ShowcaseView View, ShowcaseSource Source);

/// <summary>
/// Opening a provider's showcase from their code: scanned with the camera, or typed. The camera is optional —
/// typing is always offered, because a denied permission, a missing camera or a code read aloud must not be a
/// dead end.
/// </summary>
public partial class ScanProviderViewModel : ObservableObject
{
    private readonly IShowcaseApiService _api;
    private readonly IQrScanner _scanner;
    private string _lastRejectedScan = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCameraReady), nameof(IsCameraDenied), nameof(IsCameraUnavailable))]
    private CameraAccess? _camera;

    [ObservableProperty]
    private bool _isDetecting;

    [ObservableProperty]
    private string _typedCode = string.Empty;

    [ObservableProperty]
    private bool _isTyping;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isResolving;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScanMessage))]
    private string _scanMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    public ScanProviderViewModel(IShowcaseApiService api, IQrScanner scanner)
    {
        _api = api;
        _scanner = scanner;
    }

    public event EventHandler<ShowcaseResolvedEventArgs>? ShowcaseResolved;

    public bool IsCameraReady => Camera == CameraAccess.Granted;
    public bool IsCameraDenied => Camera == CameraAccess.Denied;
    public bool IsCameraUnavailable => Camera == CameraAccess.Unavailable;
    public bool IsIdle => !IsResolving;
    public bool HasScanMessage => !string.IsNullOrEmpty(ScanMessage);
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand]
    private async Task StartAsync()
    {
        ErrorMessage = string.Empty;
        ScanMessage = string.Empty;
        _lastRejectedScan = string.Empty;
        try
        {
            Camera = await _scanner.EnsureCameraAccessAsync();
        }
        catch (Exception)
        {
            Camera = CameraAccess.Unavailable;
        }

        IsDetecting = Camera == CameraAccess.Granted && !IsTyping;
        if (Camera != CameraAccess.Granted)
            IsTyping = true;
    }

    [RelayCommand]
    private void Stop() => IsDetecting = false;

    [RelayCommand]
    private void OpenSettings() => _scanner.OpenAppSettings();

    [RelayCommand]
    private void ShowTyping()
    {
        IsTyping = true;
        IsDetecting = false;
    }

    [RelayCommand]
    private void ShowCamera()
    {
        IsTyping = false;
        ErrorMessage = string.Empty;
        IsDetecting = IsCameraReady;
    }

    /// <summary>
    /// A payload the camera read. A foreign QR says so once, not once per frame; reading it again after something
    /// else was scanned says so again.
    /// </summary>
    public async Task HandleScannedAsync(string? payload)
    {
        if (IsResolving || string.IsNullOrWhiteSpace(payload))
            return;

        if (!ShowcaseCodeParser.TryParseScan(payload, out var code))
        {
            if (!string.Equals(payload, _lastRejectedScan, StringComparison.Ordinal))
            {
                _lastRejectedScan = payload;
                ScanMessage = AppResources.GetString("Scan_NotAnAgendaMeCode");
            }

            return;
        }

        _lastRejectedScan = string.Empty;
        ScanMessage = string.Empty;
        IsDetecting = false;
        var opened = await ResolveAsync(code, ShowcaseSource.Scan);
        if (!opened)
            IsDetecting = IsCameraReady && !IsTyping;
    }

    [RelayCommand]
    private async Task SubmitCodeAsync()
    {
        if (IsResolving)
            return;

        ErrorMessage = string.Empty;
        if (!ShowcaseCodeParser.TryParse(TypedCode, out var code))
        {
            ErrorMessage = AppResources.GetString("Scan_InvalidCode");
            return;
        }

        await ResolveAsync(code, ShowcaseSource.Code);
    }

    private async Task<bool> ResolveAsync(string code, ShowcaseSource source)
    {
        IsResolving = true;
        try
        {
            var result = await _api.GetShowcaseByCodeAsync(code, source);
            if (!result.IsSuccess || result.Value is null)
            {
                var message = result.ErrorCode == ShowcaseErrorCodes.ShowcaseNotFound
                    ? AppResources.GetString("Scan_CodeNotFound")
                    : ShowcaseErrorCopy.Describe(result);
                if (source == ShowcaseSource.Scan)
                    ScanMessage = message;
                else
                    ErrorMessage = message;
                return false;
            }

            ShowcaseResolved?.Invoke(this, new ShowcaseResolvedEventArgs(result.Value, source));
            return true;
        }
        finally
        {
            IsResolving = false;
        }
    }
}
