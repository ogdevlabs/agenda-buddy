namespace EventAndCommands.Queries.Provider;

[ExcludeFromCodeCoverage]
public class GetProvidersQuery : IRequest<IEnumerable<ProviderEntity>>
{
    public IEnumerable<ProviderEntity>? ProviderEntities { get; }
}