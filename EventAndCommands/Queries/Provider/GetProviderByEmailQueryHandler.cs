using System.Text.Json;
using EventAndCommands.Persitency;
using Microsoft.Extensions.DependencyInjection;
using Quickwire.Attributes;

namespace EventAndCommands.Queries.Provider;

[RegisterService(ServiceLifetime.Scoped)]
public class GetProviderByEmailQueryHandler(IMediator mediator, ProviderService providerService, string email)
    : IRequestHandler<GetProviderByEmailQuery, ProviderEntity>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();
    public async Task<ProviderEntity> Handle(GetProviderByEmailQuery request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetProviderByEmailEvent{Email = email}, cancellationToken);
        try
        {
            var filter = SupportTools<ProviderEntity>.FilterByEmail(email);
            var providerEntity = await providerService.FindProviders(filter);
            var @successEvent = new Event()
            {
                Id = new ObjectId(),
                TimeStamp = DateTime.UtcNow,
                Type = "GetProviderByEmail_Success",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await EventStore!.SaveAsync(@successEvent);
            return providerEntity;
        }
        catch
        {
            var @failEvent = new Event()
            {
                Id = new ObjectId(),
                TimeStamp = DateTime.UtcNow,
                Type = "GetProviderByEmail_Fail",
                Data = JsonSerializer.Serialize(new ProviderEntity())
            };
            await EventStore!.SaveAsync(@failEvent);
            return null!;
        }
    }
}