namespace EventAndCommands.Events.Provider;

[ExcludeFromCodeCoverage]
public class UpdateProviderEvent : INotification
{
    public ProviderEntity? ProviderEntity { get; set; }
}
