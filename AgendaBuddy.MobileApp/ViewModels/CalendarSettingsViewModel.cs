using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// A provider's own calendar day — the window their bookable slots are generated in.
/// </summary>
/// <remarks>
/// Whole hours, on the provider's own clock (their device zone, recorded as they use the app). The end hour
/// is exclusive, so 08:00–17:00 means the last session finishes at 17:00 — which is why the pickers offer
/// 0–23 for the start and 1–24 for the end.
/// </remarks>
public partial class CalendarSettingsViewModel : ObservableObject
{
    private readonly IProviderApiService _providerApiService;
    private readonly IUserSessionService _session;

    /// <summary>Selectable start hours: any hour of the day, since a day cannot start at 24:00.</summary>
    public IReadOnlyList<string> StartHourOptions { get; } =
        Enumerable.Range(0, 24).Select(FormatHour).ToList();

    /// <summary>Selectable end hours: 01:00 through 24:00, the latter meaning midnight.</summary>
    public IReadOnlyList<string> EndHourOptions { get; } =
        Enumerable.Range(1, 24).Select(FormatHour).ToList();

    [ObservableProperty]
    private int _startHourIndex;

    [ObservableProperty]
    private int _endHourIndex;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>Raised once the hours are stored, so the page can return to the calendar.</summary>
    public event EventHandler? Saved;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public int StartHour => StartHourIndex;

    /// <summary>The end options start at 1, so the index is one behind the hour it names.</summary>
    public int EndHour => EndHourIndex + 1;

    /// <summary>Only a provider has a calendar to configure.</summary>
    public bool IsProvider => _session.IsProvider;

    /// <summary>
    /// A day that opens at or after it closes has no bookable slots in it, so saving is refused rather than
    /// silently corrected.
    /// </summary>
    public bool IsWindowValid => StartHour < EndHour;

    public string WindowSummary => IsWindowValid
        ? $"Bookable {FormatHour(StartHour)} to {FormatHour(EndHour)}, {EndHour - StartHour} hours a day."
        : "The day has to start before it ends.";

    public CalendarSettingsViewModel(IProviderApiService providerApiService, IUserSessionService session)
    {
        _providerApiService = providerApiService;
        _session = session;
        Apply(WorkHours.Default);
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
            var hours = await _providerApiService.GetWorkHoursAsync(_session.Email);
            if (hours is null)
            {
                ErrorMessage = "Could not load your calendar hours. Check your connection and try again.";
                return;
            }

            Apply(hours.Value);
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

    [RelayCommand(CanExecute = nameof(IsWindowValid))]
    private async Task SaveAsync()
    {
        IsSaving = true;
        ErrorMessage = string.Empty;

        try
        {
            var succeeded = await _providerApiService.UpdateWorkHoursAsync(
                _session.Email, new WorkHours(StartHour, EndHour));

            if (!succeeded)
            {
                ErrorMessage = "Could not save your calendar hours — try again.";
                await ToastNotifier.ShowAsync(ErrorMessage);
                return;
            }

            await ToastNotifier.ShowAsync("Calendar hours saved.");
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

    private void Apply(WorkHours hours)
    {
        StartHourIndex = Math.Clamp(hours.StartHour, 0, 23);
        EndHourIndex = Math.Clamp(hours.EndHour, 1, 24) - 1;
    }

    private static string FormatHour(int hour) => hour == 24 ? "24:00" : $"{hour:00}:00";

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnStartHourIndexChanged(int value) => NotifyWindowChanged();

    partial void OnEndHourIndexChanged(int value) => NotifyWindowChanged();

    private void NotifyWindowChanged()
    {
        OnPropertyChanged(nameof(StartHour));
        OnPropertyChanged(nameof(EndHour));
        OnPropertyChanged(nameof(IsWindowValid));
        OnPropertyChanged(nameof(WindowSummary));
        SaveCommand.NotifyCanExecuteChanged();
    }
}
