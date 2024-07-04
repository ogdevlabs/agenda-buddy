namespace Services.Events;

public static class EventHelper
{
    public static async Task<List<ServiceEntity>> GetServicesFromProviderEvent(
        IRequestCollection requestCollection, IMediator mediator, ProviderService providerService, string email)
    {
        var notificationResponse =
            await requestCollection.GetServicesFromProvider(mediator, providerService, email);
        return notificationResponse;
    }

    public static async Task<ProviderEntity> AddServicesToProviderEvent(
        IRequestCollection requestCollection, IMediator mediator, ProviderService providerService,
        List<ServiceEntity> serviceEntities, string email)
    {
        var notificationResponse =
            await requestCollection.AddServicesToProvider(mediator, providerService, serviceEntities, email);
        return notificationResponse;
    }

    public static async Task<ProviderEntity> UpdateServicesFromProviderEvent(IRequestCollection requestCollection,
        IMediator mediator, ProviderService providerService, List<ServiceEntity> serviceEntities, string email)
    {
        var notificationResponse =
            await requestCollection.UpdateServicesFromProvider(mediator, providerService, serviceEntities, email);
        return notificationResponse;
    }
}