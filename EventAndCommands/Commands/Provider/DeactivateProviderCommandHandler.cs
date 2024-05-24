using System.Text.Json;
using EventAndCommands.Persitency;
using Microsoft.Extensions.DependencyInjection;
using Quickwire.Attributes;

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
                Type = "DeactiveProvider_Success",
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
                Type = "GetProviders_Failed",
                Data = JsonSerializer.Serialize(new ProviderEntity())
            };
            await EventStore!.SaveAsync(@failEvent);
            return await Task.FromResult(request.ToJson());
        }
    }
}