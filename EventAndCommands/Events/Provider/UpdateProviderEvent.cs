namespace EventAndCommands.Events.Provider;

public class UpdateProviderEvent: INotification
{
    public ProviderEntity? ProviderEntity  { get; set; }
}