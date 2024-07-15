namespace EventAndCommands.Commands.Kafka;

public class CustomerCreateTopicCommand : IRequest<string>
{
    public required CustomerCreatedEvent Event { get; init; }
}