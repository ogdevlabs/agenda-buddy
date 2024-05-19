using MediatR;

namespace EventAndCommands.Commands;

public class AddProviderCommand : IRequest<string>
{
    public required string TopicName { get; set; }
}