namespace EventAndCommands.Commands.Booking;

[ExcludeFromCodeCoverage]
public class UpdateAppointmentCommand : IRequest<string>
{
    public required AppointmentEntity AppointmentEntity { get; set; }
}