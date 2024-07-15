namespace EventAndCommands.Events.Kafka;

public class MessageSendEvent: INotification
{
    public required string ProviderEmail { get; set; }
    public required string CustomerEmail { get; set; }
    public required string Message { get; set; }
}