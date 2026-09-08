using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

/// <summary>
/// A provider's own time off. Provider-only, end to end.
/// </summary>
public interface ICalendarBlockApiService
{
    /// <summary>The provider's blocks that have not finished yet, soonest first.</summary>
    Task<List<CalendarBlock>> GetBlocksAsync(CancellationToken ct = default);

    /// <summary>
    /// How many live sessions fall inside a proposed range.
    /// </summary>
    /// <remarks>
    /// Read before saving, so the provider is told what blocking the range would strand rather than discovering
    /// it from a refusal. <c>null</c> means the question could not be answered — distinct from zero, and the
    /// caller must not report "nothing in the way" on a failed request.
    /// </remarks>
    Task<int?> CountConflictsAsync(DateTime startLocal, DateTime endLocal, CancellationToken ct = default);

    /// <summary>
    /// Records a block. Times are LOCAL and converted here, once.
    /// </summary>
    /// <param name="force">
    /// Block the range even though live sessions fall inside it. Without it the server refuses and names how
    /// many, which is the answer the provider should see first.
    /// </param>
    Task<AppointmentActionResult> BlockAsync(
        DateTime startLocal, DateTime endLocal, string? reason, bool force = false,
        CancellationToken ct = default);

    /// <summary>Removes a block. A cancelled trip that still blocks the calendar costs bookings.</summary>
    Task<AppointmentActionResult> RemoveBlockAsync(string identifier, CancellationToken ct = default);
}
