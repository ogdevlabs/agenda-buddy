using MediatR;

namespace Booking.Requests;

public interface IRequestCollection
{
    public Task<string> BookAppointmentRequest(IMediator mediator, ProviderService providerService,
        BookingService bookingService,
        AppointmentEntity appointmentEntity);

    public Task<bool> UpdateAppointmentRequest(IMediator mediator, ProviderService providerService,
        BookingService bookingService, string identifier,
        AppointmentEntity appointmentEntity);

    public Task<bool> CancelAppointment(IMediator mediator, ProviderService providerService,
        BookingService bookingService, string identifier);
}