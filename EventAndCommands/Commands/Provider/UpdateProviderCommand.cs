using Library.Entities;
using MediatR;

namespace EventAndCommands.Commands.Provider;

public class UpdateProviderCommand : IRequest<string>
{
    public required ProviderEntity Provider { get; set; }
}