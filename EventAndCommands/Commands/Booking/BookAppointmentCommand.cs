namespace EventAndCommands.Commands.Booking;

public class BookAppointmentCommand : IRequest<string>
{
    public required AppointmentEntity AppointmentEntity { get; set; }
}
