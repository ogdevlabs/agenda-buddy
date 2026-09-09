using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Resources.Strings;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// A provider's time off — travel, holiday, anything personal. Their calendar, their private note.
/// </summary>
/// <remarks>
/// <para>
/// A block is a time RANGE, which is the whole reason this screen exists: the mechanism it replaces was a
/// whole-day flag, so an afternoon off cost the entire day. Multi-day is one interval, not a run of days.
/// </para>
/// <para>
/// <b>Removal matters as much as adding.</b> A cancelled trip that still blocks the calendar costs the provider
/// bookings they never meant to refuse, so it is a first-class action rather than something to re-do by editing.
/// </para>
/// <para>
/// The <see cref="CalendarBlock.Reason"/> is provider-visible only. Customers see time off as absence from
/// availability and nothing more.
/// </para>
/// </remarks>
public partial class TimeOffViewModel : ObservableObject
{
    private readonly ICalendarBlockApiService _blockApiService;
    private readonly IUserSessionService _session;

    public TimeOffViewModel(ICalendarBlockApiService blockApiService, IUserSessionService session)
    {
        _blockApiService = blockApiService;
        _session = session;

        var today = DateTime.Today;
        _startDate = today;
        _endDate = today;
        _startTime = new TimeSpan(9, 0, 0);
        _endTime = new TimeSpan(17, 0, 0);
    }

    [ObservableProperty]
    private List<CalendarBlock> _blocks = [];

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Dedicated to the pull-to-refresh gesture, never shared with <see cref="IsLoading"/>.
    /// </summary>
    /// <remarks>
    /// Binding <c>RefreshView.IsRefreshing</c> to a general loading flag starts a refresh nobody asked for on
    /// every <c>OnAppearing</c>; on iOS that leaves the control's content inset behind as a blank band above the
    /// list that only a manual pull clears.
    /// </remarks>
    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // ── The add form ──────────────────────────────────────────────────────────────────────────────────

    [ObservableProperty]
    private DateTime _startDate;

    [ObservableProperty]
    private TimeSpan _startTime;

    [ObservableProperty]
    private DateTime _endDate;

    [ObservableProperty]
    private TimeSpan _endTime;

    [ObservableProperty]
    private string _reason = string.Empty;

    /// <summary>
    /// Whole days rather than a time range — the common case, and it must not require setting 00:00 to 00:00 by
    /// hand.
    /// </summary>
    [ObservableProperty]
    private bool _isAllDay = true;

    /// <summary>How many live sessions fall inside the range as currently entered; <c>null</c> when unknown.</summary>
    [ObservableProperty]
    private int? _conflictCount;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsProvider => _session.IsProvider;
    public bool HasBlocks => Blocks.Count > 0;

    /// <summary>Only claimed once a load has finished, so "no time off" is never shown before anything was asked.</summary>
    public bool ShowEmptyState => !IsLoading && !HasError && Blocks.Count == 0 && HasLoaded;

    public bool HasLoaded { get; private set; }

    /// <summary>Times are only editable when the block is not a whole-day one.</summary>
    public bool ShowTimePickers => !IsAllDay;

    /// <summary>
    /// The range being entered, as UTC instants.
    /// </summary>
    /// <remarks>
    /// A whole-day block runs from midnight to midnight AFTER the last day — the end is exclusive, so "8th to
    /// 9th, all day" has to end at the 10th's midnight or the 9th would stay bookable.
    /// </remarks>
    public (DateTime Start, DateTime End) Range => IsAllDay
        ? (StartDate.Date, EndDate.Date.AddDays(1))
        : (StartDate.Date + StartTime, EndDate.Date + EndTime);

    /// <summary>Refused rather than clamped: widening a provider's time off blocks time they never chose.</summary>
    public bool IsRangeValid => Range.End > Range.Start;

    public string RangeSummary
    {
        get
        {
            if (!IsRangeValid) return AppResources.GetString("TimeOff_InvalidRange");

            var (start, end) = Range;

            if (IsAllDay)
            {
                var days = (end.Date - start.Date).Days;
                return days == 1
                    ? $"{start.ToString("dddd d MMMM", AppResources.CurrentCulture)} · {AppResources.GetString("TimeOff_AllDay")}"
                    : $"{start.ToString("ddd d MMM", AppResources.CurrentCulture)} – {end.AddDays(-1).ToString("ddd d MMM", AppResources.CurrentCulture)} · {AppResources.Format("TimeOff_Days", days)}";
            }

            return start.Date == end.Date
                ? $"{start.ToString("dddd d MMMM", AppResources.CurrentCulture)} · {start.ToString("t", AppResources.CurrentCulture)} – {end.ToString("t", AppResources.CurrentCulture)}"
                : $"{start.ToString("g", AppResources.CurrentCulture)} – {end.ToString("g", AppResources.CurrentCulture)}";
        }
    }

