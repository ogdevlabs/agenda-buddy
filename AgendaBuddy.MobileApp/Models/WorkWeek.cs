using CommunityToolkit.Mvvm.ComponentModel;

namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// One weekday's editable row on the calendar-settings screen.
/// </summary>
/// <remarks>
/// <para>
/// An observable row per weekday rather than fourteen properties on the view model: the seven rows are laid out
/// from a single collection, and each has to carry its own validity so the form can say <b>which</b> day is
/// wrong. A single page-level error for seven rows is unusable.
/// </para>
/// <para>
/// <see cref="EndHour"/> is exclusive, matching the single-pair route this generalises: 17 means the last
/// session finishes at 17:00. Hours are whole hours because the slot grid steps by the hour.
/// </para>
/// </remarks>
public partial class WorkDayRow : ObservableObject
{
    public WorkDayRow(DayOfWeek day, int startHour, int endHour, bool isClosed)
    {
        Day = day;
        _startHourIndex = Math.Clamp(startHour, 0, 23);
        _endHourIndex = Math.Clamp(endHour, 1, 24) - 1;
        _isClosed = isClosed;
    }

    public DayOfWeek Day { get; }

    /// <summary>Full name, e.g. "Monday" — the row's own label.</summary>
    public string DayName => Day.ToString();

    /// <summary>Three letters, for the compact copy-to-all confirmation.</summary>
    public string ShortDayName => Day.ToString()[..3];

    [ObservableProperty]
    private int _startHourIndex;

    [ObservableProperty]
    private int _endHourIndex;

    /// <summary>
    /// The provider does not work this day.
    /// </summary>
    /// <remarks>
    /// Kept separate from the hours, and the hours are kept when it is set, so re-opening a day does not mean
    /// re-entering them — and so "closed" stays distinguishable from "never configured", which inherits the
    /// fallback rather than blocking the day.
    /// </remarks>
    [ObservableProperty]
    private bool _isClosed;

    public int StartHour => StartHourIndex;

    /// <summary>The end options start at 1, so the index is one behind the hour it names.</summary>
    public int EndHour => EndHourIndex + 1;

    /// <summary>A closed day has nothing to validate; an open one has to open before it closes.</summary>
    public bool IsValid => IsClosed || StartHour < EndHour;

    public bool HasError => !IsValid;

    /// <summary>Named per row, so the provider is told which day is wrong rather than that something is.</summary>
    public string ErrorMessage => IsValid ? string.Empty : $"{DayName} has to start before it ends.";

    /// <summary>What the row reads as at a glance.</summary>
    public string Summary => IsClosed
        ? "Closed"
        : IsValid ? $"{Format(StartHour)} – {Format(EndHour)}" : "Invalid";

    /// <summary>
    /// The inverse of <see cref="IsClosed"/> — what the row's Open/Closed switch binds to.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>This needs a SETTER, and not having one is what made closing a day impossible.</b>
    /// <c>CalendarSettingsPage</c> binds <c>Switch.IsToggled</c> here, and <c>IsToggled</c> is a two-way
    /// binding: with a getter only, the switch moved visually while the write back was silently dropped, so
    /// <see cref="IsClosed"/> never changed. Saving then sent every day as open, the server dutifully stored
    /// "open every day", and reopening the screen showed the weekend enabled again — which read as the save not
    /// persisting when in fact the closure never reached the view model at all.
    /// <para>
    /// Inverting into <see cref="IsClosed"/> rather than holding a second flag keeps one source of truth:
    /// <c>IsClosed</c> is what the wire and the entity carry, and two independent booleans would be free to
    /// disagree.
    /// </para>
    /// </remarks>
    public bool IsOpen
    {
        get => !IsClosed;
        set => IsClosed = !value;
    }

    public static string Format(int hour) => hour == 24 ? "24:00" : $"{hour:00}:00";

    partial void OnStartHourIndexChanged(int value) => Notify();
    partial void OnEndHourIndexChanged(int value) => Notify();

    partial void OnIsClosedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsOpen));
        Notify();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(StartHour));
        OnPropertyChanged(nameof(EndHour));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(Summary));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised on any edit, so the host can re-evaluate whether the whole week can be saved.</summary>
    public event EventHandler? Changed;
}

/// <summary>
/// One weekday's stored hours, as the wire carries them.
/// </summary>
/// <remarks>
/// Separate from <see cref="WorkDayRow"/> on purpose: the row is editor state (indices, validity, labels) and
/// this is the contract. Binding the editor straight to the wire type is what makes a picker index leak into a
/// request body.
/// </remarks>
public readonly record struct WorkDayHoursDto(DayOfWeek Day, int? StartHour, int? EndHour, bool IsClosed);
