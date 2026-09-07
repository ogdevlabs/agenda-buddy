namespace AgendaBuddy.Library.Services;

/// <summary>
/// A provider's time-off blocks.
/// </summary>
/// <remarks>
/// Replaces <c>ICalendarService.BlockCalendarPeriodAsync</c>, which wrote whole-day fake appointments, was never
/// called from any route or handler, and whose day loop wrote zero documents when start and end fell on the same
/// date — so a single-day block silently did nothing.
/// </remarks>
public interface ICalendarBlockService
{
    /// <summary>
    /// Records a block over <c>[start, end)</c>.
    /// </summary>
    /// <returns>
    /// The stored block, or <c>null</c> when the interval does not open before it closes. Refused rather than
    /// clamped, on the same rule as a working window: silently widening a provider's time off costs them
    /// bookings they never agreed to give up.
    /// </returns>
    Task<CalendarBlockEntity?> BlockAsync(string emailProvider, DateTime start, DateTime end, string? reason = null);

    /// <summary>
    /// A provider's blocks that end after <paramref name="fromUtc"/>, oldest first.
    /// </summary>
    /// <remarks>
    /// Bounded by time rather than returning every block ever recorded: this is read on the availability path,
    /// where a provider with years of history would otherwise pay for all of it on every customer's calendar.
    /// </remarks>
    Task<IEnumerable<CalendarBlockEntity>> GetBlocksAsync(string emailProvider, DateTime fromUtc);

    /// <summary>Removes a block by its public identifier, scoped to its owner.</summary>
    /// <returns><c>false</c> when no such block exists for that provider.</returns>
    Task<bool> RemoveBlockAsync(string emailProvider, string identifier);

    /// <summary>
    /// The provider's appointments that fall inside <c>[start, end)</c> and are still live.
    /// </summary>
    /// <remarks>
    /// Read BEFORE a block is created, so the provider is told what blocking that range would strand rather than
    /// discovering it afterwards. Cancelled and completed appointments are excluded — neither is something a
    /// block can strand.
    /// </remarks>
    Task<IEnumerable<AppointmentEntity>> GetAffectedAppointmentsAsync(
        string emailProvider, DateTime start, DateTime end);
}
