using EventAndCommands.Commands;
using Kafka;
using MediatR;

namespace Provider.Requests;

public class RequestCollection(IKafkaClient kafkaClient) : IRequestCollection
{
    public async Task<string> CreateTopicNotification(IMediator mediator, string topicName)
    {
        //var result = await mediator.Send(new CreateTopicCommand() { TopicName = topicName });
        var result = await new CreateTopicCommandHandler(mediator, (kafkaClient as KafkaClient)!).Handle(
            new CreateTopicCommand() { TopicName = topicName },
            new CancellationToken());
        return result;
    }
    
}