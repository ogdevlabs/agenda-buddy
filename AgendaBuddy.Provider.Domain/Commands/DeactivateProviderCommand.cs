namespace AgendaBuddy.Provider.Domain.Commands;

[ExcludeFromCodeCoverage]
public class DeactivateProviderCommand : IRequest<Result<ProviderEntity>>
{
    public required ProviderEntity ProviderEntity { get; set; }
}
