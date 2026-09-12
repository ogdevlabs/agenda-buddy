using AgendaBuddy.Library.Dtos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

public enum AppointmentListTab
{
    Scheduled,
    Done,
    Cancelled
}

public partial class CalendarViewModel : ObservableObject
{
    private readonly ICalendarApiService _calendarApiService;
    private readonly IUserSessionService _session;
    private AppointmentPage _scheduledPage = AppointmentPage.Empty(1, PageRequest.MaxPageSize);
    private AppointmentPage _donePage = AppointmentPage.Empty(1, HistoryPageSize);
    private AppointmentPage _cancelledPage = AppointmentPage.Empty(1, HistoryPageSize);
    public const int HistoryPageSize = 5;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDisplayedAppointments))]
    [NotifyPropertyChangedFor(nameof(AppointmentListIsEmpty))]
    private List<AppointmentDetail> _displayedAppointments = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScheduledTab))]
    [NotifyPropertyChangedFor(nameof(IsDoneTab))]
    [NotifyPropertyChangedFor(nameof(IsCancelledTab))]
    [NotifyPropertyChangedFor(nameof(EmptyTitle))]
    [NotifyPropertyChangedFor(nameof(EmptySubtitle))]
    [NotifyPropertyChangedFor(nameof(HistoryPaginationIsVisible))]
    private AppointmentListTab _selectedAppointmentTab = AppointmentListTab.Scheduled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HistoryPageLabel))]
    [NotifyPropertyChangedFor(nameof(CanGoBackInHistory))]
    [NotifyPropertyChangedFor(nameof(CanGoForwardInHistory))]
    private int _historyPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HistoryPageLabel))]
    [NotifyPropertyChangedFor(nameof(CanGoForwardInHistory))]
    private long _selectedHistoryTotal;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Only a provider has working hours to configure, so only they see the settings affordance.</summary>
    public bool IsProvider => _session.IsProvider;

    public bool IsScheduledTab => SelectedAppointmentTab == AppointmentListTab.Scheduled;
    public bool IsDoneTab => SelectedAppointmentTab == AppointmentListTab.Done;
    public bool IsCancelledTab => SelectedAppointmentTab == AppointmentListTab.Cancelled;
    public long ScheduledCount => _scheduledPage.TotalCount;
    public long DoneCount => _donePage.TotalCount;
    public long CancelledCount => _cancelledPage.TotalCount;
    public bool HistoryPaginationIsVisible => !IsScheduledTab && SelectedHistoryTotal > HistoryPageSize;
    public bool CanGoBackInHistory => !IsScheduledTab && HistoryPage > 1;
    public bool CanGoForwardInHistory => !IsScheduledTab && HistoryPage * HistoryPageSize < SelectedHistoryTotal;
    public string HistoryPageLabel
    {
        get
        {
            if (!HistoryPaginationIsVisible) return string.Empty;
            var pages = (int)Math.Ceiling((double)SelectedHistoryTotal / HistoryPageSize);
            return $"{HistoryPage} / {pages}";
        }
    }
    public bool HasDisplayedAppointments => DisplayedAppointments.Count > 0;
    public bool AppointmentListIsEmpty => !IsLoading && !HasError && !HasDisplayedAppointments;
    public string EmptyTitle => AppResources.GetString(SelectedAppointmentTab switch
    {
        AppointmentListTab.Scheduled => "Appointments_EmptyScheduledTitle",
        AppointmentListTab.Done => "Appointments_EmptyDoneTitle",
        _ => "Appointments_EmptyCancelledTitle"
    });
    public string EmptySubtitle => AppResources.GetString(SelectedAppointmentTab switch
    {
        AppointmentListTab.Scheduled => "Appointments_EmptyScheduledSubtitle",
        AppointmentListTab.Done => "Appointments_EmptyDoneSubtitle",
        _ => "Appointments_EmptyCancelledSubtitle"
    });

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
        OnPropertyChanged(nameof(IsProvider));

        try
        {
            var appointmentsTask = _calendarApiService.GetAppointmentsPageAsync(
                AppointmentPageSegment.Scheduled, 1, PageRequest.MaxPageSize);
            var doneTask = _calendarApiService.GetAppointmentsPageAsync(
                AppointmentPageSegment.Done, 1, HistoryPageSize);
            var cancelledTask = _calendarApiService.GetAppointmentsPageAsync(
                AppointmentPageSegment.Cancelled, 1, HistoryPageSize);

            await Task.WhenAll(appointmentsTask, doneTask, cancelledTask);

            _scheduledPage = appointmentsTask.Result;
            _donePage = doneTask.Result;
            _cancelledPage = cancelledTask.Result;
            HistoryPage = 1;
            OnPropertyChanged(nameof(ScheduledCount));
            OnPropertyChanged(nameof(DoneCount));
            OnPropertyChanged(nameof(CancelledCount));
            ApplyAppointmentTab();
        }
        catch (Exception)
        {
            // Real failure (network, timeout, malformed response, ambiguous write, etc.) — surface it
            // through ErrorMessage rather than masking it with fabricated data.
            ErrorMessage = AppResources.GetString("Error_LoadAppointments");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SelectAppointmentTabAsync(string tab)
    {
        if (!Enum.TryParse<AppointmentListTab>(tab, ignoreCase: true, out var parsed)) return;

        SelectedAppointmentTab = parsed;
        if (!IsScheduledTab)
            await LoadHistoryPageAsync(1);
        else
        {
            HistoryPage = 1;
            ApplyAppointmentTab();
        }
    }

    partial void OnSelectedAppointmentTabChanged(AppointmentListTab value) => ApplyAppointmentTab();

    private void ApplyAppointmentTab()
    {
        var page = SelectedAppointmentTab == AppointmentListTab.Done ? _donePage : _cancelledPage;
        DisplayedAppointments = IsScheduledTab
            ? _scheduledPage.Items
            : page.Items;
        SelectedHistoryTotal = IsScheduledTab ? 0 : page.TotalCount;
    }

    [RelayCommand]
    private async Task NextHistoryPageAsync()
    {
        if (!CanGoForwardInHistory) return;
        await LoadHistoryPageAsync(HistoryPage + 1);
    }

    [RelayCommand]
    private async Task PreviousHistoryPageAsync()
    {
        if (!CanGoBackInHistory) return;
        await LoadHistoryPageAsync(HistoryPage - 1);
    }

    private async Task LoadHistoryPageAsync(int pageNumber)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var segment = SelectedAppointmentTab == AppointmentListTab.Done
                ? AppointmentPageSegment.Done
                : AppointmentPageSegment.Cancelled;
            var page = await _calendarApiService.GetAppointmentsPageAsync(
                segment, pageNumber, HistoryPageSize);

            if (segment == AppointmentPageSegment.Done)
                _donePage = page;
            else
                _cancelledPage = page;

            HistoryPage = page.Page;
            OnPropertyChanged(nameof(DoneCount));
            OnPropertyChanged(nameof(CancelledCount));
            ApplyAppointmentTab();
        }
        catch (Exception)
        {
            ErrorMessage = AppResources.GetString("Error_LoadAppointments");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Raised when a booked row is tapped, so the page can open the appointment.
    /// </summary>
    /// <remarks>
    /// An event rather than the view model navigating itself, matching how every other list page here works:
    /// <c>Shell</c> is a MAUI type and this view model is covered on the <c>net10.0</c> test slice, where it does
    /// not exist.
    /// </remarks>
    public event EventHandler<AppointmentDetail>? AppointmentSelected;

    /// <summary>
    /// Opens an appointment from any of the three list segments.
    /// </summary>
    [RelayCommand]
    private void OpenAppointmentFromList(AppointmentDetail? appointment)
    {
        if (appointment is null || string.IsNullOrWhiteSpace(appointment.Id)) return;

        AppointmentSelected?.Invoke(this, appointment);
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(AppointmentListIsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(AppointmentListIsEmpty));
}
