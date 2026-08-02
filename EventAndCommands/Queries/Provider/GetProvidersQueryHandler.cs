namespace EventAndCommands.Queries.Provider;

public class GetProvidersQueryHandler(IMediator mediator, ProviderService providerService, IEventStore eventStore)
    : IRequestHandler<GetProvidersQuery, List<ProviderEntity>>
{


    public async Task<List<ProviderEntity>> Handle(GetProvidersQuery request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetAllProvidersEvent(), cancellationToken);

        var providerList = await providerService.GetAllProvidersAsync();
        var providerEntities = providerList.ToList();
        if (providerEntities.Count != 0)
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "GetProvidersQuery",
                Data = JsonSerializer.Serialize(providerEntities)
            };
            await eventStore.SaveAsync(successEvent);
            return providerEntities;
        }


        var failEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "GetProvidersQuery",
            Data = JsonSerializer.Serialize(new List<ProviderEntity>())
        };
        await eventStore.SaveAsync(failEvent);
        return new List<ProviderEntity>();
    }
}