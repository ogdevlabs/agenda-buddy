using System.Text.Json;
using EventAndCommands.Persitency;
using Microsoft.Extensions.DependencyInjection;
using Quickwire.Attributes;

namespace EventAndCommands.Commands.Provider;

[RegisterService(ServiceLifetime.Scoped)]
public class UpdateProviderCommandHandler(
    string email,
    IMediator mediator,
    ProviderService providerService,
    ProviderEntity providerEntity)
    : IRequestHandler<UpdateProviderCommand, string>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();
    public async Task<string> Handle(UpdateProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish( new UpdateProviderEvent
        {
            ProviderEntity = request.ProviderEntity
        }, cancellationToken);
        var record = await providerService
            .FindProviders(SupportTools<ProviderEntity>.FilterByEmail(email));
        if (record != null)
        {
            providerEntity.Id = record.Id;
            if (await providerService.UpdateProvider(record.Id.ToString(), providerEntity))
            {
                var @successEvent = new Event()
                {
                    Id = new ObjectId(),
                    TimeStamp = DateTime.UtcNow,
                    Type = "UpdateProvider_Success",
                    Data = JsonSerializer.Serialize(providerEntity)
                };
                await EventStore!.SaveAsync(@successEvent);
                return await Task.FromResult(providerEntity.ToJson());
            }
            var @failEvent = new Event()
            {
                Id = new ObjectId(),
                TimeStamp = DateTime.UtcNow,
                Type = "UpdateProvider_Failed",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await EventStore!.SaveAsync(@failEvent);
        }
        return await Task.FromResult(string.Empty);
    }
}