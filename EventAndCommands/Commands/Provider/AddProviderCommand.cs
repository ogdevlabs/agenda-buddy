using MediatR;

namespace EventAndCommands.Commands.Provider;

public class AddProviderCommand : IRequest<string>
{
    public required string TopicName { get; set; }
}