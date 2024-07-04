namespace EventAndCommands.Queries.Provider;

[ExcludeFromCodeCoverage]
public class GetProvidersQuery : IRequest<List<ProviderEntity>>
{
    public List<ProviderEntity>? ProviderEntities { get; }
}