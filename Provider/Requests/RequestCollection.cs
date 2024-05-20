using EventAndCommands.Commands;
using EventAndCommands.Commands.Provider;
using Kafka;
using Library.Entities;
using Library.Services;
using MediatR;

namespace Provider.Requests;

public class RequestCollection(IKafkaClient kafkaClient) : IRequestCollection
{
    // public async Task<string> AddProviderRequest(IMediator mediator, string topicName)
    // {
    //     var result = await new AddProviderCommandHandler(mediator, (kafkaClient as KafkaClient)!).Handle(
    //         new AddProviderCommand() { TopicName = topicName },
    //         new CancellationToken());
    //     return result;
    // }
    
    public async Task<string> AddProviderRequest(
        IMediator mediator, 
        ProviderService providerService, 
        ProviderEntity providerEntity)
    {
        var result = await new AddProviderCommandHandler(
            mediator, 
            (kafkaClient as KafkaClient)!,
            providerService, providerEntity)
            .Handle(
                new AddProviderCommand() { TopicName = providerEntity.KafkaTopic! }, 
                new CancellationToken());
        return result;
    }
}