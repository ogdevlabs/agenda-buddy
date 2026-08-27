namespace AgendaBuddy.EventAndCommands.Events.Provider;

[ExcludeFromCodeCoverage]
public class GetProviderByEmailEvent : INotification
{
    public required string Email { get; set; }
}
