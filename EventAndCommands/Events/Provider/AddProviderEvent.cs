namespace EventAndCommands.Events.Provider;

[ExcludeFromCodeCoverage]
public class AddProviderEvent : INotification
{
    public string? ProviderName { get; set; }
}
