namespace EventAndCommands.Events.Kafka;

public class CustomerCreatedEvent : INotification
{
    [EmailAddress]
    public required string Email { get; set; }
}