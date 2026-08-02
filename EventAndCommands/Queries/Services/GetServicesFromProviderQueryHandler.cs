namespace EventAndCommands.Queries.Services;

public class GetServicesFromProviderQueryHandler(
    IMediator mediator,
    ProviderService providerService,
    string email,
    IEventStore eventStore)
    : IRequestHandler<GetServicesFromProviderQuery, List<ServiceEntity>>
{

    public async Task<List<ServiceEntity>> Handle(GetServicesFromProviderQuery request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetServicesFromProviderEvent { Email = email }, cancellationToken);

        var filter = SupportTools<ProviderEntity>.FilterByEmail(email);
        var providerEntity = await providerService.FindProvidersAsync(filter);
        if (providerEntity != null)
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "GetServicesFromProviderQuery",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await eventStore.SaveAsync(successEvent);
            return providerEntity.ServiceEntities;
        }

        var failEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "GetServicesFromProviderQuery",
            Data = JsonSerializer.Serialize(new ProviderEntity())
        };
        await eventStore.SaveAsync(failEvent);
        return new List<ServiceEntity>();
    }
}