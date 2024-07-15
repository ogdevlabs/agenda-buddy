using System.ComponentModel.DataAnnotations;

namespace EventAndCommands.Events.Kafka;

public class ProviderCreatedEvent : INotification
{
    [EmailAddress]
    public required string Email { get; set; }
}