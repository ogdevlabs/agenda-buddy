namespace EventAndCommands.Queries.Provider;

public class GetProvidersQueryHandler(IMediator mediator, ProviderService providerService)
    : IRequestHandler<GetProvidersQuery, IEnumerable<ProviderEntity>>
{
    public async Task<IEnumerable<ProviderEntity>> Handle(GetProvidersQuery request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetAllProvidersEvent(), cancellationToken);
        var providerList = await providerService.GetAllProviders();
        return await Task.FromResult(providerList);
    }
}