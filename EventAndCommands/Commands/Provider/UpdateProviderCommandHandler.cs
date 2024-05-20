using MediatR;
using MongoDB.Bson;

namespace EventAndCommands.Commands.Provider;

public class UpdateProviderCommandHandler(IMediator mediator) 
    : IRequestHandler<UpdateProviderCommand, string>
{
    public async Task<string> Handle(UpdateProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish( request, cancellationToken);
        return await Task.FromResult(request.ToJson());
    }
}