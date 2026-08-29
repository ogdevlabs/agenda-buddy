using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IBookingApiService _bookingApiService;
    private readonly IUserSessionService _session;
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

    // The provider report route is provider-only; a Customer sees session history instead.
    public bool IsProvider => _session.IsProvider;
    public bool IsCustomer => _session.IsCustomer;

    public string SectionTitle => IsCustomer ? "Recent Sessions" : "Appointments";
    public string PrimaryStatLabel => IsCustomer ? "Completed" : "Today";
    public string PrimaryStatCaption => IsCustomer ? "" : "Sessions";
    public string SecondaryStatLabel => IsCustomer ? "Cancelled" : "This Week";
    public string SecondaryStatCaption => IsCustomer ? "" : "Upcoming";
    public string EmptyStateTitle => IsCustomer ? "No Sessions Yet" : "No Appointments";
    public string EmptyStateSubtitle => IsCustomer
        ? "Your completed and cancelled sessions will show up here."
        : "Check your calendar for upcoming sessions.";

    public event EventHandler? AppointmentsLoaded;

    public DashboardViewModel(IBookingApiService bookingApiService, IUserSessionService session)
    {
        _bookingApiService = bookingApiService;
        _session = session;
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

        await _session.RefreshAsync();
        OnPropertyChanged(nameof(IsProvider));
        OnPropertyChanged(nameof(IsCustomer));
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(PrimaryStatLabel));
        OnPropertyChanged(nameof(PrimaryStatCaption));
        OnPropertyChanged(nameof(SecondaryStatLabel));
        OnPropertyChanged(nameof(SecondaryStatCaption));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));

        Greeting = DateTime.Now.Hour switch
        {
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            _ => "Good evening"
        };

        try
        {
            var results = IsCustomer
                ? await _bookingApiService.GetPastAppointmentsAsync()
                : await _bookingApiService.GetTodayAppointmentsAsync();

            _allAppointments = results;
            _pageIndex = 0;
            UpdatePage();

            if (IsCustomer)
            {
                TodayCount = results.Count(a => a.Status == AgendaBuddy.Library.Entities.AppointmentStatus.Completed);
                WeekCount = results.Count(a => a.Status == AgendaBuddy.Library.Entities.AppointmentStatus.Cancelled);
            }
            else
            {
                TodayCount = results.Count(a => a.ScheduledAt.Date == DateTime.Today);
                WeekCount = results.Count;
            }

            AppointmentsLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            // Real failure (network, timeout, malformed response, ambiguous write, etc.) — surface it
            // through the error banner rather than masking it with fabricated data.
            ErrorMessage = "Could not load appointments. Check your connection and try again.";
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
