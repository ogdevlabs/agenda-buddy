using MediatR;

namespace EventAndCommands.Commands;

public class AddProviderTopicCommand : IRequest<string>
{
    public required string TopicName { get; set; }
}