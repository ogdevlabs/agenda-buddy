using EventAndCommands.Events;
using Kafka;
using MediatR;

namespace EventAndCommands.Commands;

public class AddProviderCommandHandler(IMediator mediator, KafkaClient kafkaClient)
    : IRequestHandler<AddProviderCommand, string>
{
    public async Task<string> Handle(AddProviderCommand request, CancellationToken cancellationToken)
    {
        await mediator.Publish(new AddProviderEvent() { ProviderName = request.TopicName }, cancellationToken);
        await kafkaClient.CreateTopicIfNotExist(request.TopicName);
        return await Task.FromResult(request.TopicName);
    }
}