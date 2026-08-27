namespace AgendaBuddy.Booking.Domain.Commands;

[ExcludeFromCodeCoverage]
public class CancelAppointmentCommand : IRequest<Result<AppointmentEntity>>
{
    public required string Identifier { get; set; }
}
