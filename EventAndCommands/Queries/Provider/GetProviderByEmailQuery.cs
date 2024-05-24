namespace EventAndCommands.Queries.Provider;

public class GetProviderByEmailQuery : IRequest<ProviderEntity>
{
    public ProviderEntity? ProviderEntity { get; private set; }
}