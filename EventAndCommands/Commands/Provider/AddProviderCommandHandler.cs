using EventAndCommands.Events.Provider;
using Kafka;
using Library.Entities;
using Library.Services;
using MediatR;

namespace EventAndCommands.Commands.Provider;

public class AddProviderCommandHandler : IRequestHandler<AddProviderCommand, string>
{
    private readonly IMediator _mediator;
    private readonly KafkaClient _kafkaClient;
    private readonly ProviderService _providerService;
    private readonly ProviderEntity _providerEntity;

    public AddProviderCommandHandler(IMediator mediator, KafkaClient kafkaClient)
    {
        _mediator = mediator;
        _kafkaClient = kafkaClient;
    }
    
    public AddProviderCommandHandler(
        IMediator mediator, 
        KafkaClient kafkaClient, 
        ProviderService providerService,
        ProviderEntity providerEntity)
    {
        _mediator = mediator;
        _kafkaClient = kafkaClient;
        _providerService = providerService;
        _providerEntity = providerEntity;
    }

    public async Task<string> Handle(AddProviderCommand request, CancellationToken cancellationToken)
    {
        await _mediator.Publish(new AddProviderEvent { ProviderName = request.TopicName }, cancellationToken);
        var kafkaTopic =await _kafkaClient.CreateTopicIfNotExist(request.TopicName);
        if (!string.IsNullOrEmpty(kafkaTopic))
        {
            await _providerService.AddProvider(_providerEntity);
            return await Task.FromResult(request.TopicName);
        }
        return await Task.FromResult(string.Empty);
    }
}