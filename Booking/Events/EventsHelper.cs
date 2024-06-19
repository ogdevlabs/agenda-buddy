using MediatR;

namespace Booking.Events;

public static class EventsHelper
{
    public static async Task<string> BookAppointmentEvent(IRequestCollection requestCollection, IMediator mediator,
        ProviderService providerService, BookingService bookingService, AppointmentEntity appointmentEntity)
    {
        var notificationResponse =
            await requestCollection.BookAppointmentRequest(mediator, providerService, bookingService,
                appointmentEntity);
        return notificationResponse;
    }
}