    public bool CanSave => IsRangeValid && !IsSaving;

    /// <summary>
    /// Whether live sessions fall inside the range as entered.
    /// </summary>
    /// <remarks>
    /// Shown before saving so the cost of blocking is visible in advance. The server refuses by default and names
    /// the count, so this is the courteous half — but it is the half that stops the provider meeting a refusal
    /// they could have seen coming.
    /// </remarks>
    public bool HasConflicts => ConflictCount is > 0;

    public string ConflictMessage => ConflictCount switch
    {
        null or 0 => string.Empty,
           1 => AppResources.GetString("TimeOff_OneConflict"),
           _ => AppResources.Format("TimeOff_ManyConflicts", ConflictCount)
    };

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        await _session.RefreshAsync();
        OnPropertyChanged(nameof(IsProvider));

        try
        {
            Blocks = await _blockApiService.GetBlocksAsync();
        }
        catch (GatewayServiceUnavailableException exception)
        {
            ErrorMessage = GatewayErrorMapper.Describe(exception.FailedService);
        }
        catch (Exception)
        {
            // Never an empty list on failure: showing "no time off" for a failed read tells the provider their
            // calendar is open when it may not be.
            ErrorMessage = AppResources.GetString("Error_LoadTimeOff");
        }
        finally
        {
            HasLoaded = true;
            IsLoading = false;
            NotifyDerived();
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try { await LoadAsync(); }
        finally { IsRefreshing = false; }
    }

    /// <summary>
    /// Re-reads how many sessions the entered range would strand. Called as the pickers change.
    /// </summary>
    [RelayCommand]
    private async Task CheckConflictsAsync()
    {
        if (!IsRangeValid)
        {
            ConflictCount = null;
            NotifyDerived();
            return;
        }

        var (start, end) = Range;

        // null means "could not ask", which must not be reported as "nothing in the way".
        ConflictCount = await _blockApiService.CountConflictsAsync(start, end);
        NotifyDerived();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        IsSaving = true;
        ErrorMessage = string.Empty;
        NotifyDerived();

        try
        {
            var (start, end) = Range;
            var reason = string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim();

            // force: the provider has already been shown the conflict count, so a second refusal would be
            // telling them something they have seen and accepted.
            var result = await _blockApiService.BlockAsync(start, end, reason, force: HasConflicts);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? AppResources.GetString("Error_SaveTimeOff");
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            await ToastNotifier.ShowAsync(AppResources.GetString("Action_TimeOffSaved"));
            Reason = string.Empty;
            ConflictCount = null;
            await LoadAsync();
        }
        catch (GatewayServiceUnavailableException exception)
        {
            ErrorMessage = GatewayErrorMapper.Describe(exception.FailedService);
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
        finally
        {
            IsSaving = false;
            NotifyDerived();
        }
    }

    [RelayCommand]
    private async Task RemoveAsync(CalendarBlock? block)
    {
        if (block is null) return;

        ErrorMessage = string.Empty;

        try
        {
            var result = await _blockApiService.RemoveBlockAsync(block.Identifier);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? AppResources.GetString("Error_RemoveTimeOff");
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            await ToastNotifier.ShowAsync(AppResources.GetString("Action_TimeOffRemoved"));
            await LoadAsync();
        }
        catch (GatewayServiceUnavailableException exception)
        {
            ErrorMessage = GatewayErrorMapper.Describe(exception.FailedService);
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
    }

    private void NotifyDerived()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasBlocks));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(HasLoaded));
        OnPropertyChanged(nameof(ShowTimePickers));
        OnPropertyChanged(nameof(Range));
        OnPropertyChanged(nameof(IsRangeValid));
        OnPropertyChanged(nameof(RangeSummary));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(HasConflicts));
        OnPropertyChanged(nameof(ConflictMessage));
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnErrorMessageChanged(string value) => NotifyDerived();
    partial void OnIsLoadingChanged(bool value) => NotifyDerived();
    partial void OnIsSavingChanged(bool value) => NotifyDerived();
    partial void OnBlocksChanged(List<CalendarBlock> value) => NotifyDerived();
    partial void OnConflictCountChanged(int? value) => NotifyDerived();

    partial void OnIsAllDayChanged(bool value) => NotifyDerived();

    // Any picker change invalidates the previous conflict answer: it was about a different range.
    partial void OnStartDateChanged(DateTime value) => OnRangeEdited();
    partial void OnEndDateChanged(DateTime value) => OnRangeEdited();
    partial void OnStartTimeChanged(TimeSpan value) => OnRangeEdited();
    partial void OnEndTimeChanged(TimeSpan value) => OnRangeEdited();

    private void OnRangeEdited()
    {
        ConflictCount = null;
        NotifyDerived();
    }
}
