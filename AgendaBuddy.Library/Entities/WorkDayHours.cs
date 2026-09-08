namespace AgendaBuddy.Library.Entities;

/// <summary>
/// One weekday's working window on the provider's own clock, or that weekday marked closed.
/// </summary>
/// <remarks>
/// <para>
/// Embedded in <see cref="ProviderEntity.WorkWeek"/>. A provider who has set nothing has an empty list and
/// falls back to <see cref="ProviderEntity.WorkDayStartHour"/>/<see cref="ProviderEntity.WorkDayEndHour"/>, and
/// from there to <see cref="Tools.AvailabilityCalculator"/>'s default — so no stored provider loses their
/// calendar by this field arriving.
/// </para>
/// <para>
/// <b>Whole hours, matching the single pair it generalises.</b> The slot grid steps by the hour, so a half-past
/// start has nowhere to land. <see cref="EndHour"/> is exclusive on the same convention: 17 means the last
/// session finishes at 17:00.
/// </para>
/// <para>
/// A day is closed either by <see cref="IsClosed"/> or by having no usable hours. The explicit flag exists so a
/// provider can say "Sunday, closed" without their previously-stored Sunday hours being lost, and so "closed"
/// is distinguishable from "never configured" — the latter inherits the fallback, the former does not.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
[BsonIgnoreExtraElements]
public class WorkDayHours
{
    public WorkDayHours()
    {
    }

    public WorkDayHours(DayOfWeek day, int? startHour, int? endHour, bool isClosed = false)
    {
        Day = day;
        StartHour = startHour;
        EndHour = endHour;
        IsClosed = isClosed;
    }

    /// <summary>
    /// Which weekday this applies to. Stored as its integer (Sunday = 0), which is
    /// <see cref="System.DayOfWeek"/>'s own value and what <see cref="DateTime.DayOfWeek"/> yields.
    /// </summary>
    [BsonElement("day")]
    [BsonRepresentation(BsonType.Int32)]
    public DayOfWeek Day { get; set; }

    [BsonElement("start_hour")]
    [BsonIgnoreIfNull]
    [Range(0, 23, ErrorMessage = "Work day start hour must be between 0 and 23.")]
    public int? StartHour { get; set; }

    /// <summary>The hour by which a session must have ENDED. Exclusive.</summary>
    [BsonElement("end_hour")]
    [BsonIgnoreIfNull]
    [Range(1, 24, ErrorMessage = "Work day end hour must be between 1 and 24.")]
    public int? EndHour { get; set; }

    /// <summary>The provider does not work this day at all. Takes precedence over any hours stored with it.</summary>
    [BsonElement("is_closed")]
    public bool IsClosed { get; set; }

    /// <summary>
    /// Whether this entry describes a window a session can actually be booked in.
    /// </summary>
    /// <remarks>
    /// A window that does not open before it closes is rejected with 400 at the API boundary rather than
    /// clamped, so reaching here with an unusable pair means the row predates that check or was written around
    /// it. It is treated as unconfigured rather than as closed — a provider silently unbookable is worse than
    /// one bookable on the fallback hours.
    /// </remarks>
    [BsonIgnore]
    public bool DescribesAWindow =>
        !IsClosed
        && StartHour is >= 0 and <= 23
        && EndHour is >= 1 and <= 24
        && StartHour < EndHour;
}
