using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

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

            _allDays = result;
            _pageIndex = 0;
            UpdatePage();
        }
        catch (Exception)
        {
            // Real failure (network, timeout, malformed response, ambiguous write, etc.) — surface it
            // through ErrorMessage rather than masking it with fabricated data.
            ErrorMessage = "Could not load the calendar. Check your connection and try again.";
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

    [RelayCommand]
    private void ToggleDay(CalendarDaySummary day)
    {
        day.IsExpanded = !day.IsExpanded;
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
