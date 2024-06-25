namespace Calendar.Requests;

public class RequestCollection : IRequestCollection
{
    public async Task<IEnumerable<DateTime>> CheckCalendarAvailabilityRequest(IMediator mediator,
        ProviderService providerService, CalendarService calendarService, string email)
    {
        var result =
            await new CheckCalendarAvailabilityQueryHandler(mediator, providerService, email)
                .Handle(new CheckCalendarAvailabilityQuery(), new CancellationToken());
        return result;
    }

    public async Task<IEnumerable<AppointmentEntity>> CheckCalendarAppointmentsRequest(IMediator mediator,
        ProviderService providerService, CalendarService calendarService, string email)
    {
        var result =
            await new CheckCalendarAppointmentsQueryHandler(mediator, providerService, email)
                .Handle(new CheckCalendarAppointmentsQuery(), new CancellationToken());
        return result;
    }
}