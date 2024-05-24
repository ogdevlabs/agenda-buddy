namespace EventAndCommands.Commands.Provider;

public class UpdateProviderCommand : IRequest<string>
{
    public required ProviderEntity ProviderEntity { get; set; }
}