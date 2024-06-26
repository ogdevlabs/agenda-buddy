namespace EventAndCommands.Events.Provider;

[ExcludeFromCodeCoverage]
public class DeactivateProviderEvent : INotification
{
    public required ProviderEntity ProviderEntity { get; set; }
}