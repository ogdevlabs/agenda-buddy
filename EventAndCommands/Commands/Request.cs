using MediatR;

namespace EventAndCommands.Commands;

public class Request: IRequest
{
    public string? Message { get; set; }
}