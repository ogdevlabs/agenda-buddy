namespace EventAndCommands.Events.Kafka;

public class SubscriptionEvent : INotification
{
    public Subscription? Subscription;
}