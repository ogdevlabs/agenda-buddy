namespace EventAndCommands.Commands.Provider;

[ExcludeFromCodeCoverage]
public class AddProviderCommand : IRequest<string>
{
    public required string TopicName { get; set; }
}
