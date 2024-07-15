namespace Customer.Requests;

public class KafkaRequestCollection : IKafkaRequestCollection
{
    public async Task<string> CreateCustomerTopic(IMediator mediator, CustomerCreatedEvent @event, string customerEmail,
        bool providerFlag)
    {
        var result = await new CustomerCreateTopicCommandHandler(mediator, customerEmail, providerFlag).Handle(
            new CustomerCreateTopicCommand
            {
                Event = @event
            }, new CancellationToken());
        return result;
    }
}