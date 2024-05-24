using System.Text.Json;
using EventAndCommands.Persitency;
using Microsoft.Extensions.DependencyInjection;
using Quickwire.Attributes;

namespace EventAndCommands.Queries.Provider;

[RegisterService(ServiceLifetime.Scoped)]
public class GetProvidersQueryHandler(IMediator mediator, ProviderService providerService)
    : IRequestHandler<GetProvidersQuery, IEnumerable<ProviderEntity>>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();


    public async Task<IEnumerable<ProviderEntity>> Handle(GetProvidersQuery request,
        CancellationToken cancellationToken)
    {
        await mediator.Publish(new GetAllProvidersEvent(), cancellationToken);
        try
        {
            var providerList = await providerService.GetAllProviders();
            var providerEntities = providerList.ToList();
            var @successEvent = new Event()
            {
                Id = new ObjectId(),
                TimeStamp = DateTime.UtcNow,
                Type = "GetProviders_Success",
                Data = JsonSerializer.Serialize(providerEntities)
            };
            await EventStore!.SaveAsync(@successEvent);
            return await Task.FromResult(providerEntities);
        }
        catch
        {
            var @failEvent = new Event()
            {
                Id = new ObjectId(),
                TimeStamp = DateTime.UtcNow,
                Type = "GetProviders_Failed",
                Data = JsonSerializer.Serialize(new List<ProviderEntity>())
            };
            await EventStore!.SaveAsync(@failEvent);
            return await Task.FromResult(new List<ProviderEntity>());
        }
    }
}