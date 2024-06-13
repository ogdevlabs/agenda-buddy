namespace EventAndCommands.Queries.Provider;

[RegisterService(ServiceLifetime.Scoped)]
public class GetProviderByEmailQueryHandler(IMediator mediator, ProviderService providerService, string email)
    : IRequestHandler<GetProviderByEmailQuery, ProviderEntity>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();

    public async Task<ProviderEntity> Handle(GetProviderByEmailQuery request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetProviderByEmailEvent { Email = email }, cancellationToken);

        var filter = SupportTools<ProviderEntity>.FilterByEmail(email);
        var providerEntity = await providerService.FindProviders(filter);
        if (providerEntity != null)
        {
            var @successEvent = new Event()
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "GetProviderByEmailQuery",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await EventStore!.SaveAsync(@successEvent);
            return providerEntity;
        }

        var @failEvent = new Event()
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "GetProviderByEmailQuery",
            Data = JsonSerializer.Serialize(new ProviderEntity())
        };
        await EventStore!.SaveAsync(@failEvent);
        return null!;
    }
}