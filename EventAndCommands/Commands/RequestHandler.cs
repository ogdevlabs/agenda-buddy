using EventAndCommands.Events;
using MediatR;

namespace EventAndCommands.Commands;

public class RequestHandler(IMediator mediator) : IRequestHandler<Request>
{
    public async Task<Unit> Handle(Request request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new EventNotifications() { Message = request.Message }, cancellationToken);
        return Unit.Value;
    }
}

