using MediatR;
using Provider.Requests;

namespace Provider.Extensions;

public static class KafkaExtension
{
    public static async Task<string> AddProviderEvent(IRequestCollection requestCollection, IMediator mediator, string providerTopicName)
    {
        var notificationResponse = await requestCollection.AddProviderRequest(mediator, providerTopicName);
        return notificationResponse;
    }
}