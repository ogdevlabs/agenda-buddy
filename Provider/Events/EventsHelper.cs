namespace Provider.Events;

public static class EventsHelper
{
    public static async Task<string> AddProviderEvent(
        IRequestCollection requestCollection,
        IMediator mediator,
        ProviderService providerService,
        ProviderEntity providerEntity)
    {
        var notificationResponse =
            await requestCollection.AddProviderRequest(mediator, providerService, providerEntity);
        return notificationResponse;
    }

    public static async Task<string> UpdateProviderEvent(
        string email,
        IRequestCollection requestCollection,
        IMediator mediator,
        ProviderService providerService,
        ProviderEntity providerEntity)
    {
        var notificationResponse =
            await requestCollection.UpdateProviderRequest(email, mediator, providerService, providerEntity);
        return notificationResponse;
    }

    public static async Task<IEnumerable<ProviderEntity>> GetProvidersEvent(IRequestCollection requestCollection, IMediator mediator,
        ProviderService providerService)
    {
        var notificationResponse = await requestCollection.GetProvidersRequest(mediator, providerService);
        return notificationResponse;
    }

    public static async Task<ProviderEntity> GetProviderByEmail(IRequestCollection requestCollection,
        IMediator mediator, ProviderService providerService, string email)
    {
        var notificationResponse = await requestCollection.GetProviderByEmail(mediator, providerService, email);
        return notificationResponse;
    }
}