namespace Booking.Domain.Commands;

[ExcludeFromCodeCoverage]
public class UpdateAppointmentCommand : IRequest<string>
{
    public required AppointmentEntity AppointmentEntity { get; set; }
}
