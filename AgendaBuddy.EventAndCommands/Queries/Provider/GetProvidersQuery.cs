namespace AgendaBuddy.EventAndCommands.Queries.Provider;

[ExcludeFromCodeCoverage]
public class GetProvidersQuery : IRequest<PagedResponse<ProviderEntity>>
{
    public List<ProviderEntity>? ProviderEntities { get; }
}
