namespace AgendaBuddy.Provider.Domain.Commands;

[ExcludeFromCodeCoverage]
public class AddProviderCommand : IRequest<Result<ProviderEntity>>
{
    public required ProviderEntity ProviderEntity { get; set; }
}
