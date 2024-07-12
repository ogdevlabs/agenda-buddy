namespace EventAndCommands.Events.Customer;

public class SubscribeToProviderEvent : INotification
{
    public required CustomerSubscribedToProviderEntity CustomerSubscribedToProviderEntity { get; set; }
}