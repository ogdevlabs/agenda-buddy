using EventAndCommands.Commands.Provider;
using Kafka;
using Library.Entities;
using Library.Services;
using MediatR;

namespace Provider.Requests;

public class RequestCollection(IKafkaClient kafkaClient) : IRequestCollection
{
    public async Task<string> AddProviderRequest(
        IMediator mediator, 
        ProviderService providerService, 
        ProviderEntity providerEntity)
    {
        var result = await new AddProviderCommandHandler(
            mediator, 
            (kafkaClient as KafkaClient)!,
            providerService, 
            providerEntity)
            .Handle(
                new AddProviderCommand { TopicName = providerEntity.KafkaTopic! }, 
                new CancellationToken());
        return result;
    }
    
    public async Task<string> UpdateProviderRequest(
        string email,
        IMediator mediator, 
        ProviderService providerService, 
        ProviderEntity providerEntity)
    {
        var result = await new UpdateProviderCommandHandler(
                email,
                mediator,
                providerService, 
                providerEntity)
            .Handle(
                new UpdateProviderCommand { ProviderEntity = providerEntity }, 
                new CancellationToken());
        return result;
    } 
}