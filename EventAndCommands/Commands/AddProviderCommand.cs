using MediatR;

namespace EventAndCommands.Commands;

public class AddProviderCommand: IRequest
{
    public string? ProviderName { get; set; }
}