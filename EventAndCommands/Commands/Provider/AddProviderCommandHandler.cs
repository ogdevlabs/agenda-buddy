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
        if (!string.IsNullOrEmpty(kafkaTopic) && (!kafkaTopic.ToLower().StartsWith("exception")))
        {
            await providerService.AddProvider(providerEntity);
            var @succesEvent = new Event()
            {
                Id = providerEntity.Id,
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "AddProviderCommand",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await EventStore.SaveAsync(@succesEvent);
            return await Task.FromResult(request.TopicName);
        }

        if (kafkaTopic.ToLower().StartsWith("exception"))
        {
            var @failEvent = new Event()
            {
                Id = providerEntity.Id,
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = $"AddProviderCommand - {kafkaTopic}",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await EventStore.SaveAsync(@failEvent);
            return await Task.FromResult(kafkaTopic);
        }
        return await Task.FromResult(string.Empty);
    }
}