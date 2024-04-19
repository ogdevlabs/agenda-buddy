using MediatR;

namespace EventAndCommands.Commands;

public class CreateTopicCommand : IRequest<string>
{
    public required string TopicName { get; set; }
}