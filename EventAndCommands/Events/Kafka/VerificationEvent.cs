namespace EventAndCommands.Events.Kafka;

public class VerificationEvent : INotification
{
    public Verification? Verification;
}