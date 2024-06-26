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

    public static async Task<string> UpdateAppointmentEvent(IRequestCollection requestCollection, IMediator mediator,
        ProviderService providerService, BookingService bookingService, AppointmentEntity appointmentEntity)
    {
        var notificationResponse =
            await requestCollection.UpdateAppointmentRequest(mediator, providerService, bookingService,
                appointmentEntity.Identifier,
                appointmentEntity);
        return notificationResponse;
    }

    public static async Task<string> CancelAppointmentEvent(IRequestCollection requestCollection, IMediator mediator,
        ProviderService providerService, BookingService bookingService, AppointmentEntity appointmentEntity)
    {
        var notificationResponse =
            await requestCollection.CancelAppointmentRequest(mediator, providerService, bookingService,
                appointmentEntity.Identifier);
        return notificationResponse;
    }
}