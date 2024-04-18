using MediatR;

namespace EventAndCommands.Commands;

public class CreateTopicCommand :IRequest
{
    public required string TopicName { get; set; }
}