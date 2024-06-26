namespace EventAndCommands.Commands.Booking;

[ExcludeFromCodeCoverage]
public class BookAppointmentCommand : IRequest<string>
{
    public required AppointmentEntity AppointmentEntity { get; set; }
}