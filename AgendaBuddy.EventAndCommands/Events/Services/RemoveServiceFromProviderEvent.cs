namespace AgendaBuddy.EventAndCommands.Events.Services;

public class RemoveServiceFromProviderEvent : INotification
{
    public required string Email { get; set; }
    public required string ServiceName { get; set; }
}
