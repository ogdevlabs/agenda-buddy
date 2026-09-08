using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// A provider's own calendar week — the windows their bookable slots are generated in, one per weekday.
/// </summary>
/// <remarks>
/// <para>
/// Whole hours, on the provider's own clock (their device zone, recorded as they use the app). The end hour is
/// exclusive, so 08:00–17:00 means the last session finishes at 17:00 — which is why the pickers offer 0–23 for
/// the start and 1–24 for the end.
/// </para>
/// <para>
/// <b>Seven rows, and the screen opens on the hours the provider already had.</b> A provider who has only ever
/// set the single pair has an empty stored week, so every row is seeded from that pair rather than left blank —
/// opening this page must not look as though their calendar was wiped.
/// </para>
/// <para>
/// A day is closed by its own flag, and its hours are kept while it is, so re-opening a day does not mean
/// re-entering them. Each row carries its own validity, because a single page-level error for seven rows cannot
/// say which day is wrong.
/// </para>
/// </remarks>
public partial class CalendarSettingsViewModel : ObservableObject
{
    private readonly IProviderApiService _providerApiService;
    private readonly IUserSessionService _session;

    /// <summary>Monday first: it is how a working week is read, even though DayOfWeek starts at Sunday.</summary>
    private static readonly DayOfWeek[] WeekOrder =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    /// <summary>Selectable start hours: any hour of the day, since a day cannot start at 24:00.</summary>
    public IReadOnlyList<string> StartHourOptions { get; } =
        Enumerable.Range(0, 24).Select(WorkDayRow.Format).ToList();

    /// <summary>Selectable end hours: 01:00 through 24:00, the latter meaning midnight.</summary>
    public IReadOnlyList<string> EndHourOptions { get; } =
        Enumerable.Range(1, 24).Select(WorkDayRow.Format).ToList();

    /// <summary>The seven editable rows, Monday first.</summary>
    [ObservableProperty]
    private List<WorkDayRow> _days = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>Raised once the hours are stored, so the page can return to the calendar.</summary>
    public event EventHandler? Saved;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Only a provider has a calendar to configure.</summary>
    public bool IsProvider => _session.IsProvider;

    /// <summary>
    /// Every open day has to open before it closes, so saving is refused rather than silently corrected.
    /// </summary>
    public bool IsWeekValid => Days.Count > 0 && Days.All(day => day.IsValid);

    /// <summary>
    /// What the week amounts to, in one line.
    /// </summary>
    /// <remarks>
    /// Names the closed days rather than the open ones: a provider scanning this is checking they have not left
    /// a day shut by accident, which is the mistake that costs them bookings silently.
    /// </remarks>
    public string WeekSummary
    {
        get
        {
            if (!IsWeekValid) return "Some days do not open before they close.";

            var closed = Days.Where(day => day.IsClosed).Select(day => day.ShortDayName).ToList();
            var openHours = Days.Where(day => !day.IsClosed).Sum(day => day.EndHour - day.StartHour);

            return closed.Count switch
            {
                0 => $"Open every day, {openHours} bookable hours a week.",
                7 => "Closed every day — no one can book you.",
                _ => $"Closed {string.Join(", ", closed)} · {openHours} bookable hours a week."
            };
        }
    }

    /// <summary>Warned about rather than blocked: a provider may genuinely be shutting up shop for a while.</summary>
    public bool IsFullyClosed => Days.Count > 0 && Days.All(day => day.IsClosed);

