using Calendar.Requests;

namespace Calendar.Events;

public static class EventHelper
{
    public static async Task<IEnumerable<AppointmentEntity>> CheckCalendarAvailabilityEvent(
        IRequestCollection requestCollection, IMediator mediator, ProviderService providerService, string email)
    {
        var notificationResponse =
            await requestCollection.CheckCalendarAvailabilityRequest(mediator, providerService, email);
        return notificationResponse;
    }
}