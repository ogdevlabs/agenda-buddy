using EventAndCommands.Events.Customer;
using Kafka.Support;
using Library.Entities;

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
            await customerService.AddCustomer(customerEntity);
            var succesEvent = new Event
            {
                Id = customerEntity.Id,
                TimeStamp = DateTime.UtcNow,
                Status = "Success",
                Type = "AddProviderCommand",
                Data = JsonSerializer.Serialize(customerEntity)
            };
            await EventStore.SaveAsync(succesEvent);
            return await Task.FromResult(TopicName);
        }
        if (kafkaTopic.ToLower().StartsWith("exception"))
        {
            var failEvent = new Event
            {
                Id = customerEntity.Id,
                TimeStamp = DateTime.UtcNow,
                Status = "Failed",
                Type = $"AddProviderCommand - {kafkaTopic}",
                Data = JsonSerializer.Serialize(customerEntity)
            };
            await EventStore.SaveAsync(failEvent);
            return await Task.FromResult(kafkaTopic);
        }
        return await Task.FromResult(string.Empty);
    }

    private async Task<string> CreateTopic(string email)
    {
        TopicName= KafkaHelper.CreateTopicName(email);
        return await kafkaClient.CreateTopicIfNotExist(TopicName);
    }
}