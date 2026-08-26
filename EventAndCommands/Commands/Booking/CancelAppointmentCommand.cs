namespace EventAndCommands.Commands.Booking;

[ExcludeFromCodeCoverage]
public class CancelAppointmentCommand : IRequest<string>
{
    public required string Identifier { get; set; }
}
