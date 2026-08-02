using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileApp.Models;
using MobileApp.Services;

namespace MobileApp.ViewModels;

public partial class CalendarViewModel : ObservableObject
{
    private readonly ICalendarApiService _calendarApiService;
    private readonly IUserSessionService _session;
    private List<CalendarDaySummary> _allDays = new();
    private int _pageIndex;
    private const int PageSize = 4;

    [ObservableProperty]
    private List<CalendarDaySummary> _days = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _weekLabel = string.Empty;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public CalendarViewModel(ICalendarApiService calendarApiService, IUserSessionService session)
    {
        _calendarApiService = calendarApiService;
        _session = session;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        await _session.RefreshAsync();

        try
        {
            var result = await _calendarApiService.GetAvailabilityAsync(7);

            if (result.Count == 0)
                result = GenerateSeedWeek();

            _allDays = result;
            _pageIndex = 0;
            UpdatePage();

            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(6);
            WeekLabel = $"{startDate:MMM d} — {endDate:MMM d, yyyy}";
        }
        catch (HttpRequestException)
        {
            _allDays = GenerateSeedWeek();
            _pageIndex = 0;
            UpdatePage();

            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(6);
            WeekLabel = $"{startDate:MMM d} — {endDate:MMM d, yyyy}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (!CanGoForward) return;
        _pageIndex++;
        UpdatePage();
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (!CanGoBack) return;
        _pageIndex--;
        UpdatePage();
    }

    private void UpdatePage()
    {
        Days = _allDays.Skip(_pageIndex * PageSize).Take(PageSize).ToList();
        CanGoBack = _pageIndex > 0;
        CanGoForward = (_pageIndex + 1) * PageSize < _allDays.Count;
    }

    private List<CalendarDaySummary> GenerateSeedWeek()
    {
        var today = DateTime.Today;
        var days = new List<CalendarDaySummary>();
        var email = _session.Email;

        // Full provider schedule (Sarah Mitchell's view)
        string[][] providerBookings =
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

        // For customers, filter bookings to only their sessions
        var customerName = email switch
        {
            "alex.chen@agendabuddy.dev" => "Alex Chen",
            "priya.sharma@agendabuddy.dev" => "Priya Sharma",
            "david.thompson@agendabuddy.dev" => "David Thompson",
            _ => ""
        };

        for (var i = 0; i < 7; i++)
        {
            var date = today.AddDays(i);
            var booked = providerBookings[i].ToList();

            if (_session.IsCustomer && !string.IsNullOrEmpty(customerName))
                booked = booked.Where(s => s.Contains(customerName)).ToList();

            days.Add(new CalendarDaySummary
            {
                Date = date.ToString("yyyy-MM-dd"),
                BookedSlots = booked,
                AvailableSlots = _session.IsProvider ? seedAvailable[i].ToList() : []
            });
        }

        return days;
    }

    [RelayCommand]
    private void ToggleDay(CalendarDaySummary day)
    {
        day.IsExpanded = !day.IsExpanded;
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
