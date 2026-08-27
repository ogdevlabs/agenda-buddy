namespace AgendaBuddy.Provider.Domain.Queries;

// Carries Email directly, rather than as a per-instance constructor parameter on the handler.
[ExcludeFromCodeCoverage]
public class GetProviderByEmailQuery : IRequest<Result<ProviderEntity>>
{
    public required string Email { get; set; }
}
