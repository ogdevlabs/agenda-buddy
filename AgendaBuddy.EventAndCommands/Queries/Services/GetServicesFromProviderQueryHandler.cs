namespace AgendaBuddy.EventAndCommands.Queries.Services;

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
            // Counts the services disclosed, not the single provider they were read from.
            await eventStore.SaveAsync(QueryAudit.Success(
                "GetServicesFromProviderQuery", providerEntity.ServiceEntities.Count));
            return providerEntity.ServiceEntities;
        }

        await eventStore.SaveAsync(QueryAudit.Failure("GetServicesFromProviderQuery"));
        return new List<ServiceEntity>();
    }
}
