namespace AgendaBuddy.Calendar.Domain.Commands;

/// <summary>
/// Records a period the provider is unavailable — time off, travel, or anything personal.
/// </summary>
/// <remarks>
/// A time RANGE, not a date. An afternoon off must leave the morning bookable, which is exactly what the
/// whole-day <c>day_off</c> mechanism this replaces could not express. Multi-day is one long interval, not a
/// per-day expansion — the replaced implementation looped over days and wrote nothing at all when start and end
/// fell on the same date.
/// </remarks>
[ExcludeFromCodeCoverage]
public class BlockCalendarCommand : IRequest<Result<CalendarBlockEntity>>
{
    public required string EmailProvider { get; set; }

    public required DateTime StartUtc { get; set; }

    /// <summary>Exclusive: a block from 09:00 to 12:00 leaves 12:00 bookable.</summary>
    public required DateTime EndUtc { get; set; }

    /// <summary>The provider's own note. Never returned on a customer-facing route.</summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Whether to block the range even though live appointments fall inside it.
    /// </summary>
    /// <remarks>
    /// Default <c>false</c>, and the refusal names how many. Blocking over existing bookings silently is the
    /// trap here: the provider would believe they were free while customers still held sessions that had
    /// vanished from availability but not from anyone's calendar. Forcing it is a deliberate second act — the
    /// sessions are left standing and it is then the provider's job to move or cancel them.
    /// </remarks>
    public bool Force { get; set; }
}

/// <summary>Removes one of a provider's own blocks.</summary>
[ExcludeFromCodeCoverage]
public class RemoveCalendarBlockCommand : IRequest<Result<bool>>
{
    public required string EmailProvider { get; set; }

    public required string Identifier { get; set; }
}
