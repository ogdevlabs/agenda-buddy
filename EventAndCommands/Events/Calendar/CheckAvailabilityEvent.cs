namespace EventAndCommands.Events.Calendar;

public class CheckAvailabilityEvent : INotification
{
    public required string Email { get; set; }
}