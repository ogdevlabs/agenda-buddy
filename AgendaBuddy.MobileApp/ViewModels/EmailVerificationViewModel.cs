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
    [NotifyCanExecuteChangedFor(nameof(ConfirmCodeCommand))]
    private string _code = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCodeCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResendCommand))]
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
            if (IsVerified)
                VerificationSucceeded?.Invoke();
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

    [RelayCommand(CanExecute = nameof(CanConfirmCode))]
    private async Task ConfirmCodeAsync()
    {
        IsLoading = true;
        StatusMessage = AppResources.EmailVerification_CheckingCode;
        try
        {
            IsVerified = await authService.ConfirmEmailCodeAsync(Email.Trim(), Code);
            StatusMessage = IsVerified
                ? AppResources.EmailVerification_Verified
                : AppResources.EmailVerification_InvalidCode;
            if (IsVerified)
                VerificationSucceeded?.Invoke();
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

    private bool CanConfirmCode() =>
        !string.IsNullOrWhiteSpace(Email)
        && Code.Length == 6
        && Code.All(char.IsAsciiDigit)
        && !IsLoading;

    public event Action? VerificationSucceeded;

    partial void OnIsVerifiedChanged(bool value) => OnPropertyChanged(nameof(ShowPendingActions));
}