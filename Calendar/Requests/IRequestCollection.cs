namespace Calendar.Requests;

public interface IRequestCollection
{
    public Task<IEnumerable<DateTime>> CheckCalendarAvailabilityRequest(IMediator mediator,
        ProviderService providerService, CalendarService calendarService, string email);

    public Task<IEnumerable<AppointmentEntity>> CheckCalendarAppointmentsRequest(IMediator mediator,
        ProviderService providerService, CalendarService calendarService, string email);
}