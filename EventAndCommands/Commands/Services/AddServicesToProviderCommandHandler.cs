namespace EventAndCommands.Commands.Services;

public class AddServicesToProviderCommandHandler(
    IMediator mediator,
    ProviderService providerService,
    List<ServiceEntity> serviceEntities,
    string email,
    IEventStore eventStore)
    : IRequestHandler<AddServicesToProviderCommand, ProviderEntity>
{

    public async Task<ProviderEntity> Handle(AddServicesToProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new AddServicesToProviderEvent
        {
            Email = email,
            ServiceEntities = serviceEntities
        }, cancellationToken);

        var provider = await providerService.FindProvidersAsync(SupportTools<ProviderEntity>.FilterByEmail(email));
        if (provider != null)
        {
            provider.ServiceEntities.AddRange(SupportTools<ServiceEntity>.GenerateIdForRecord(serviceEntities));
            var updateResult = await providerService.UpdateProviderAsync(provider.Id.ToString(), provider);
            if (updateResult)
            {
                var successEvent = new Event
                {
                    Id = ObjectId.GenerateNewId(),
                    TimeStamp = DateTime.UtcNow,
                    Status = "Success",
                    Type = "AddServicesToProviderCommand",
                    Data = JsonSerializer.Serialize(provider)
                };
                await eventStore.SaveAsync(successEvent);
                return await Task.FromResult(provider);
            }
        }
        else
        {
            var failEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "AddServicesToProviderCommand",
                Data = JsonSerializer.Serialize(new ProviderEntity())
            };
            await eventStore.SaveAsync(failEvent);
        }

        return null!;
    }
}