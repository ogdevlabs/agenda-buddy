using EventAndCommands.Events;
using Kafka;
using MediatR;

namespace EventAndCommands.Commands;

public class CreateTopicCommandHandler(IMediator mediator, KafkaClient kafkaClient)
    : IRequestHandler<CreateTopicCommand, string>
{
    public async Task<string> Handle(CreateTopicCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new EventNotifications() { Message = request.TopicName }, cancellationToken);
        await kafkaClient.CreateTopicIfNotExist(request.TopicName);
        return await Task.FromResult(request.TopicName);
    }
}