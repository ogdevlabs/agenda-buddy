namespace EventAndCommands.Commands.Kafka;

public class SubscribeToProviderCommandHandler(
    IMediator mediator,
    CustomerSubscribedToProviderEntity customerSubscribedToProviderEntity,
    KafkaProducer kafkaProducer)
    : IRequestHandler<SubscribeToProviderCommand, string>
{
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();

    public async Task<string> Handle(SubscribeToProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(
            new SubscribeToProviderEvent { CustomerSubscribedToProviderEntity = customerSubscribedToProviderEntity },
            cancellationToken);
        var topic = KafkaHelper.CreateProviderTopicName(customerSubscribedToProviderEntity.ProviderEmail);
        var response = await kafkaProducer.ProducerAsync(topic,
            KafkaHelper.SubscribedToProviderMessage(customerSubscribedToProviderEntity.CustomerEmail));
        if (response is not null)
        {
            var successEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "SubscribeToProviderCommand",
                Data = JsonSerializer.Serialize(customerSubscribedToProviderEntity)
            };
            await EventStore!.SaveAsync(successEvent);
        }
        else
        {
            var failedEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = "SubscribeToProviderCommand",
                Data = JsonSerializer.Serialize(customerSubscribedToProviderEntity)
            };
            await EventStore!.SaveAsync(failedEvent);
        }

        return response!;
    }
}