using MediatR;

namespace EventAndCommands.Events;

public class AddProviderEvent: INotification
{
    public string? ProviderName { get; set; }
}