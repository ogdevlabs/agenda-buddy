namespace Calendar.Events;

public static class EventHelper
{
    public static async Task<List<DateTime>> CheckCalendarAvailabilityEvent(
        IRequestCollection requestCollection, IMediator mediator, ProviderService providerService,
        CalendarService calendarService, string email)
    {
        var notificationResponse =
            await requestCollection.CheckCalendarAvailabilityRequest(mediator, providerService, calendarService, email);
        return notificationResponse;
    }

    public static async Task<List<AppointmentEntity>> CheckCalendarAppointmentsEvent(
        IRequestCollection requestCollection, IMediator mediator, ProviderService providerService,
        CalendarService calendarService, string email)
    {
        var notificationResponse =
            await requestCollection.CheckCalendarAppointmentsRequest(mediator, providerService, calendarService, email);
        return notificationResponse;
    }
}
