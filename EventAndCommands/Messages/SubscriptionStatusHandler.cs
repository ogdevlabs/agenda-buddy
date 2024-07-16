namespace EventAndCommands.Messages;

public class SubscriptionStatusHandler : IMessageHandler<string>
{
    public Task Handle(IMessageContext context, string message)
    {
        var @event = JsonSerializer.Deserialize<SubscriptionStatusEvent>(message);
        Console.WriteLine(
            $"Consumer {@event!.SubscriptionStatus!.ConsumerEmail} subscription status: {@event.SubscriptionStatus.Status}");
        return Task.CompletedTask;
    }
}