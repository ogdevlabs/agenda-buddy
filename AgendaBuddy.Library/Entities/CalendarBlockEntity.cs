namespace AgendaBuddy.Library.Entities;

/// <summary>
/// A period a provider is unavailable — time off, travel, or anything personal. Its own collection, its own
/// document, not an appointment.
/// </summary>
/// <remarks>
/// <para>
/// <b>Replaces the fake-appointment representation.</b> A block used to be an
/// <see cref="AppointmentEntity"/> with <c>day_off = true</c>: no time range (so an afternoon off blocked the
/// whole day), no reason, and it sat in the provider's embedded appointment list where every reader of that
/// list had to remember it was not a real session. It also had to invent a customer email to satisfy a required
/// field. This is a first-class record instead.
/// </para>
/// <para>
/// <b>The <c>day_off</c> flag stays readable</b> — <see cref="Tools.AvailabilityCalculator"/> still honours it
/// for rows already written — so nothing already blocked silently becomes bookable. Nothing new writes one.
/// </para>
/// <para>
/// <see cref="Start"/> and <see cref="End"/> are UTC instants and the interval is HALF-OPEN, matching how
/// appointments are compared: a block ending exactly when a slot starts is not a clash, so a provider can block
/// the morning and still be booked at noon. Multi-day is simply a long interval — there is no per-day expansion,
/// which is what the replaced implementation got wrong (its loop wrote zero days when start and end fell on the
/// same date, so a single-day block silently did nothing).
/// </para>
/// <para>
/// <see cref="Reason"/> is the provider's own note. <b>It must never be exposed to customers.</b> The
/// customer-facing availability response is free start times only — busy time is inferable as absence, which is
/// inherent to a booking product, but why a provider is away is not.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
[BsonIgnoreExtraElements]
public class CalendarBlockEntity
{
    public CalendarBlockEntity()
    {
    }

    [SetsRequiredMembers]
    public CalendarBlockEntity(string emailProvider, DateTime start, DateTime end, string? reason = null)
    {
        EmailProvider = emailProvider;
        Start = start;
        End = end;
        Reason = reason;
    }

    [BsonElement("_id")] public ObjectId Id { get; set; }

    /// <summary>
    /// Stable public identifier, for the same reason appointments carry one: <see cref="ObjectId"/> cannot be
    /// round-tripped through JSON without the custom converter, so routes address a block by this instead.
    /// </summary>
    [BsonElement("identifier")]
    public string Identifier { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Whose calendar this blocks. Blocks belong to providers only; a customer has no calendar to block.</summary>
    [BsonElement("email_provider")]
    [EmailAddress]
    public required string EmailProvider { get; set; } = string.Empty;

    [BsonElement("start")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Start { get; set; }

    /// <summary>Exclusive end, UTC. A block from 09:00 to 12:00 leaves 12:00 bookable.</summary>
    [BsonElement("end")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime End { get; set; }

    /// <summary>The provider's own note. Provider-visible only — never returned on a customer-facing route.</summary>
    [BsonElement("reason")]
    [BsonIgnoreIfNull]
    [StringLength(200, ErrorMessage = "A block reason cannot exceed 200 characters.")]
    public string? Reason { get; set; }

    [BsonElement("created_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this block describes a real interval.
    /// </summary>
    /// <remarks>
    /// A block that does not open before it closes is rejected at the API boundary rather than clamped, on the
    /// same rule as a working window. This is the last line of defence: an unusable interval is treated as
    /// blocking nothing, so a malformed row cannot empty a provider's calendar.
    /// </remarks>
    [BsonIgnore]
    public bool DescribesAnInterval => End > Start;

    /// <summary>
    /// Whether this block overlaps <c>[start, end)</c>. Half-open on both sides, so touching intervals do not
    /// clash.
    /// </summary>
    public bool Overlaps(DateTime start, DateTime end) =>
        DescribesAnInterval && start < End.ToUniversalTime() && Start.ToUniversalTime() < end;
}
