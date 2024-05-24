namespace EventAndCommands.Events.Provider;

public class AddProviderEvent: INotification
{
    public string? ProviderName { get; set; }
}