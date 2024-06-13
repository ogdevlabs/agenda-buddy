namespace EventAndCommands.Events.Calendar;

public class CheckCalendarAvailabilityEvent : INotification
{
    public required string Email { get; set; }
}