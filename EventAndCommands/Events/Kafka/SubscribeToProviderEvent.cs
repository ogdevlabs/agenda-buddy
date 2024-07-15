namespace EventAndCommands.Events.Kafka;

public class SubscribeToProviderEvent : INotification
{
    public required CustomerSubscribedToProviderEntity CustomerSubscribedToProviderEntity { get; set; }
}