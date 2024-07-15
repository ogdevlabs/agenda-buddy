namespace Provider.Requests;

public interface IKafkaRequestCollection
{
    public Task<string> CreateProviderTopic(IMediator mediator, ProviderCreatedEvent @event, string providerEmail, bool providerFlag);
}