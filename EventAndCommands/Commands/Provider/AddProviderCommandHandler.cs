using Kafka.Support;

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

    private string TopicName { get; set; } = string.Empty;

    public async Task<string> Handle(AddProviderCommand request, CancellationToken cancellationToken)
    {
        var kafkaTopic = await CreateTopic(email: providerEntity.Email!);
        await mediator.Publish(new AddProviderEvent { ProviderName = TopicName }, cancellationToken);
        if (!string.IsNullOrEmpty(kafkaTopic) && !kafkaTopic.ToLower().StartsWith("exception"))
        {
            providerEntity.KafkaTopic = TopicName;
            await providerService.AddProvider(providerEntity);
            var succesEvent = new Event
            {
                Id = providerEntity.Id,
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "AddProviderCommand",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await EventStore.SaveAsync(succesEvent);
            return await Task.FromResult(TopicName);
        }

        if (kafkaTopic.ToLower().StartsWith("exception"))
        {
            var failEvent = new Event
            {
                Id = providerEntity.Id,
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = $"AddProviderCommand - {kafkaTopic}",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await EventStore.SaveAsync(failEvent);
            return await Task.FromResult(kafkaTopic);
        }

        return await Task.FromResult(string.Empty);
    }

    private async Task<string> CreateTopic(string email)
    {
        TopicName = KafkaHelper.CreateProviderTopicName(email);
        return await kafkaClient.CreateTopicIfNotExist(TopicName);
    }
}