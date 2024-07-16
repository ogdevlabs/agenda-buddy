namespace Customer.Requests;

public interface IKafkaRequestCollection
{
    public Task<string> GenerateSubscriptionMessage(IProducerAccessor producerAccessor,
        CustomerSubscribedToProviderEntity customerSubscribedToProviderEntity, string producerName);
}