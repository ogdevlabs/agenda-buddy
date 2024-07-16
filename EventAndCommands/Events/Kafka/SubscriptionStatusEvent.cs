namespace EventAndCommands.Events.Kafka;

public class SubscriptionStatusEvent : INotification
{
    public SubscriptionStatus? SubscriptionStatus;
}