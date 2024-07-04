namespace Calendar.Requests;

public class RequestCollection : IRequestCollection
{
    public async Task<List<DateTime>> CheckCalendarAvailabilityRequest(IMediator mediator,
        ProviderService providerService, CalendarService calendarService, string email)
    {
        var result =
            await new CheckCalendarAvailabilityQueryHandler(mediator, providerService, email)
                .Handle(new CheckCalendarAvailabilityQuery(), new CancellationToken());
        return result;
    }

    public async Task<List<AppointmentEntity>> CheckCalendarAppointmentsRequest(IMediator mediator,
        ProviderService providerService, CalendarService calendarService, string email)
    {
        var result =
            await new CheckCalendarAppointmentsQueryHandler(mediator, providerService, email)
                .Handle(new CheckCalendarAppointmentsQuery(), new CancellationToken());
        return result;
    }
}