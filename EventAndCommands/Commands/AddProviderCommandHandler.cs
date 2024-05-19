using EventAndCommands.Events;
using Kafka;
using MediatR;

namespace EventAndCommands.Commands;

public class AddProviderTopicCommandHandler(IMediator mediator, KafkaClient kafkaClient)
    : IRequestHandler<AddProviderTopicCommand, string>
{
    public async Task<string> Handle(AddProviderTopicCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new AddProviderEvent() { ProviderName = request.TopicName }, cancellationToken);
        await kafkaClient.CreateTopicIfNotExist(request.TopicName);
        return await Task.FromResult(request.TopicName);
    }
}