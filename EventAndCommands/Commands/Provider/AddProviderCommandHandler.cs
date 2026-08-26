namespace EventAndCommands.Commands.Provider;

public class AddProviderCommandHandler(
    IMediator mediator,
    IKafkaClient kafkaClient,
    ProviderService providerService,
    ProviderEntity providerEntity,
    IEventStore eventStore)
    : IRequestHandler<AddProviderCommand, string>
{

    private string TopicName { get; set; } = string.Empty;

    public async Task<string> Handle(AddProviderCommand request, CancellationToken cancellationToken)
    {
        var kafkaTopic = await CreateTopic(email: providerEntity.Email!);
        await mediator.Publish(new AddProviderEvent { ProviderName = TopicName }, cancellationToken);
        if (!string.IsNullOrEmpty(kafkaTopic) && !kafkaTopic.ToLower().StartsWith("exception"))
        {
            providerEntity.KafkaTopic = TopicName;
            await providerService.AddProviderAsync(providerEntity);
            var succesEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "AddProviderCommand",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await eventStore.SaveAsync(succesEvent);
            return TopicName;
        }

        if (kafkaTopic.ToLower().StartsWith("exception"))
        {
            var failEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = $"AddProviderCommand - {kafkaTopic}",
                Data = JsonSerializer.Serialize(providerEntity)
            };
            await eventStore.SaveAsync(failEvent);
            return kafkaTopic;
        }

        return string.Empty;
    }

    private async Task<string> CreateTopic(string email)
    {
        TopicName = KafkaHelper.CreateProviderTopicName(email);
        return await kafkaClient.CreateTopicIfNotExist(TopicName);
    }
}
