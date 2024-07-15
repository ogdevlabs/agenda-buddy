namespace EventAndCommands.Events.Kafka;

public class CreateTopicEvent : INotification
{
    public required string Email { get; set; }
}