namespace Customer.Requests;

public class KafkaRequestCollection : IKafkaRequestCollection
{
    public async Task<string> GenerateSubscriptionMessage(IProducerAccessor producerAccessor,
        CustomerSubscribedToProviderEntity customerSubscribedToProviderEntity, string producerName)
    {
        var producer = producerAccessor.GetProducer(producerName);
        await producer.ProduceAsync(producerName, 
            $"ProviderEmail:{customerSubscribedToProviderEntity.ProviderEmail}{System.Environment.NewLine}" +
            $"CustomerEmail:{customerSubscribedToProviderEntity.CustomerEmail}{System.Environment.NewLine}" +
            $"Action:Subscription");
        return JsonSerializer.Serialize(customerSubscribedToProviderEntity);
    }
}