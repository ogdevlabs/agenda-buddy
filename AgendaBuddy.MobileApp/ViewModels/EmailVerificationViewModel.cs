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
    private string _statusMessage = "Check your inbox and open the confirmation link.";

    public bool ShowPendingActions => !IsVerified;

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (string.IsNullOrWhiteSpace(Token)) return;

        IsLoading = true;
        StatusMessage = "Checking your confirmation link...";
        try
        {
            IsVerified = await authService.ConfirmEmailAsync(Token);
            StatusMessage = IsVerified
                ? "Email verified. Sign in to continue."
                : "This confirmation link is invalid or has expired.";
        }
        catch (HttpRequestException)
        {
            StatusMessage = "We could not verify your email. Check your connection and try again.";
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
                ? "If this account is awaiting verification, a new link has been sent."
                : "We could not send a new link. Try again shortly.";
        }
        catch (HttpRequestException)
        {
            StatusMessage = "We could not send a new link. Check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanResend() => !string.IsNullOrWhiteSpace(Email) && !IsLoading;

    partial void OnIsVerifiedChanged(bool value) => OnPropertyChanged(nameof(ShowPendingActions));
}