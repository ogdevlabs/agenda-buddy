namespace EventAndCommands.Messages;

public class NotificationMessageHandler : IMessageHandler<string>
{
    public Task Handle(IMessageContext context, string message)
    {
        Console.WriteLine($"Received message: {message}");
        return Task.CompletedTask;
    }
}