using MediatR;

namespace EventAndCommands.Events;

public class ProviderAddedEvent: INotification
{
    public string? ProviderName { get; set; }
}