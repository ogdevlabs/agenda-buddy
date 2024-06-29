namespace EventAndCommands.Commands.Customer;

[RegisterService(ServiceLifetime.Scoped)]
public class AddCustomerCommandHandler(
    IMediator mediator,
    KafkaClient kafkaClient,
    CustomerService customerService,
    CustomerEntity customerEntity) : IRequestHandler<AddCustomerCommand, string>
{
    [InjectService] private IEventStore EventStore { get; } = new EventStore();
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
            await EventStore.SaveAsync(succesEvent);
            return await Task.FromResult(TopicName);
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
            await EventStore.SaveAsync(failEvent);
            return await Task.FromResult(kafkaTopic);
        }
        return await Task.FromResult(string.Empty);
    }

    private async Task<string> CreateTopic(string email)
    {
        TopicName= KafkaHelper.CreateCustomerTopicName(email);
        return await kafkaClient.CreateTopicIfNotExist(TopicName);
    }
}