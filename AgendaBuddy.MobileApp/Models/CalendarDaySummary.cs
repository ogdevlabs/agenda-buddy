using CommunityToolkit.Mvvm.ComponentModel;
using AgendaBuddy.MobileApp.Resources.Strings;

namespace AgendaBuddy.MobileApp.Models;

public partial class CalendarDaySummary : ObservableObject
{
    public string Date { get; set; } = string.Empty;
    public List<string> AvailableSlots { get; set; } = new();

    /// <summary>
    /// The day's booked sessions.
    /// </summary>
    /// <remarks>
    /// A model rather than the <c>List&lt;string&gt;</c> it was, because the row renders a second line for the
    /// session's LENGTH and a string has no length to bind to. The template already asked for
    /// <c>DurationLabel</c>, which on a string silently resolved to nothing — so every calendar row showed a
    /// blank second line where the duration belonged, and before that a hardcoded "30 min" that was wrong for
    /// every session of any other length.
    /// </remarks>
    public List<BookedSlot> BookedSlots { get; set; } = new();

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    public string DayOfWeek => DateTime.TryParse(Date, out var dt) ? dt.ToString("ddd", AppResources.CurrentCulture) : "";
    public string DayNumber => DateTime.TryParse(Date, out var dt) ? dt.Day.ToString(AppResources.CurrentCulture) : "";
    public string MonthDay => DateTime.TryParse(Date, out var dt) ? dt.ToString("MMM d", AppResources.CurrentCulture) : Date;
    public bool IsToday => DateTime.TryParse(Date, out var dt) && dt.Date == DateTime.Today;
    public bool HasBookings => BookedSlots.Count > 0;
    public bool ShowSlots => IsExpanded && HasBookings;

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ShowSlots));
}

/// <summary>
/// One booked session on the calendar: when it is, who it is with, and how long it runs.
/// </summary>
/// <remarks>
/// <see cref="DurationLabel"/> comes from the appointment's own <c>serviceDurationMinutes</c>, which has been on
/// the wire all along. Null for sessions booked before services were selectable — those genuinely have no
/// recorded length, so the line is omitted rather than guessed at.
/// </remarks>
public sealed record BookedSlot(string Label, int? DurationMinutes, AppointmentDetail Appointment)
{
    /// <summary>e.g. "45 min". Empty when the session has no recorded length.</summary>
    public string DurationLabel => DurationMinutes is { } minutes ? RuntimeText.Duration(minutes) : string.Empty;

    public bool HasDuration => DurationMinutes.HasValue;

    /// <summary>
    /// Whether tapping this row can open the session.
    /// </summary>
    /// <remarks>
    /// A row with no identifier cannot be opened, and offering the tap anyway would land on a page that has
    /// nothing to fetch — <c>AppointmentDetailPage</c> shows an explanatory error in that case, which is the
    /// right behaviour but the wrong thing to walk somebody into from a row that looks navigable.
    /// </remarks>
    public bool CanOpen => !string.IsNullOrWhiteSpace(Appointment.Id);
}

public class TimeSlot
{
    public string Time { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public bool IsBooked { get; set; }
}
