namespace AgendaBuddy.Booking.Domain.Commands;

[ExcludeFromCodeCoverage]
public class BookAppointmentCommand : IRequest<Result<AppointmentEntity>>
{
    public required AppointmentEntity AppointmentEntity { get; set; }
}
