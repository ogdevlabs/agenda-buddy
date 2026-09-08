namespace AgendaBuddy.Calendar.Domain.Queries;

/// <summary>A provider's own time-off blocks that have not finished yet.</summary>
/// <remarks>
/// Provider-visible only: the block's <c>Reason</c> is the provider's private note. The customer-facing
/// availability route already reflects blocks — as absence, which is all a customer needs and all they get.
/// </remarks>
[ExcludeFromCodeCoverage]
public class GetCalendarBlocksQuery : IRequest<Result<List<CalendarBlockEntity>>>
{
    public required string EmailProvider { get; set; }
}

/// <summary>
/// The live appointments a proposed block would strand, read BEFORE it is created.
/// </summary>
/// <remarks>
/// So the provider is told what blocking a range costs rather than discovering it afterwards. Cancelled and
/// completed sessions are excluded — neither is something a block can strand.
/// </remarks>
[ExcludeFromCodeCoverage]
public class GetCalendarBlockConflictsQuery : IRequest<Result<List<AppointmentEntity>>>
{
    public required string EmailProvider { get; set; }

    public required DateTime StartUtc { get; set; }

    public required DateTime EndUtc { get; set; }
}
