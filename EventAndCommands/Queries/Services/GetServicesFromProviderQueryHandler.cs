namespace EventAndCommands.Queries.Services;

[RegisterService(ServiceLifetime.Scoped)]
public class GetServicesFromProviderQueryHandler(
    IMediator mediator,
    ProviderService providerService,
    string email)
    : IRequestHandler<GetServicesFromProviderQuery, List<ServiceEntity>>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();

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
            await EventStore!.SaveAsync(successEvent);
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
        await EventStore!.SaveAsync(failEvent);
        return new List<ServiceEntity>();
    }
}