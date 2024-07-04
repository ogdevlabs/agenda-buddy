namespace EventAndCommands.Queries.Provider;

[RegisterService(ServiceLifetime.Scoped)]
public class GetProvidersQueryHandler(IMediator mediator, ProviderService providerService)
    : IRequestHandler<GetProvidersQuery, List<ProviderEntity>>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();


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
            await EventStore!.SaveAsync(successEvent);
            return await Task.FromResult(providerEntities);
        }


        var failEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "GetProvidersQuery",
            Data = JsonSerializer.Serialize(new List<ProviderEntity>())
        };
        await EventStore!.SaveAsync(failEvent);
        return await Task.FromResult(new List<ProviderEntity>());
    }
}