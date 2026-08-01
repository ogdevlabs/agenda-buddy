namespace EventAndCommands.Commands.Provider;

public class DeactivateProviderCommandHandler(IMediator mediator, IEventStore eventStore)
    : IRequestHandler<DeactivateProviderCommand, string>
{

    //TODO
    //Pending of implementation
    public async Task<string> Handle(DeactivateProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(request, cancellationToken);
        try
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "DeactivateProviderCommand",
                Data = JsonSerializer.Serialize(new ProviderEntity())
            };
            await eventStore.SaveAsync(successEvent);
            return await Task.FromResult(request.ToJson());
        }
        catch
        {
            var failEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "DeactivateProviderCommand",
                Data = JsonSerializer.Serialize(new ProviderEntity())
            };
            await eventStore.SaveAsync(failEvent);
            return await Task.FromResult(request.ToJson());
        }
    }
}