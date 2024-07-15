namespace Customer.Requests;

public interface IKafkaRequestCollection
{
    public Task<string> CreateCustomerTopic(IMediator mediator, CustomerCreatedEvent @event, string customerEmail,
        bool providerFlag);
}