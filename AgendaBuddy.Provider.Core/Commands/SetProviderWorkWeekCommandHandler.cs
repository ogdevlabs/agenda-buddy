namespace AgendaBuddy.Provider.Core.Commands;

/// <summary>
/// Writes a provider's per-weekday hours with a targeted <c>$set</c> on the <c>work_week</c> array.
/// </summary>
/// <remarks>
/// A whole-document replace here would carry every defect of <c>PUT /api/v1/providers/{email}</c> with it — it
/// would discard a concurrent edit to the provider's services or appointments. That is why this is a dedicated
/// route with its own targeted write, exactly like the single-pair sibling.
/// </remarks>
public class SetProviderWorkWeekCommandHandler(
    IProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<SetProviderWorkWeekCommand, Result<ProviderEntity>>
{
    public async Task<Result<ProviderEntity>> Handle(
        SetProviderWorkWeekCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        if (request.Days is null || request.Days.Count == 0)
        {
            await SaveAsync("Failed", request);
            return Result.Fail<ProviderEntity>("At least one weekday is required.");
        }

        // Re-checked here as well as at the API boundary, so a caller reaching the handler directly gets the
        // same answer. An open day that does not open before it closes is REFUSED, never clamped: silently
        // correcting it leaves the provider looking at hours they did not choose, and silently accepting it
        // makes them unbookable that day with nothing to explain why.
        var unusable = request.Days
            .Where(day => !day.IsClosed && !day.DescribesAWindow)
            .Select(day => day.Day.ToString())
            .ToList();

        if (unusable.Count > 0)
        {
            await SaveAsync("Failed", request);
            return Result.Fail<ProviderEntity>(
                $"These days do not describe a usable window: {string.Join(", ", unusable)}.");
        }

        // A duplicate weekday is a client bug, and picking one silently would store hours nobody chose.
        var duplicates = request.Days
            .GroupBy(day => day.Day)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.ToString())
            .ToList();

        if (duplicates.Count > 0)
        {
            await SaveAsync("Failed", request);
            return Result.Fail<ProviderEntity>(
                $"These days appear more than once: {string.Join(", ", duplicates)}.");
        }

        var updated = await providerService.SetWorkWeekAsync(request.Email, request.Days);
        if (updated is null)
        {
            await SaveAsync("Failed", request);
            return Result.Fail<ProviderEntity>($"No provider found with email {request.Email}");
        }

        await SaveAsync("Success", request);
        return Result.Ok(updated);
    }

    private Task SaveAsync(string status, SetProviderWorkWeekCommand request) =>
        eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = status,
            Type = nameof(SetProviderWorkWeekCommand),
            Data = JsonSerializer.Serialize(request)
        });
}
