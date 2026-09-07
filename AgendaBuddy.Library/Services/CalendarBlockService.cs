namespace AgendaBuddy.Library.Services;

/// <inheritdoc cref="ICalendarBlockService"/>
public class CalendarBlockService(
    IRepository<CalendarBlockEntity> blockRepository,
    IRepository<AppointmentEntity> appointmentRepository) : ICalendarBlockService
{
    public async Task<CalendarBlockEntity?> BlockAsync(
        string emailProvider, DateTime start, DateTime end, string? reason = null)
    {
        var startUtc = DateTime.SpecifyKind(start.ToUniversalTime(), DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(end.ToUniversalTime(), DateTimeKind.Utc);

        // Refused, never clamped. A caller that got the order wrong is told so; widening or flipping the range
        // on their behalf would block time the provider never asked to block.
        if (endUtc <= startUtc) return null;

        var block = new CalendarBlockEntity(emailProvider, startUtc, endUtc, reason);
        await blockRepository.InsertAsync(block);
        return block;
    }

    public async Task<IEnumerable<CalendarBlockEntity>> GetBlocksAsync(string emailProvider, DateTime fromUtc)
    {
        // Matched on end, not start: a block that began last week and runs through next week is still in force
        // and must be returned. Filtering on start would drop exactly the multi-day blocks this replaced the
        // whole-day mechanism to support.
        var filter = new BsonDocument
        {
            { "email_provider", emailProvider },
            { "end", new BsonDocument("$gt", DateTime.SpecifyKind(fromUtc.ToUniversalTime(), DateTimeKind.Utc)) }
        };

        var sort = new BsonDocument("start", 1);
        var blocks = await blockRepository.FindAllAsync(filter, sort, MaxBlocksRead);
        return blocks;
    }

    public async Task<bool> RemoveBlockAsync(string emailProvider, string identifier)
    {
        // The provider's email is in the FILTER, not checked beforehand: one operation that can only ever match
        // this provider's own block, so it cannot be raced into deleting somebody else's.
        var filter = new BsonDocument
        {
            { "identifier", identifier },
            { "email_provider", emailProvider }
        };

        var removed = await blockRepository.FindOneAndDeleteAsync(filter);
        return removed is not null;
    }

    public async Task<IEnumerable<AppointmentEntity>> GetAffectedAppointmentsAsync(
        string emailProvider, DateTime start, DateTime end)
    {
        var startUtc = DateTime.SpecifyKind(start.ToUniversalTime(), DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(end.ToUniversalTime(), DateTimeKind.Utc);

        if (endUtc <= startUtc) return [];

        // Overlap, not containment: a session that begins before the block and runs into it is just as stranded
        // as one wholly inside it. Half-open on both sides, so a session ending exactly as the block opens is
        // untouched. Cancelled and Completed are excluded — neither is something a block can strand.
        var filter = new BsonDocument
        {
            { "email_provider", emailProvider },
            { "day_off", false },
            { "start", new BsonDocument("$lt", endUtc) },
            { "end", new BsonDocument("$gt", startUtc) },
            {
                "appointment_status", new BsonDocument("$in", new BsonArray
                {
                    (int)AppointmentStatus.Requested,
                    (int)AppointmentStatus.Booked,
                    (int)AppointmentStatus.RescheduleRequested
                })
            }
        };

        return await appointmentRepository.FindAllAsync(filter);
    }

    /// <summary>
    /// Ceiling on one read, so a provider with an unusual number of blocks cannot make every customer's
    /// availability request expensive. Far above any plausible amount of time off in a 90-day window.
    /// </summary>
    private const int MaxBlocksRead = 500;
}
