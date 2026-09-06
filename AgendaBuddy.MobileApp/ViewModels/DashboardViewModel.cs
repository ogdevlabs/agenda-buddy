using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IBookingApiService _bookingApiService;
    private readonly IUserSessionService _session;
    private readonly BrandHeaderViewModel _signedInUser;
    private List<AppointmentSummary> _allAppointments = new();
    private int _pageIndex;
    private const int PageSize = 4;

    [ObservableProperty]
    private List<AppointmentSummary> _appointments = new();

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Drives the pull-to-refresh control, and nothing else does.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="IsLoading"/> on purpose — see <see cref="RefreshAsync"/> for the blank band
    /// that sharing one flag produced.
    /// </remarks>
    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private int _todayCount;

    [ObservableProperty]
    private int _weekCount;

    [ObservableProperty]
    private string _greeting = string.Empty;

    /// <summary>
    /// The user's own name, shown beside <see cref="Greeting"/>. Empty until it resolves, and empty for an
    /// account with no profile, in which case the greeting simply stands alone.
    /// </summary>
    public string UserDisplayName => _signedInUser.DisplayName;

    public bool HasUserDisplayName => !string.IsNullOrEmpty(UserDisplayName);

    /// <summary>
    /// <see cref="UserDisplayName"/> ready to append to the greeting. The comma lives here rather than in a
    /// XAML <c>StringFormat</c>, which would render a trailing ", " while the name is still unresolved.
    /// </summary>
    public string GreetingNameSuffix => HasUserDisplayName ? $", {UserDisplayName}" : string.Empty;

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

    public string SectionTitle => "Upcoming Sessions";
    public string PrimaryStatLabel => "Today";
    public string PrimaryStatCaption => IsCustomer ? "" : "Sessions";
    public string SecondaryStatLabel => "Upcoming";
    public string SecondaryStatCaption => "";
    public string EmptyStateTitle => IsCustomer ? "Nothing Booked Yet" : "No Appointments";
    public string EmptyStateSubtitle => IsCustomer
        ? "Book a session with a provider and it will show up here."
        : "Check your calendar for upcoming sessions.";

    public event EventHandler? AppointmentsLoaded;

    /// <remarks>
    /// Takes <see cref="BrandHeaderViewModel"/> because it is the singleton that already resolves and caches
    /// the signed-in user's name — the JWT does not carry one, so it costs a profile call. Fetching it again
    /// here would mean a second round trip per dashboard load for the same string.
    /// </remarks>
    public DashboardViewModel(
        IBookingApiService bookingApiService,
        IUserSessionService session,
        BrandHeaderViewModel signedInUser)
    {
        _bookingApiService = bookingApiService;
        _session = session;
        _signedInUser = signedInUser;
        Greeting = DateTime.Now.Hour switch
        {
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            _ => "Good evening"
        };
    }

    /// <summary>
    /// Pull-to-refresh. The same load, but this is the only thing that may drive the refresh control.
    /// </summary>
    /// <remarks>
    /// <c>RefreshView.IsRefreshing</c> used to be bound straight to <see cref="IsLoading"/>, and
    /// <c>OnAppearing</c> fires <c>LoadCommand</c> — so simply arriving on the page started a refresh nobody
    /// asked for. On iOS that begins a <c>UIRefreshControl</c> animation and inserts its content inset; the
    /// inset was left behind when <see cref="IsLoading"/> went false before the control finished animating in,
    /// which is the **blank white band under the brand header that disappeared after a manual pull** (the pull
    /// resets the control). Splitting the flags means a programmatic load never touches it.
    /// </remarks>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadAsync();
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

        // Idempotent and cached per account, so this is a no-op once the name is known.
        await _signedInUser.RefreshAsync();
        OnPropertyChanged(nameof(UserDisplayName));
        OnPropertyChanged(nameof(HasUserDisplayName));
        OnPropertyChanged(nameof(GreetingNameSuffix));

        try
        {
            // Both roles lead with what is still ahead.
            //
            // For a Customer this used to fetch past appointments only, so a session booked seconds earlier
            // could never appear however often it refreshed. For a Provider it fetched today only, which
            // hid every session from tomorrow onward — a provider had no way to see their own forthcoming
            // bookings at all, and the "This Week" tile was really just today's count under another name.
            var results = await _bookingApiService.GetUpcomingAppointmentsAsync();

            _allAppointments = results;
            _pageIndex = 0;
            UpdatePage();

            TodayCount = results.Count(a => a.ScheduledAt.Date == DateTime.Today);
            WeekCount = results.Count;

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
            IsRefreshing = false;
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

    /// <summary>
    /// Raised when a card is tapped, so the page can navigate to that appointment's detail screen.
    /// </summary>
    /// <remarks>
    /// Replaces an inline expand/collapse. The cards grew in place to show service, phone and notes, but on
    /// iOS a <c>CollectionView</c> cell that grows does not shrink back when its content collapses — a
    /// closed card kept its opened height as dead space, and no amount of re-measuring, rebuilding the
    /// collection or forcing <c>ItemSizingStrategy="MeasureAllItems"</c> restored it. Every card is now a
    /// fixed height and the same information lives on the detail page, which already shows all of it and
    /// carries the actions besides.
    /// </remarks>
    public event EventHandler<AppointmentSummary>? AppointmentSelected;

    [RelayCommand]
    private void SelectAppointment(AppointmentSummary? item)
    {
        if (item is not null) AppointmentSelected?.Invoke(this, item);
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnAppointmentsChanged(List<AppointmentSummary> value) => OnPropertyChanged(nameof(IsEmpty));
}
