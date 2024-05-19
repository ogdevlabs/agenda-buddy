using EventAndCommands.Commands;
using Kafka;
using MediatR;

namespace Provider.Requests;

public class RequestCollection(IKafkaClient kafkaClient) : IRequestCollection
{
    public async Task<string> AddProviderRequest(IMediator mediator, string topicName)
    {
        var result = await new AddProviderCommandHandler(mediator, (kafkaClient as KafkaClient)!).Handle(
            new AddProviderCommand() { TopicName = topicName },
            new CancellationToken());
        return result;
    }
    
}