    public CalendarSettingsViewModel(IProviderApiService providerApiService, IUserSessionService session)
    {
        _providerApiService = providerApiService;
        _session = session;
        Apply(WorkHours.Default, []);
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
            // Both, because they answer different questions: the pair is the fallback every unconfigured
            // weekday inherits, and the week is whatever has been set specifically. Seeding the rows from the
            // pair is what stops this page opening as seven blanks for a provider who has hours already.
            var hours = await _providerApiService.GetWorkHoursAsync(_session.Email);
            if (hours is null)
            {
                ErrorMessage = "Could not load your calendar hours. Check your connection and try again.";
                return;
            }

            var week = await _providerApiService.GetWorkWeekAsync(_session.Email);
            Apply(hours.Value, week);
        }
        catch (Exception)
        {
            ErrorMessage = "Could not load your calendar hours. Check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsWeekValid))]
    private async Task SaveAsync()
    {
        IsSaving = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _providerApiService.UpdateWorkWeekAsync(
                _session.Email,
                Days.Select(day => new WorkDayHoursDto(
                    day.Day, day.StartHour, day.EndHour, day.IsClosed)));

            if (!result.Succeeded)
            {
                // The server names which weekday it refused, which cannot be reconstructed here.
                ErrorMessage = result.ErrorMessage ?? "Could not save your calendar — try again.";
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            await ToastNotifier.ShowAsync("Calendar saved.");
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            ErrorMessage = "Could not reach the server. Check your connection and try again.";
            await ToastNotifier.ShowAsync(ErrorMessage);
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Copies the first OPEN day's hours to every other open day.
    /// </summary>
    /// <remarks>
    /// Without this, setting up a normal week is fourteen pickers. Closed days are left closed — the provider
    /// said they do not work then, and "copy hours" is not an instruction to start.
    /// </remarks>
    [RelayCommand]
    private async Task CopyFirstDayToAllAsync()
    {
        var source = Days.FirstOrDefault(day => !day.IsClosed);
        if (source is null)
        {
            await ToastNotifier.ShowAsync("Open at least one day first.");
            return;
        }

        foreach (var day in Days.Where(day => !day.IsClosed && !ReferenceEquals(day, source)))
        {
            day.StartHourIndex = source.StartHourIndex;
            day.EndHourIndex = source.EndHourIndex;
        }

        NotifyWeekChanged();
        await ToastNotifier.ShowAsync(
            $"{source.ShortDayName}'s hours copied to every open day.");
    }

    /// <summary>
    /// Builds the seven rows: the stored week where it exists, the single pair everywhere else.
    /// </summary>
    /// <remarks>
    /// The fallback is the load-bearing half. Every provider stored before per-weekday hours existed has an empty
    /// week, and seeding those rows from their single pair is what makes this screen open showing the hours they
    /// actually have — rather than a blank form that saving would turn into a change they never intended.
    /// </remarks>
    private void Apply(WorkHours fallback, List<WorkDayHoursDto> stored)
    {
        var byDay = stored.GroupBy(day => day.Day).ToDictionary(group => group.Key, group => group.First());

        var rows = new List<WorkDayRow>();

        foreach (var day in WeekOrder)
        {
            if (byDay.TryGetValue(day, out var configured))
            {
                // An unusable stored pair is treated as unconfigured, matching the server: a provider silently
                // unbookable is worse than one on their fallback hours.
                var usable = configured.StartHour is >= 0 and <= 23
                             && configured.EndHour is >= 1 and <= 24
                             && configured.StartHour < configured.EndHour;

                rows.Add(new WorkDayRow(
                    day,
                    usable ? configured.StartHour!.Value : fallback.StartHour,
                    usable ? configured.EndHour!.Value : fallback.EndHour,
                    configured.IsClosed));
            }
            else
            {
                rows.Add(new WorkDayRow(day, fallback.StartHour, fallback.EndHour, isClosed: false));
            }
        }

        foreach (var row in rows) row.Changed += OnRowChanged;

        Days = rows;
        NotifyWeekChanged();
    }

    private void OnRowChanged(object? sender, EventArgs e) => NotifyWeekChanged();

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnDaysChanged(List<WorkDayRow> value) => NotifyWeekChanged();

    private void NotifyWeekChanged()
    {
        OnPropertyChanged(nameof(IsWeekValid));
        OnPropertyChanged(nameof(WeekSummary));
        OnPropertyChanged(nameof(IsFullyClosed));
        SaveCommand.NotifyCanExecuteChanged();
    }
}
