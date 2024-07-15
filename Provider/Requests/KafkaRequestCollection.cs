namespace Provider.Requests;

public class KafkaRequestCollection : IKafkaRequestCollection
{
    public async Task<string> CreateProviderTopic(IMediator mediator, ProviderCreatedEvent @event, string providerEmail,
        bool providerFlag = true)
    {
        var result = await new ProviderCreateTopicCommandHandler(mediator, providerEmail, providerFlag).Handle(
            new ProviderCreateTopicCommand
            {
                Event = @event
            }, new CancellationToken());
        return result;
    }
}