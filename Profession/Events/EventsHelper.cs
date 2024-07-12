namespace Profession.Events;

public static class EventsHelper
{
    public static async Task<ProviderEntity> UpdateProfessionsFromProviderEvent(IRequestCollection requestCollection,
        IMediator mediator, ProviderService providerService, List<ProfessionEntity> professionEntities, string email)
    {
        var notificationResponse =
            await requestCollection.UpdateProfessionsFromProvider(mediator, providerService, professionEntities, email);
        return notificationResponse;
    }

    public static async Task<ProfessionEntity> AddProfessionEvent(IRequestCollection requestCollection,
        IMediator mediator, ProfessionService professionService, ProfessionEntity professionEntity)
    {
        var notificationResponse =
            await requestCollection.AddProfessionRequest(mediator, professionService, professionEntity);
        return notificationResponse;
    }

    public static async Task<List<ProfessionEntity>> GetAllProfessionsEvent(IRequestCollection requestCollection,
        IMediator mediator, ProfessionService professionService)
    {
        var notificationResponse =
            await requestCollection.GetProfessionsRequest(mediator, professionService);
        return notificationResponse;
    }

    public static async Task<ProfessionEntity> GetProfessionByNameEvent(IRequestCollection requestCollection,
        IMediator mediator, ProfessionService professionService, string name)
    {
        var notificationResponse =
            await requestCollection.GetProfessionByNameRequest(mediator, professionService, name);
        return notificationResponse;
    }
}