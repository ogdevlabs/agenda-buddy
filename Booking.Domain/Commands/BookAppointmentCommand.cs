namespace Booking.Domain.Commands;

[ExcludeFromCodeCoverage]
public class BookAppointmentCommand : IRequest<string>
{
    public required AppointmentEntity AppointmentEntity { get; set; }
}
