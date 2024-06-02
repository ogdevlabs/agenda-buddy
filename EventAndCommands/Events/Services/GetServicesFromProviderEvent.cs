namespace EventAndCommands.Events.Services;

public class GetServicesFromProviderEvent: INotification
{
    public required string Email { get; set; }
}