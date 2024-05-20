using Library.Entities;
using MediatR;

namespace EventAndCommands.Events.Provider;

public class DeactivateProviderEvent : INotification
{
    public required ProviderEntity ProviderEntity { get; set; }
}