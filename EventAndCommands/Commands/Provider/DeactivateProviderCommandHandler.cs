namespace EventAndCommands.Commands.Provider;

public class DeactivateProviderCommandHandler(
    IMediator mediator,
    ProviderService providerService,
    IEventStore eventStore)
    : IRequestHandler<DeactivateProviderCommand, string>
{
    public async Task<string> Handle(DeactivateProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(request, cancellationToken);

        var filter = SupportTools<ProviderEntity>.FilterByEmail(request.ProviderEntity.Email);
        var provider = await providerService.FindProvidersAsync(filter);

        if (provider is null)
        {
            var failEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "DeactivateProviderCommand",
                Data = JsonSerializer.Serialize(request.ProviderEntity)
            };
            await eventStore.SaveAsync(failEvent);
            return null!;
        }

        provider.IsActive = false;
        await providerService.UpdateProviderAsync(provider.Id.ToString(), provider);

        var successEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = "DeactivateProviderCommand",
            Data = JsonSerializer.Serialize(provider)
        };
        await eventStore.SaveAsync(successEvent);
        return provider.ToJson();
    }
}
