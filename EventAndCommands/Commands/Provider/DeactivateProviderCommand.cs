using Library.Entities;
using MediatR;

namespace EventAndCommands.Commands.Provider;

public class DeactivateProviderCommand : IRequest<string>
{
    public required ProviderEntity ProviderEntity { get; set; }
}