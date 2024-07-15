namespace EventAndCommands.Commands.Kafka;

public class ProviderCreateTopicCommand : IRequest<string>
{
    public required ProviderCreatedEvent Event { get; init; }
}