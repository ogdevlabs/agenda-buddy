namespace Customer.Events;

public static class KafkaEvents
{
    public static async Task<string> CreateCustomerTopicEvent(IMediator mediator, CustomerCreatedEvent @event,
        IKafkaRequestCollection kafkaRequestCollection, string email, bool flag)
    {
        var notificationResponse = await kafkaRequestCollection.CreateCustomerTopic(mediator, @event, email, flag);
        return notificationResponse;
    }
}