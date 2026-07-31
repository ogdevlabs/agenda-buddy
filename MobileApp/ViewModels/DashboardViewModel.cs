using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Models;
using MobileApp.Services;

namespace MobileApp.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IBookingApiService _bookingApiService;

    [ObservableProperty]
    private List<AppointmentSummary> _appointments = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsEmpty => !IsLoading && Appointments.Count == 0 && !HasError;

    public event EventHandler? AppointmentsLoaded;

    public DashboardViewModel(IBookingApiService bookingApiService)
    {
        _bookingApiService = bookingApiService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var results = await _bookingApiService.GetTodayAppointmentsAsync();
            Appointments = results;
            AppointmentsLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Could not load appointments — check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnAppointmentsChanged(List<AppointmentSummary> value) => OnPropertyChanged(nameof(IsEmpty));
}
