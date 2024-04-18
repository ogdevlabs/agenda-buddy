using EventAndCommands.Events;
using MediatR;

namespace EventAndCommands.Commands;

public class RequestHandlerGenerics<TRequest, TResponse>(IMediator mediator)
    : IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new EventNotificationsGenerics<TRequest>(),
            cancellationToken);
        return default;
    }

   
}