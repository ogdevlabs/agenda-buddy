namespace EventAndCommands.Commands.Provider;

public class DeactivateProviderCommandHandler (IMediator mediator) 
    : IRequestHandler<DeactivateProviderCommand, string>
{
    public async Task<string> Handle(DeactivateProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(request, cancellationToken);
        return await Task.FromResult(request.ToJson());
    }
}