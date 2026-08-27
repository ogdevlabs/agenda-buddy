namespace AgendaBuddy.Provider.Domain.Queries;

// F-020-T11: carries Email directly -- the pre-refactor handler (AgendaBuddy.EventAndCommands, deleted)
// took `email` as a per-instance constructor parameter instead.
[ExcludeFromCodeCoverage]
public class GetProviderByEmailQuery : IRequest<Result<ProviderEntity>>
{
    public required string Email { get; set; }
}
