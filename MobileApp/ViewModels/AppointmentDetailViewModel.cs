using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Entities;
using MobileApp.Models;
using MobileApp.Services;

namespace MobileApp.ViewModels;

public enum ActionType
{
    Confirm,
    Cancel,
    Complete
}

public class AppointmentActionEventArgs : EventArgs
{
    public ActionType Action { get; }
    public AppointmentActionEventArgs(ActionType action) => Action = action;
}

public partial class AppointmentDetailViewModel : ObservableObject
{
    private readonly IBookingApiService _bookingApiService;
    private readonly IUserSessionService _session;

    [ObservableProperty] private AppointmentDetail? _appointment;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isConfirmEnabled = true;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsNotLoading => !IsLoading;
    public bool HasAppointment => Appointment is not null;

    // ux-review.md finding 3 / PRD requirement 6 / AC7: the customer-facing "mark complete" control must be
    // HIDDEN entirely, not disabled — a disabled button with no explanation invites "why can't I do this?"
    // Bound to the Complete button's IsVisible (not IsEnabled) in AppointmentDetailPage.xaml, and gates the
    // command's CanExecute below so the action is genuinely unavailable, not merely invisible.
    public bool ShowCompleteButton => _session.IsProvider;

    public string AppointmentId { get; set; } = string.Empty;

    public event EventHandler<AppointmentActionEventArgs>? ActionRequested;

    public AppointmentDetailViewModel(IBookingApiService bookingApiService, IUserSessionService session)
    {
        _bookingApiService = bookingApiService;
        _session = session;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _bookingApiService.GetAppointmentAsync(AppointmentId);
            if (result is null)
            {
                ErrorMessage = "Could not load appointment — try again.";
            }
            else
            {
                Appointment = result;
            }
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Could not load appointment — check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadWithFallbackAsync(AppointmentDetail? fallback)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _bookingApiService.GetAppointmentAsync(AppointmentId);
            Appointment = result ?? fallback;
        }
        catch (HttpRequestException)
        {
            Appointment = fallback;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Confirm() =>
        ActionRequested?.Invoke(this, new AppointmentActionEventArgs(ActionType.Confirm));

    [RelayCommand]
    private void Cancel() =>
        ActionRequested?.Invoke(this, new AppointmentActionEventArgs(ActionType.Cancel));

    [RelayCommand(CanExecute = nameof(ShowCompleteButton))]
    private void Complete() =>
        ActionRequested?.Invoke(this, new AppointmentActionEventArgs(ActionType.Complete));

    public async Task ExecuteStatusUpdateAsync(AppointmentStatus status)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var updated = await _bookingApiService.UpdateStatusAsync(AppointmentId, status);
            if (updated is null)
            {
                // T-003: API returned non-success (e.g., 400 for invalid status).
                ErrorMessage = "Status update failed";
            }
            else
            {
                Appointment = updated;
            }
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Status update failed — check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));

    partial void OnAppointmentChanged(AppointmentDetail? value) => OnPropertyChanged(nameof(HasAppointment));
}
