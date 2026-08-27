namespace AgendaBuddy.Provider.Core.Commands;

// Constructor takes only DI-resolvable services; email comes from the command, not a per-instance
// constructor parameter.
public class UpdateProviderCommandHandler(
    IMediator mediator,
    IProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<UpdateProviderCommand, Result<ProviderEntity>>
{
    public async Task<Result<ProviderEntity>> Handle(UpdateProviderCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(new UpdateProviderEvent { ProviderEntity = request.ProviderEntity }, cancellationToken);

        var record = await providerService.FindProvidersAsync(SupportTools<ProviderEntity>.FilterByEmail(request.Email));
        if (record is null)
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = nameof(UpdateProviderCommand),
                Data = JsonSerializer.Serialize(request.ProviderEntity)
            });
            return Result.Fail<ProviderEntity>($"No provider found with email {request.Email}");
        }

        request.ProviderEntity.Id = record.Id;
        var updateResult = await providerService.UpdateProviderAsync(record.Id.ToString(), request.ProviderEntity);
        if (!updateResult)
        {
            // Pre-existing gap, preserved: the original returned string.Empty here (mapped to 404 by
            // Program.cs) with no audit write on this branch either -- unlike the "record is null" branch
            // above. Same shape of gap Services' own migration documented for its sibling handler.
            return Result.Fail<ProviderEntity>($"Failed to update provider with email {request.Email}");
        }

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(UpdateProviderCommand),
            Data = JsonSerializer.Serialize(request.ProviderEntity)
        });
        return Result.Ok(request.ProviderEntity);
    }
}
