namespace AgendaBuddy.Provider.Domain.Commands;

// Carries Email directly -- the per-request email comes from the command, not a per-instance
// constructor argument.
[ExcludeFromCodeCoverage]
public class UpdateProviderCommand : IRequest<Result<ProviderEntity>>
{
    public required string Email { get; set; }
    public required ProviderEntity ProviderEntity { get; set; }
}
