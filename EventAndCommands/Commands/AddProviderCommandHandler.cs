using EventAndCommands.Events;
using MediatR;

namespace EventAndCommands.Commands;

public class AddProviderCommandHandler(IMediator mediator) : IRequestHandler<AddProviderCommand>
{
    public async Task<Unit> Handle(AddProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(
            new ProviderAddedEvent { ProviderName = request.ProviderName! }, 
            cancellationToken);

        return Unit.Value;
    }
}