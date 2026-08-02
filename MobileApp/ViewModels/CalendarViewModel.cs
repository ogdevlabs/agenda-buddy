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

    [ObservableProperty]
    private string _weekLabel = string.Empty;

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
            var result = await _calendarApiService.GetAvailabilityAsync(7);

            if (result.Count == 0)
                result = GenerateSeedWeek();

            Days = result;

            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(6);
            WeekLabel = $"{startDate:MMM d} — {endDate:MMM d, yyyy}";
        }
        catch (HttpRequestException)
        {
            Days = GenerateSeedWeek();
            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(6);
            WeekLabel = $"{startDate:MMM d} — {endDate:MMM d, yyyy}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static List<CalendarDaySummary> GenerateSeedWeek()
    {
        var today = DateTime.Today;
        var days = new List<CalendarDaySummary>();

        string[][] seedBookings =
        [
            ["9:00 AM — Alex Chen", "10:00 AM — Priya Sharma", "2:00 PM — David Thompson"],
            ["8:30 AM — Priya Sharma", "11:00 AM — Alex Chen"],
            ["9:30 AM — David Thompson", "1:00 PM — Alex Chen", "3:30 PM — Priya Sharma", "5:00 PM — David Thompson"],
            ["10:00 AM — Priya Sharma"],
            ["9:00 AM — Alex Chen", "11:30 AM — David Thompson", "4:00 PM — Priya Sharma"],
            [],
            []
        ];

        string[][] seedAvailable =
        [
            ["10:30 AM", "11:00 AM", "3:00 PM", "4:00 PM"],
            ["9:00 AM", "9:30 AM", "10:00 AM", "1:00 PM", "2:00 PM", "3:00 PM"],
            ["10:00 AM", "2:00 PM"],
            ["8:30 AM", "9:00 AM", "9:30 AM", "11:00 AM", "1:00 PM", "2:00 PM", "3:00 PM", "4:00 PM"],
            ["10:00 AM", "2:00 PM", "3:00 PM"],
            ["9:00 AM", "9:30 AM", "10:00 AM", "10:30 AM", "11:00 AM", "1:00 PM", "2:00 PM", "3:00 PM", "4:00 PM"],
            ["9:00 AM", "9:30 AM", "10:00 AM", "10:30 AM", "11:00 AM", "1:00 PM", "2:00 PM", "3:00 PM", "4:00 PM"]
        ];

        for (var i = 0; i < 7; i++)
        {
            var date = today.AddDays(i);
            days.Add(new CalendarDaySummary
            {
                Date = date.ToString("yyyy-MM-dd"),
                BookedSlots = seedBookings[i].ToList(),
                AvailableSlots = seedAvailable[i].ToList()
            });
        }

        return days;
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
