using System.Text.Json;
using EventAndCommands.Events.Services;
using EventAndCommands.Persitency;
using Quickwire.Attributes;

namespace EventAndCommands.Commands.Services;

public class
    AddServicesToProviderCommandHandler(
        IMediator mediator,
        ProviderService providerService, 
        List<ServiceEntity> serviceEntities,
        string email)
    : IRequestHandler<AddServicesToProviderCommand, string>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();

    public async Task<string> Handle(AddServicesToProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new AddServicesToProviderEvent
        {
            Email = email,
            ServiceEntities = serviceEntities
        }, cancellationToken);

        var provider = await providerService.FindProviders(SupportTools<ProviderEntity>.FilterByEmail(email));
        if (provider != null)
        {
            provider.ServiceEntities.AddRange(serviceEntities);
            var updateResult = await providerService.UpdateProvider(provider.Id.ToString(), provider);
            if (updateResult)
            {
                var @successEvent = new Event()
                {
                    Id = provider.Id,
                    TimeStamp = DateTime.UtcNow,
                    Status = "Success",
                    Type = "AddServicesToProviderCommand",
                    Data = JsonSerializer.Serialize(provider)
                };
                await EventStore!.SaveAsync(@successEvent);
                return await Task.FromResult(provider.ToJson());
            }
        }
        else
        {
            var @failEvent = new Event()
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "AddServicesToProviderCommand",
                Data = JsonSerializer.Serialize(new ProviderEntity())
            };
            await EventStore!.SaveAsync(@failEvent);
        }

        return await Task.FromResult(string.Empty);
    }
}