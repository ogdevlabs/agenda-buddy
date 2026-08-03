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
    private const int PageSize = 7;

    [ObservableProperty]
    private List<CalendarDaySummary> _days = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _weekLabel = string.Empty;

    [ObservableProperty]
    private string _monthYear = string.Empty;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedDay))]
    [NotifyPropertyChangedFor(nameof(HasAvailableSlots))]
    [NotifyPropertyChangedFor(nameof(SelectedDayIsEmpty))]
    private CalendarDaySummary? _selectedDay;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasSelectedDay => SelectedDay is not null;
    public bool HasAvailableSlots => SelectedDay?.AvailableSlots.Count > 0;
    public bool SelectedDayIsEmpty => SelectedDay is not null
                                     && !SelectedDay.HasBookings
                                     && SelectedDay.AvailableSlots.Count == 0;

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
        }
        catch (HttpRequestException)
        {
            _allDays = GenerateSeedWeek();
            _pageIndex = 0;
            UpdatePage();
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
        // Clear selection state on all days
        foreach (var d in _allDays)
            d.IsSelected = false;

        Days = _allDays.Skip(_pageIndex * PageSize).Take(PageSize).ToList();
        CanGoBack = _pageIndex > 0;
        CanGoForward = (_pageIndex + 1) * PageSize < _allDays.Count;

        // Update header labels
        var startDate = DateTime.Today.AddDays(_pageIndex * PageSize);
        var endDate = startDate.AddDays(Days.Count - 1);
        MonthYear = startDate.ToString("MMMM yyyy");
        WeekLabel = $"{startDate:MMM d} — {endDate:MMM d}";

        // Auto-select today or first day
        var today = Days.FirstOrDefault(d => d.IsToday) ?? Days.FirstOrDefault();
        if (today is not null)
        {
            today.IsSelected = true;
            SelectedDay = today;
        }
    }

    partial void OnSelectedDayChanged(CalendarDaySummary? value)
    {
        // Deselect all, then select the new one
        foreach (var d in Days)
            d.IsSelected = d == value;
    }

    private List<CalendarDaySummary> GenerateSeedWeek()
    {
        var today = DateTime.Today;
        var days = new List<CalendarDaySummary>();
        var email = _session.Email;

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
