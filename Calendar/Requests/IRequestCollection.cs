namespace Calendar.Requests;

public interface IRequestCollection
{
    public Task<IEnumerable<AppointmentEntity>> CheckCalendarAvailabilityRequest(IMediator mediator,
        ProviderService providerService, string email);
    
    public Task<IEnumerable<AppointmentEntity>> CheckCalendarAppointmentsRequest(IMediator mediator,
        ProviderService providerService, string email);
}