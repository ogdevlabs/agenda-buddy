using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

public partial class EmailVerificationViewModel(IAuthService authService) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResendCommand))]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _token = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isVerified;

    [ObservableProperty]
    private string _statusMessage = AppResources.EmailVerification_Pending;

    public bool ShowPendingActions => !IsVerified;

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (string.IsNullOrWhiteSpace(Token)) return;

        IsLoading = true;
        StatusMessage = AppResources.EmailVerification_Checking;
        try
        {
            IsVerified = await authService.ConfirmEmailAsync(Token);
            StatusMessage = IsVerified
                ? AppResources.EmailVerification_Verified
                : AppResources.EmailVerification_Invalid;
        }
        catch (HttpRequestException)
        {
            StatusMessage = AppResources.EmailVerification_VerifyUnavailable;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanResend))]
    private async Task ResendAsync()
    {
        IsLoading = true;
        try
        {
            var sent = await authService.RequestEmailVerificationAsync(Email.Trim());
            StatusMessage = sent
                ? AppResources.EmailVerification_Resent
                : AppResources.EmailVerification_ResendFailed;
        }
        catch (HttpRequestException)
        {
            StatusMessage = AppResources.EmailVerification_ResendUnavailable;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanResend() => !string.IsNullOrWhiteSpace(Email) && !IsLoading;

    partial void OnIsVerifiedChanged(bool value) => OnPropertyChanged(nameof(ShowPendingActions));
}