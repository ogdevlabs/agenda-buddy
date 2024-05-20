using Library.Entities;
using Library.Services;
using MediatR;
using Provider.Requests;

namespace Provider.Extensions;

public static class KafkaExtension
{
    // public static async Task<string> AddProviderEvent(IRequestCollection requestCollection, IMediator mediator, string providerTopicName)
    // {
    //     var notificationResponse = await requestCollection.AddProviderRequest(mediator, providerTopicName);
    //     return notificationResponse;
    // }
    
    public static async Task<string> AddProviderEvent(
        IRequestCollection requestCollection, 
        IMediator mediator,
        ProviderService providerService,
        ProviderEntity providerEntity)
    {
        var notificationResponse = await requestCollection.AddProviderRequest(mediator, providerService, providerEntity);
        return notificationResponse;
    }
}