namespace AgendaBuddy.EventAndCommands.Commands.Customer;

public class AddCustomerCommandHandler(
    IMediator mediator,
    KafkaClient kafkaClient,
    CustomerService customerService,
    CustomerEntity customerEntity,
    IEventStore eventStore) : IRequestHandler<AddCustomerCommand, string>
{
    private string TopicName { get; set; } = string.Empty;

    public async Task<string> Handle(AddCustomerCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new AddCustomerEvent { CustomerEntity = customerEntity }, cancellationToken);
        var kafkaTopic = await CreateTopic(email: customerEntity.Email!);
        if (!string.IsNullOrEmpty(kafkaTopic) && !kafkaTopic.ToLower().StartsWith("exception"))
        {
            customerEntity.KafkaTopic = TopicName;
            await customerService.AddCustomerAsync(customerEntity);
            var succesEvent = new Event
            {
                Id = ObjectId.GenerateNewId(),
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "AddCustomerCommand",
                Data = JsonSerializer.Serialize(customerEntity)
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
                Type = $"AddCustomerCommand - {kafkaTopic}",
                Data = JsonSerializer.Serialize(customerEntity)
            };
            await eventStore.SaveAsync(failEvent);
            return kafkaTopic;
        }
        return string.Empty;
    }

    private async Task<string> CreateTopic(string email)
    {
        TopicName = KafkaHelper.CreateCustomerTopicName(email);
        return await kafkaClient.CreateTopicIfNotExist(TopicName);
    }
}
