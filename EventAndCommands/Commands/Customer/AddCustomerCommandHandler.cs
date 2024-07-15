namespace EventAndCommands.Commands.Customer;

[RegisterService(ServiceLifetime.Scoped)]
public class AddCustomerCommandHandler(
    IMediator mediator,
    CustomerService customerService,
    CustomerEntity customerEntity) : IRequestHandler<AddCustomerCommand, string>
{
    [InjectService] private IEventStore EventStore { get; } = new EventStore();
    private string TopicName { get; set; } = string.Empty;

    public async Task<string> Handle(AddCustomerCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new AddCustomerEvent(), cancellationToken);
        TopicName = customerEntity.KafkaTopic!;
        customerEntity.KafkaTopic = TopicName;
        await customerService.AddCustomerAsync(customerEntity);
        var successEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = "AddCustomerCommand",
            Data = JsonSerializer.Serialize(customerEntity)
        };
        await EventStore.SaveAsync(successEvent);
        return await Task.FromResult(TopicName);
    }
}