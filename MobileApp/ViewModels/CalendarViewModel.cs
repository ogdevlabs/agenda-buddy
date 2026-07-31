using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Models;
using MobileApp.Services;

namespace MobileApp.ViewModels;

public partial class CalendarViewModel : ObservableObject
{
    private readonly ICalendarApiService _calendarApiService;

    [ObservableProperty]
    private List<CalendarDaySummary> _days = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public CalendarViewModel(ICalendarApiService calendarApiService)
    {
        _calendarApiService = calendarApiService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            Days = await _calendarApiService.GetAvailabilityAsync();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Could not load calendar — check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
