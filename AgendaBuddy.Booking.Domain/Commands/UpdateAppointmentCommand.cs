namespace AgendaBuddy.Booking.Domain.Commands;

[ExcludeFromCodeCoverage]
public class UpdateAppointmentCommand : IRequest<Result<AppointmentEntity>>
{
    public required AppointmentEntity AppointmentEntity { get; set; }
}
