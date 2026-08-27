namespace AgendaBuddy.Provider.Domain.Commands;

// F-020-T11: carries Email directly, rather than the handler's own former constructor parameter
// (Requests/RequestCollection.cs, deleted) -- the per-request email comes from the command, not a
// per-instance constructor argument.
[ExcludeFromCodeCoverage]
public class UpdateProviderCommand : IRequest<Result<ProviderEntity>>
{
    public required string Email { get; set; }
    public required ProviderEntity ProviderEntity { get; set; }
}
