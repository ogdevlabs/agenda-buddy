namespace EventAndCommands.Commands.Kafka;

public class CustomerCreateTopicCommandHandler(IMediator mediator, string email, bool flag)
    : IRequestHandler<CustomerCreateTopicCommand, string>
{
    private string _topicName = string.Empty;
    [InjectService] private IEventStore? EventStore { get; } = new EventStore();

    public async Task<string> Handle(CustomerCreateTopicCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new CreateTopicEvent
        {
            Email = request.Event.Email
        }, cancellationToken);
        _topicName = flag ? KafkaHelper.CreateProviderTopicName(email) : KafkaHelper.CreateCustomerTopicName(email);
        
        var succesEvent = new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = "CustomerCreateTopicCommand",
            Data = JsonSerializer.Serialize(request)
        };
        await EventStore!.SaveAsync(succesEvent);

        return _topicName;
    }
}