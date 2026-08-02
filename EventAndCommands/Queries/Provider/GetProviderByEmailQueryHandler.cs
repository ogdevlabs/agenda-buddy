namespace EventAndCommands.Queries.Provider;

public class GetProviderByEmailQueryHandler(IMediator mediator, ProviderService providerService, string email, IEventStore eventStore)
    : IRequestHandler<GetProviderByEmailQuery, ProviderEntity>
{

    public async Task<ProviderEntity> Handle(GetProviderByEmailQuery request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetProviderByEmailEvent { Email = email }, cancellationToken);

        var filter = SupportTools<ProviderEntity>.FilterByEmail(email);
        var providerEntity = await providerService.FindProvidersAsync(filter);
        if (providerEntity != null)
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "GetProviderByEmailQuery",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await eventStore.SaveAsync(successEvent);
            return providerEntity;
        }

        var failEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Failed",
            Type = "GetProviderByEmailQuery",
            Data = JsonSerializer.Serialize(new ProviderEntity())
        };
        await eventStore.SaveAsync(failEvent);
        return null!;
    }
}