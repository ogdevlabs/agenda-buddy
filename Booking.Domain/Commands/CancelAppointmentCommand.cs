namespace Booking.Domain.Commands;

[ExcludeFromCodeCoverage]
public class CancelAppointmentCommand : IRequest<string>
{
    public required string Identifier { get; set; }
}
