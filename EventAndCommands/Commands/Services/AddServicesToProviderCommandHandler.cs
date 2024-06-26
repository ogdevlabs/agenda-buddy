namespace EventAndCommands.Commands.Services;

[RegisterService(ServiceLifetime.Scoped)]
public class AddServicesToProviderCommandHandler(
    IMediator mediator,
    ProviderService providerService,
    List<ServiceEntity> serviceEntities,
    string email)
    : IRequestHandler<AddServicesToProviderCommand, ProviderEntity>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();

    public async Task<ProviderEntity> Handle(AddServicesToProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new AddServicesToProviderEvent
        {
            Email = email,
            ServiceEntities = serviceEntities
        }, cancellationToken);

        var provider = await providerService.FindProviders(SupportTools<ProviderEntity>.FilterByEmail(email));
        if (provider != null)
        {
            provider.ServiceEntities.AddRange(SupportTools<ServiceEntity>.GenerateIdForRecord(serviceEntities));
            var updateResult = await providerService.UpdateProvider(provider.Id.ToString(), provider);
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
                await EventStore!.SaveAsync(successEvent);
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
            await EventStore!.SaveAsync(failEvent);
        }

        return null!;
    }
}