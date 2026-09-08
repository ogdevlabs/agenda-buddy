using AgendaBuddy.Calendar.Domain.Commands;

namespace AgendaBuddy.Calendar.Core.Commands;

/// <summary>
/// Records a provider's time off, refusing by default when live appointments fall inside the range.
/// </summary>
/// <remarks>
/// The conflict check is the reason this is a handler rather than a passthrough to the service. Blocking over
/// existing bookings silently is the trap: the slot disappears from availability while the customer still holds
/// a session nobody has told them about. So the default answer is "no, and here is what is in the way", and
/// forcing it is a deliberate second act that leaves those sessions standing for the provider to deal with.
/// </remarks>
public class BlockCalendarCommandHandler(
    ICalendarBlockService calendarBlockService,
    IEventStore eventStore)
    : IRequestHandler<BlockCalendarCommand, Result<CalendarBlockEntity>>
{
    public async Task<Result<CalendarBlockEntity>> Handle(
        BlockCalendarCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        if (request.EndUtc <= request.StartUtc)
        {
            await SaveAsync("Failed", request);
            return Result.Fail<CalendarBlockEntity>("A block must end after it starts.");
        }

        if (!request.Force)
        {
            var conflicts = (await calendarBlockService.GetAffectedAppointmentsAsync(
                request.EmailProvider, request.StartUtc, request.EndUtc)).ToList();

            if (conflicts.Count > 0)
            {
                await SaveAsync("Failed", request);

                // The COUNT, not the appointments: this message is the provider's own, but a failure message is
                // the wrong place to carry customer emails and session times. The conflicts route returns them
                // to the provider deliberately, guarded, so a client can list them.
                //
                // Singular and plural are written out rather than assembled from an "s" -- the verb and the
                // pronoun change too, and pluralising only the noun produced "1 booked session fall inside".
                var message = conflicts.Count == 1
                    ? "1 booked session falls inside this range. Move or cancel it first, or block the range "
                      + "anyway and handle it afterwards."
                    : $"{conflicts.Count} booked sessions fall inside this range. Move or cancel them first, or "
                      + "block the range anyway and handle them afterwards.";

                return Result.Fail<CalendarBlockEntity>(message);
            }
        }

        var block = await calendarBlockService.BlockAsync(
            request.EmailProvider, request.StartUtc, request.EndUtc, request.Reason);

        if (block is null)
        {
            await SaveAsync("Failed", request);
            return Result.Fail<CalendarBlockEntity>("A block must end after it starts.");
        }

        await SaveAsync("Success", request);
        return Result.Ok(block);
    }

    private Task SaveAsync(string status, BlockCalendarCommand request) =>
        eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = status,
            Type = nameof(BlockCalendarCommand),

            // The range and the owner, never the reason: it is the provider's private note and the audit log is
            // not the place for it.
            Data = JsonSerializer.Serialize(new
            {
                request.EmailProvider,
                request.StartUtc,
                request.EndUtc,
                request.Force
            })
        });
}

/// <summary>
/// Removes one of a provider's own blocks.
/// </summary>
/// <remarks>
/// Removal matters as much as creation: a cancelled trip that still blocks the calendar costs the provider
/// bookings they never meant to refuse. The owning email is in the delete's own FILTER, so it cannot be raced
/// into removing somebody else's.
/// </remarks>
public class RemoveCalendarBlockCommandHandler(
    ICalendarBlockService calendarBlockService,
    IEventStore eventStore)
    : IRequestHandler<RemoveCalendarBlockCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        RemoveCalendarBlockCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var removed = await calendarBlockService.RemoveBlockAsync(request.EmailProvider, request.Identifier);

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = removed ? "Success" : "Failed",
            Type = nameof(RemoveCalendarBlockCommand),
            Data = JsonSerializer.Serialize(new { request.EmailProvider, request.Identifier })
        });

        return removed
            ? Result.Ok(true)
            : Result.Fail<bool>("No such block for this provider.");
    }
}
