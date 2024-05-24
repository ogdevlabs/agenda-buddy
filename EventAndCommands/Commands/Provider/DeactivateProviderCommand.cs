namespace EventAndCommands.Commands.Provider;

public class DeactivateProviderCommand : IRequest<string>
{
    public required ProviderEntity ProviderEntity { get; set; }
}