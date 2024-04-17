using MediatR;

namespace EventAndCommands.Events;

public class EventNotifications : INotification
{
    public string? Message { get; set; }
}