namespace Calendar.Requests;

public class RequestCollection : IRequestCollection
{
    public async Task<IEnumerable<AppointmentEntity>> CheckCalendarAvailabilityRequest(IMediator mediator,
        ProviderService providerService, string email)
    {
        var result =
            await new CheckCalendarAvailabilityQueryHandler(mediator, providerService, email).Handle(
                new CheckCalendarAvailabilityQuery(), new CancellationToken());
        return result;
    }

    public async Task<IEnumerable<AppointmentEntity>> CheckCalendarAppointmentsRequest(IMediator mediator, ProviderService providerService, string email)
    {
        // TODO 

        return null;
    }
}