using MediatR;

namespace EventAndCommands.Events;

public class EventNotificationsGenerics<T> : INotification
{
    public T? Message { get; set; }
}
