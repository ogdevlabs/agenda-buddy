namespace EventAndCommands.Events.Provider;

public class GetProviderByEmailEvent : INotification
{
    public required string Email { get; set; }
}