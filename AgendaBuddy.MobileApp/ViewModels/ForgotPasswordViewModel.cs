using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// <c>POST /api/v1/auth/password-reset/request</c>. ADR-052: no real email/SMS provider exists yet — the
/// reset token is only logged server-side, never delivered. This screen exists so the capability is
/// reachable and testable end-to-end; the token still has to come from the AppHost console log until a
/// notification provider ships.
/// </summary>
public partial class ForgotPasswordViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string _email = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSubmitted;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public ForgotPasswordViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var succeeded = await _authService.RequestPasswordResetAsync(Email);
            // Anti-enumeration: the route answers 202 whether or not the address matched an account, so a
            // false result here means a transport/5xx failure, not "no such account" — the copy stays the
            // same either way.
            IsSubmitted = succeeded;
            if (!succeeded)
                ErrorMessage = "Could not reach the server. Check your connection and try again.";
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

    private bool CanSubmit() => !string.IsNullOrWhiteSpace(Email);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
