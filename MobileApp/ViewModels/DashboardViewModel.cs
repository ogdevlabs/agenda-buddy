using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Entities;
using MobileApp.Models;
using MobileApp.Services;

namespace MobileApp.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IBookingApiService _bookingApiService;
    private List<AppointmentSummary> _allAppointments = new();
    private int _pageIndex;
    private const int PageSize = 4;

    [ObservableProperty]
    private List<AppointmentSummary> _appointments = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private int _todayCount;

    [ObservableProperty]
    private int _weekCount;

    [ObservableProperty]
    private string _greeting = string.Empty;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    [ObservableProperty]
    private string _pageLabel = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsEmpty => !IsLoading && Appointments.Count == 0 && !HasError;

    public event EventHandler? AppointmentsLoaded;

    public DashboardViewModel(IBookingApiService bookingApiService)
    {
        _bookingApiService = bookingApiService;
        Greeting = DateTime.Now.Hour switch
        {
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            _ => "Good evening"
        };
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var results = await _bookingApiService.GetTodayAppointmentsAsync();

            if (results.Count == 0)
                results = GenerateSeedAppointments();

            _allAppointments = results;
            _pageIndex = 0;
            UpdatePage();
            TodayCount = results.Count(a => a.ScheduledAt.Date == DateTime.Today);
            WeekCount = results.Count;
            AppointmentsLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (HttpRequestException)
        {
            var seed = GenerateSeedAppointments();
            _allAppointments = seed;
            _pageIndex = 0;
            UpdatePage();
            TodayCount = seed.Count(a => a.ScheduledAt.Date == DateTime.Today);
            WeekCount = seed.Count;
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
        Appointments = _allAppointments.Skip(_pageIndex * PageSize).Take(PageSize).ToList();
        CanGoBack = _pageIndex > 0;
        CanGoForward = (_pageIndex + 1) * PageSize < _allAppointments.Count;
        var totalPages = (int)Math.Ceiling((double)_allAppointments.Count / PageSize);
        PageLabel = totalPages > 1 ? $"{_pageIndex + 1} / {totalPages}" : "";
    }

    private static List<AppointmentSummary> GenerateSeedAppointments()
    {
        var today = DateTime.Today;
        return
        [
            new AppointmentSummary
            {
                Id = "seed-1",
                CustomerEmail = "alex.chen@agendabuddy.dev",
                CustomerName = "Alex Chen",
                CustomerPhone = "+1 (415) 555-0142",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev",
                ScheduledAt = today.AddHours(9),
                Status = AppointmentStatus.Confirmed,
                ServiceName = "Personal Training",
                CustomerNotes = "Focus on upper body today, shoulder has been tight"
            },
            new AppointmentSummary
            {
                Id = "seed-2",
                CustomerEmail = "priya.sharma@agendabuddy.dev",
                CustomerName = "Priya Sharma",
                CustomerPhone = "+1 (628) 555-0198",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev",
                ScheduledAt = today.AddHours(10),
                Status = AppointmentStatus.Confirmed,
                ServiceName = "Yoga Session",
                CustomerNotes = "Beginner level, working on flexibility"
            },
            new AppointmentSummary
            {
                Id = "seed-3",
                CustomerEmail = "david.thompson@agendabuddy.dev",
                CustomerName = "David Thompson",
                CustomerPhone = "+1 (510) 555-0267",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev",
                ScheduledAt = today.AddHours(14),
                Status = AppointmentStatus.Requested,
                ServiceName = "HIIT Coaching",
                CustomerNotes = "First session — wants to discuss goals"
            },
            new AppointmentSummary
            {
                Id = "seed-4",
                CustomerEmail = "priya.sharma@agendabuddy.dev",
                CustomerName = "Priya Sharma",
                CustomerPhone = "+1 (628) 555-0198",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev",
                ScheduledAt = today.AddHours(15).AddMinutes(30),
                Status = AppointmentStatus.Confirmed,
                ServiceName = "Meditation",
                CustomerNotes = ""
            },
            new AppointmentSummary
            {
                Id = "seed-5",
                CustomerEmail = "alex.chen@agendabuddy.dev",
                CustomerName = "Alex Chen",
                CustomerPhone = "+1 (415) 555-0142",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev",
                ScheduledAt = today.AddDays(1).AddHours(9),
                Status = AppointmentStatus.Confirmed,
                ServiceName = "Personal Training",
                CustomerNotes = "Leg day, bring knee brace"
            },
            new AppointmentSummary
            {
                Id = "seed-6",
                CustomerEmail = "david.thompson@agendabuddy.dev",
                CustomerName = "David Thompson",
                CustomerPhone = "+1 (510) 555-0267",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev",
                ScheduledAt = today.AddDays(1).AddHours(11),
                Status = AppointmentStatus.Requested,
                ServiceName = "HIIT Coaching",
                CustomerNotes = "Can we do outdoor if weather is good?"
            },
            new AppointmentSummary
            {
                Id = "seed-7",
                CustomerEmail = "priya.sharma@agendabuddy.dev",
                CustomerName = "Priya Sharma",
                CustomerPhone = "+1 (628) 555-0198",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev",
                ScheduledAt = today.AddDays(2).AddHours(10),
                Status = AppointmentStatus.Confirmed,
                ServiceName = "Yoga Session",
                CustomerNotes = "Wants to try hot yoga format"
            }
        ];
    }

    [RelayCommand]
    private void ToggleAppointment(AppointmentSummary item)
    {
        item.IsExpanded = !item.IsExpanded;
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnAppointmentsChanged(List<AppointmentSummary> value) => OnPropertyChanged(nameof(IsEmpty));
}
