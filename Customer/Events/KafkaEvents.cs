namespace Customer.Events;

public static class KafkaEvents
{
    public static async Task<string> SubscribeToProviderEvent(IKafkaRequestCollection kafkaRequestCollection,
        IProducerAccessor producerAccessor, CustomerSubscribedToProviderEntity customerSubscribedToProviderEntity, string producerName)
    {
        var notificationResponse =
            await kafkaRequestCollection.GenerateSubscriptionMessage(producerAccessor, customerSubscribedToProviderEntity, producerName);
        return notificationResponse;
    }
}