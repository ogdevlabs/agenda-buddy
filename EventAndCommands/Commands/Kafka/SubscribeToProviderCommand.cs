namespace EventAndCommands.Commands.Kafka;

public class SubscribeToProviderCommand : IRequest<string>
{
    public required CustomerSubscribedToProviderEntity CustomerSubscribedToProviderEntity { get; set; }
}