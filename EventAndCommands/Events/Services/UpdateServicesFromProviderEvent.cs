namespace EventAndCommands.Events.Services;

public class UpdateServicesFromProviderEvent : INotification
{
    public required string Email { get; set; }
    public List<ServiceEntity>? ServiceEntities { get; set; }
}