namespace Calendar.Requests;

public interface IRequestCollection
{
    public Task<List<DateTime>> CheckCalendarAvailabilityRequest(IMediator mediator,
        ProviderService providerService, CalendarService calendarService, string email);

    public Task<List<AppointmentEntity>> CheckCalendarAppointmentsRequest(IMediator mediator,
        ProviderService providerService, CalendarService calendarService, string email);
}