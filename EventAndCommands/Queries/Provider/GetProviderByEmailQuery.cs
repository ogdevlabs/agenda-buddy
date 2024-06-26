namespace EventAndCommands.Queries.Provider;

[ExcludeFromCodeCoverage]
public class GetProviderByEmailQuery : IRequest<ProviderEntity>
{
    public ProviderEntity? ProviderEntity { get; }
}