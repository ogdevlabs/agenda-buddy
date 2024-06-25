namespace EventAndCommands.Commands.Booking;

public class UpdateAppointmentCommand : IRequest<string>
{
    public required AppointmentEntity AppointmentEntity { get; set; }
}