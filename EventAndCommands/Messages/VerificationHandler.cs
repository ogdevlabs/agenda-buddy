namespace EventAndCommands.Messages;

public class VerificationHandler : IMessageHandler<string>
{
    public Task Handle(IMessageContext context, string message)
    {
        var verificationEvent = JsonSerializer.Deserialize<VerificationEvent>(message);
        Console.WriteLine($"Verification message: {verificationEvent!.Verification!.Message}");
        return Task.CompletedTask;
    }
}