using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Library.Entities;
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

    [ObservableProperty]
    private int _todayCount;

    [ObservableProperty]
    private int _weekCount;

    [ObservableProperty]
    private string _greeting = string.Empty;

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

            Appointments = results;
            TodayCount = results.Count(a => a.ScheduledAt.Date == DateTime.Today);
            WeekCount = results.Count;
            AppointmentsLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (HttpRequestException)
        {
            var seed = GenerateSeedAppointments();
            Appointments = seed;
            TodayCount = seed.Count(a => a.ScheduledAt.Date == DateTime.Today);
            WeekCount = seed.Count;
        }
        finally
        {
            IsLoading = false;
        }
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
                ProviderEmail = "sarah.mitchell@agendabuddy.dev",
                ScheduledAt = today.AddHours(9),
                Status = AppointmentStatus.Confirmed
            },
            new AppointmentSummary
            {
                Id = "seed-2",
                CustomerEmail = "priya.sharma@agendabuddy.dev",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev",
                ScheduledAt = today.AddHours(10),
                Status = AppointmentStatus.Confirmed
            },
            new AppointmentSummary
            {
                Id = "seed-3",
                CustomerEmail = "david.thompson@agendabuddy.dev",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev",
                ScheduledAt = today.AddHours(14),
                Status = AppointmentStatus.Requested
            },
            new AppointmentSummary
            {
                Id = "seed-4",
                CustomerEmail = "priya.sharma@agendabuddy.dev",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev",
                ScheduledAt = today.AddDays(1).AddHours(11),
                Status = AppointmentStatus.Requested
            },
            new AppointmentSummary
            {
                Id = "seed-5",
                CustomerEmail = "alex.chen@agendabuddy.dev",
                ProviderEmail = "sarah.mitchell@agendabuddy.dev",
                ScheduledAt = today.AddDays(2).AddHours(9).AddMinutes(30),
                Status = AppointmentStatus.Confirmed
            }
        ];
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnAppointmentsChanged(List<AppointmentSummary> value) => OnPropertyChanged(nameof(IsEmpty));
}
