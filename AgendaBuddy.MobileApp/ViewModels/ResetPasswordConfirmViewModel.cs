using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary><c>POST /api/v1/auth/password-reset/confirm</c>.</summary>
public partial class ResetPasswordConfirmViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _token = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public event EventHandler? ResetSucceeded;

    public ResetPasswordConfirmViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        if (NewPassword.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var succeeded = await _authService.ConfirmPasswordResetAsync(Email, Token, NewPassword);
            if (succeeded)
                ResetSucceeded?.Invoke(this, EventArgs.Empty);
            else
                ErrorMessage = "That reset link is invalid or has expired.";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Could not reach the server. Check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanConfirm() =>
        !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(NewPassword);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
