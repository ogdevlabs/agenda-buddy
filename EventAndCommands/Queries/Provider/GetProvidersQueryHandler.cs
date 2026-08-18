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
            await eventStore.SaveAsync(QueryAudit.Success("GetProvidersQuery", providerEntities.Count));
            return providerEntities;
        }


        await eventStore.SaveAsync(QueryAudit.Failure("GetProvidersQuery"));
        return new List<ProviderEntity>();
    }
}