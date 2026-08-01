namespace EventAndCommands.Commands.Services;

public class UpdateServicesFromProviderCommandHandler(
    IMediator mediator,
    ProviderService providerService,
    List<ServiceEntity> serviceEntities,
    string email,
    IEventStore eventStore) : IRequestHandler<UpdateServicesFromProviderCommand, ProviderEntity>
{

    public async Task<ProviderEntity> Handle(UpdateServicesFromProviderCommand request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new UpdateServicesFromProviderEvent
        {
            Email = email,
            ServiceEntities = serviceEntities
        }, cancellationToken);
        var existingProvider = await providerService.FindProvidersAsync(SupportTools<ProviderEntity>.FilterByEmail(email));
        if (existingProvider == null) return null!;
        var updatedServices = serviceEntities;
        foreach (var updatedService in updatedServices)
        {
            var serviceMatch = existingProvider.ServiceEntities.SingleOrDefault(p => p.Name == updatedService.Name);
            if (serviceMatch == null) continue;
            serviceMatch.Description = updatedService.Description;
            serviceMatch.Fee = updatedService.Fee;
            serviceMatch.FeeType = updatedService.FeeType;
        }

        var updateResult = await providerService.UpdateProviderAsync(existingProvider.Id.ToString(), existingProvider);
        if (updateResult)
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "UpdateServicesFromProviderCommand",
                Data = JsonSerializer.Serialize(existingProvider)
            };
            await eventStore.SaveAsync(successEvent);
            return await Task.FromResult(existingProvider);
        }

        var failEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "UpdateServicesFromProviderCommand",
            Data = JsonSerializer.Serialize(new ProviderEntity())
        };
        await eventStore.SaveAsync(failEvent);
        return null!;
    }
}