using EventAndCommands.Commands.Booking;
using MediatR;

namespace Booking.Requests;

public class RequestCollection(IKafkaClient kafkaClient) : IRequestCollection
{
    public async Task<string> BookAppointmentRequest(IMediator mediator, ProviderService providerService,
        BookingService bookingService,
        AppointmentEntity appointmentEntity)
    {
        var result =
            await new BookingAppointmentCommandHandler(mediator, kafkaClient as KafkaClient, providerService,
                bookingService,
                appointmentEntity).Handle(new BookAppointmentCommand { AppointmentEntity = appointmentEntity },
                new CancellationToken());
        return result;
    }

    public Task<bool> UpdateAppointmentRequest(IMediator mediator, ProviderService providerService,
        BookingService bookingService, string identifier,
        AppointmentEntity appointmentEntity)
    {
        throw new NotImplementedException();
    }

    public Task<bool> CancelAppointment(IMediator mediator, ProviderService providerService,
        BookingService bookingService, string identifier)
    {
        throw new NotImplementedException();
    }
}