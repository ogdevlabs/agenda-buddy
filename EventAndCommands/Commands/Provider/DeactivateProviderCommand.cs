namespace EventAndCommands.Commands.Provider;

[ExcludeFromCodeCoverage]
public class DeactivateProviderCommand : IRequest<string>
{
    public required ProviderEntity ProviderEntity { get; set; }
}