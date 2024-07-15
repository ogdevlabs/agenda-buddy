namespace Provider.Events;

public static class KafkaEvents
{
    public static async Task<string> CreateProviderTopicEvent(IMediator mediator, ProviderCreatedEvent @event,
        IKafkaRequestCollection kafkaRequestCollection, string email, bool flag)
    {
        var notificationResponse = await kafkaRequestCollection.CreateProviderTopic(mediator, @event, email, flag);
        return notificationResponse;
    }
}