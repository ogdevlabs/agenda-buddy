namespace AgendaBuddy.Provider.Core.Commands;

// Typed against IProviderService, extended with SetActiveAsync to make this possible (see
// IProviderService's own remarks).
public class DeactivateProviderCommandHandler(
    IMediator mediator,
    IProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<DeactivateProviderCommand, Result<ProviderEntity>>
{
    public async Task<Result<ProviderEntity>> Handle(DeactivateProviderCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        await mediator.Publish(
            new DeactivateProviderEvent { ProviderEntity = request.ProviderEntity }, cancellationToken);

        var filter = SupportTools<ProviderEntity>.FilterByEmail(request.ProviderEntity.Email);
        var provider = await providerService.FindProvidersAsync(filter);

        if (provider is null)
        {
            await eventStore.SaveAsync(new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = nameof(DeactivateProviderCommand),
                Data = JsonSerializer.Serialize(request.ProviderEntity)
            });
            return Result.Fail<ProviderEntity>($"No provider found with email {request.ProviderEntity.Email}");
        }

        // A targeted $set, not a whole-document replace -- see SetActiveAsync's own remarks.
        await providerService.SetActiveAsync(provider.Email, isActive: false);
        provider.IsActive = false;

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(DeactivateProviderCommand),
            Data = JsonSerializer.Serialize(provider)
        });
        return Result.Ok(provider);
    }
}
