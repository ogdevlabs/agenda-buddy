namespace AgendaBuddy.Provider.Domain.Queries;

[ExcludeFromCodeCoverage]
public class GetProvidersQuery : IRequest<Result<PagedResponse<ProviderEntity>>>
{
    public required PageRequest Page { get; set; }
}
