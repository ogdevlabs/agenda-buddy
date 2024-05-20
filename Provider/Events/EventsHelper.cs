using Library.Entities;
using Library.Services;
using MediatR;
using Provider.Requests;

namespace Provider.Events;

public static class EventsHelper
{
    public static async Task<string> AddProviderEvent(
        IRequestCollection requestCollection, 
        IMediator mediator,
        ProviderService providerService,
        ProviderEntity providerEntity)
    {
        var notificationResponse = await requestCollection.AddProviderRequest(mediator, providerService, providerEntity);
        return notificationResponse;
    }
    
    public static async Task<string> UpdateProviderEvent(
        string email,
        IRequestCollection requestCollection, 
        IMediator mediator,
        ProviderService providerService,
        ProviderEntity providerEntity)
    {
        var notificationResponse = await requestCollection.UpdateProviderRequest(email, mediator, providerService, providerEntity);
        return notificationResponse;
    }
    
}