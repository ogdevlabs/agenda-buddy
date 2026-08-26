namespace EventAndCommands.Commands.Provider;

[ExcludeFromCodeCoverage]
public class UpdateProviderCommand : IRequest<string>
{
    public required ProviderEntity ProviderEntity { get; set; }
}
