namespace EventAndCommands.Queries.Provider;

public class GetProvidersQuery : IRequest<IEnumerable<ProviderEntity>>
{
    public IEnumerable<ProviderEntity>? ProviderEntities { get; private set; }
}