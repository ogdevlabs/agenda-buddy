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

    public async Task<string> UpdateAppointmentRequest(IMediator mediator, ProviderService providerService,
        BookingService bookingService, string identifier,
        AppointmentEntity appointmentEntity)
    {
        var result =
            await new UpdateAppointmentCommandHandler(mediator, kafkaClient as KafkaClient, providerService,
                bookingService,
                appointmentEntity).Handle(new UpdateAppointmentCommand { AppointmentEntity = appointmentEntity },
                new CancellationToken());
        return result;
    }

    public async Task<string> CancelAppointmentRequest(IMediator mediator, ProviderService providerService,
        BookingService bookingService, string identifier)
    {
        var result = await new CancelAppointmentCommandHandler(mediator, kafkaClient as KafkaClient, providerService,
                bookingService, identifier)
            .Handle(new CancelAppointmentCommand { Identifier = identifier }, new CancellationToken());
        return result;
    }
}