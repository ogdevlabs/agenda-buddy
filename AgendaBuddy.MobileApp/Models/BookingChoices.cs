using CommunityToolkit.Mvvm.ComponentModel;
using AgendaBuddy.MobileApp.Resources.Strings;

namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// One selectable date in the booking flow.
/// </summary>
/// <remarks>
/// Wraps the <see cref="DateOnly"/> rather than binding it directly so the chosen date can carry its own
/// <see cref="IsSelected"/> state. A bare <see cref="DateOnly"/> has nowhere to hold that, which left every
/// card rendering identically and the customer unable to see which date they had picked.
/// </remarks>
public partial class DateChoice : ObservableObject
{
    public DateChoice(DateOnly date) => Date = date;

    public DateOnly Date { get; }

    [ObservableProperty]
    private bool _isSelected;

    public string DayName => Date.ToString("ddd", AppResources.CurrentCulture).ToUpper(AppResources.CurrentCulture);

    public string DayNumber => Date.ToString("dd", AppResources.CurrentCulture);

    public string MonthName => Date.ToString("MMM", AppResources.CurrentCulture);

    /// <summary>
    /// Spoken description. Selection is announced as well as coloured, so the state is not carried by
    /// colour alone.
    /// </summary>
    public string AccessibilityLabel =>
        AppResources.Format(IsSelected ? "Accessibility_DateSelected" : "Accessibility_Date", Date);

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(AccessibilityLabel));
}

/// <summary>
/// One selectable start time in the booking flow, wrapping the slot the server offered.
/// </summary>
/// <remarks>
/// <see cref="StartUtc"/> is forwarded deliberately: the booking POST must send back the server's own
/// instant unchanged, so nothing downstream should have to unwrap this to reach it.
/// </remarks>
public partial class SlotChoice : ObservableObject
{
    public SlotChoice(AvailabilitySlot slot) => Slot = slot;

    public AvailabilitySlot Slot { get; }

    /// <summary>The instant to POST back, untouched.</summary>
    public DateTime StartUtc => Slot.StartUtc;

    /// <summary>The same instant on this device's clock.</summary>
    public DateTime LocalStart => Slot.LocalStart;

    [ObservableProperty]
    private bool _isSelected;

    public string Label => Slot.Label;

    public string AccessibilityLabel => AppResources.Format(
        IsSelected ? "Accessibility_TimeSelected" : "Accessibility_Time", Label);

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(AccessibilityLabel));
}
