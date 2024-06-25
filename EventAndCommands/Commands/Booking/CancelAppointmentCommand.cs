namespace EventAndCommands.Commands.Booking;

public class CancelAppointmentCommand : IRequest<string>
{
    public required string Identifier { get; set; }
}