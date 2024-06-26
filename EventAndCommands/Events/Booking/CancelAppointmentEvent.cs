namespace EventAndCommands.Events.Booking;

[ExcludeFromCodeCoverage]
public class CancelAppointmentEvent : INotification
{
    public string? Identifier { get; set; }
}