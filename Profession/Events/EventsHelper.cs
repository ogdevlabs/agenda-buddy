using MediatR;
using Profession.Requests;

namespace Profession.Events;

public static class EventsHelper
{
    public static async Task<ProfessionEntity> AddProfessionEvent(IRequestCollection requestCollection,
        IMediator mediator, ProfessionService professionService, ProfessionEntity professionEntity)
    {
        var notificationResponse =
            await requestCollection.AddProfessionRequest(mediator, professionService, professionEntity);
        return notificationResponse;
    }
    
}