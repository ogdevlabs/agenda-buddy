using System.Text.Json;
using EventAndCommands.Persitency;
using Microsoft.Extensions.DependencyInjection;
using Quickwire.Attributes;

namespace EventAndCommands.Commands.Provider;

[RegisterService(ServiceLifetime.Scoped)]
public class AddProviderCommandHandler(
    IMediator mediator,
    KafkaClient kafkaClient,
    ProviderService providerService,
    ProviderEntity providerEntity)
    : IRequestHandler<AddProviderCommand, string>
{
    [InjectService] private IEventStore EventStore { get; } = new EventStore();

    public async Task<string> Handle(AddProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new AddProviderEvent { ProviderName = request.TopicName }, cancellationToken);
        var kafkaTopic = await kafkaClient.CreateTopicIfNotExist(request.TopicName);
        if (!string.IsNullOrEmpty(kafkaTopic))
        {
            await providerService.AddProvider(providerEntity);

            var @succesEvent = new Event()
            {
                Id = providerEntity.Id,
                TimeStamp = DateTime.UtcNow,
                Type = "AddProvider_Success",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await EventStore.SaveAsync(@succesEvent);
            return await Task.FromResult(request.TopicName);
        }

        var @failEvent = new Event()
        {
            Id = providerEntity.Id,
            TimeStamp = DateTime.UtcNow,
            Type = "AddProvider_Failed",
            Data = JsonSerializer.Serialize(providerEntity)
        };
        await EventStore.SaveAsync(@failEvent);
        return await Task.FromResult(string.Empty);
    }
}