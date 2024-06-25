namespace EventAndCommands.Events.Booking;

public class CancelAppointmentEvent : INotification
{
    public string? Identifier { get; set; }
}