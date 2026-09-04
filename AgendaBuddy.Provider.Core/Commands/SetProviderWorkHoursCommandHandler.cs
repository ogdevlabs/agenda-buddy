namespace AgendaBuddy.Provider.Core.Commands;

/// <summary>
/// Writes the provider's working-day bounds with a targeted <c>$set</c>, so saving hours cannot disturb
/// their services or appointments the way a whole-document replace would.
/// </summary>
public class SetProviderWorkHoursCommandHandler(
    IProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<SetProviderWorkHoursCommand, Result<ProviderEntity>>
{
    public async Task<Result<ProviderEntity>> Handle(SetProviderWorkHoursCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        // A window that opens at or after it closes leaves the provider unbookable, so it is refused here
        // as well as at the API boundary — a caller reaching the handler directly gets the same answer.
        if (request.StartHour < 0 || request.StartHour > 23
            || request.EndHour < 1 || request.EndHour > 24
            || request.StartHour >= request.EndHour)
        {
            await SaveAsync("Failed", request);
            return Result.Fail<ProviderEntity>(
                $"Work hours {request.StartHour}-{request.EndHour} do not describe a usable day.");
        }

        var updated = await providerService.SetWorkHoursAsync(request.Email, request.StartHour, request.EndHour);
        if (updated is null)
        {
            await SaveAsync("Failed", request);
            return Result.Fail<ProviderEntity>($"No provider found with email {request.Email}");
        }

        await SaveAsync("Success", request);
        return Result.Ok(updated);
    }

    private Task SaveAsync(string status, SetProviderWorkHoursCommand request) =>
        eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = status,
            Type = nameof(SetProviderWorkHoursCommand),
            Data = JsonSerializer.Serialize(request)
        });
}
