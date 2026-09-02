namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// A provider's free start times for one service, grouped by the date they fall on <b>for this device</b>.
/// </summary>
/// <remarks>
/// <para>
/// The whole window is fetched once and grouped here, so the calendar can show which dates are bookable
/// and switch between them without another request.
/// </para>
/// <para>
/// <b>Grouped by LOCAL date, holding UTC instants.</b> Both parts matter. Grouping by the UTC date filed a
/// 01:00Z slot under the following day for anyone behind UTC — so an evening slot appeared on tomorrow's
/// tile. The values stay <see cref="AvailabilitySlot.StartUtc"/> because the booking POST has to send the server's
/// own instant back unchanged.
/// </para>
/// </remarks>
public sealed class ProviderAvailability
{
    public static readonly ProviderAvailability Empty = new();

    /// <summary>Free slots keyed by their date on this device. A date with nothing free is absent.</summary>
    public Dictionary<DateOnly, List<AvailabilitySlot>> SlotsByDate { get; init; } = new();

    public bool HasAny => SlotsByDate.Count > 0;

    /// <summary>The dates with at least one free slot, ascending.</summary>
    public List<DateOnly> BookableDates => SlotsByDate.Keys.OrderBy(date => date).ToList();

    /// <summary>The earliest bookable date, or null when the provider is full for the whole window.</summary>
    public DateOnly? FirstBookableDate => SlotsByDate.Count == 0 ? null : BookableDates[0];

    public List<AvailabilitySlot> SlotsOn(DateOnly date) =>
        SlotsByDate.TryGetValue(date, out var slots) ? slots : [];
}
