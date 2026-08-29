namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// A provider's free start times for one service, already grouped by date.
/// </summary>
/// <remarks>
/// The whole window is fetched once and grouped here, so the calendar can highlight which dates are
/// bookable and switch between them without another request. Times are the raw UTC instants the server
/// returned — the booking POST must send one of these back unchanged, so nothing here reformats or
/// shifts them.
/// </remarks>
public sealed class ProviderAvailability
{
    public static readonly ProviderAvailability Empty = new();

    /// <summary>Free start times keyed by date. A date with no free slot is absent, not empty.</summary>
    public Dictionary<DateOnly, List<DateTime>> SlotsByDate { get; init; } = new();

    public bool HasAny => SlotsByDate.Count > 0;

    /// <summary>The dates that have at least one free slot, ascending.</summary>
    public List<DateOnly> BookableDates => SlotsByDate.Keys.OrderBy(date => date).ToList();

    /// <summary>The earliest bookable date, or null when the provider is full for the whole window.</summary>
    public DateOnly? FirstBookableDate => SlotsByDate.Count == 0 ? null : BookableDates[0];

    public List<DateTime> SlotsOn(DateOnly date) =>
        SlotsByDate.TryGetValue(date, out var slots) ? slots : [];
}
