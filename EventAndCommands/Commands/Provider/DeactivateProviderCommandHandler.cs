namespace EventAndCommands.Commands.Provider;

[RegisterService(ServiceLifetime.Scoped)]
public class DeactivateProviderCommandHandler (IMediator mediator) 
    : IRequestHandler<DeactivateProviderCommand, string>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();
    
    //TODO
    //Pending of implementation
    public async Task<string> Handle(DeactivateProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(request, cancellationToken);
        try
        {
            var @successEvent = new Event()
            {
                Id = new ObjectId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "DeactivateProviderCommand",
                Data = JsonSerializer.Serialize(new ProviderEntity())
            };
            await EventStore!.SaveAsync(@successEvent);
            return await Task.FromResult(request.ToJson());
        }
        catch
        {
            var @failEvent = new Event()
            {
                Id = new ObjectId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "DeactivateProviderCommand",
                Data = JsonSerializer.Serialize(new ProviderEntity())
            };
            await EventStore!.SaveAsync(@failEvent);
            return await Task.FromResult(request.ToJson());
        }
    }
}