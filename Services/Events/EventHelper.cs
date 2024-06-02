namespace Services.Events;

public static class EventHelper
{
    public static async Task<IEnumerable<ServiceEntity>> GetServicesFromProviderEvent(
        IRequestCollection requestCollection, IMediator mediator, ProviderService providerService, string email)
    {
        var notificationResponse = 
            await requestCollection.GetServicesFromProvider(mediator, providerService, email);
        return notificationResponse;
    }

    public static async Task<string> AddServicesToProviderEvent(
        IRequestCollection requestCollection, IMediator mediator, ProviderService providerService,
        List<ServiceEntity> serviceEntities, string email)
    {
        var notificationResponse =
            await requestCollection.AddServicesToProvider(mediator, providerService, serviceEntities, email);
        return notificationResponse;
    }
}