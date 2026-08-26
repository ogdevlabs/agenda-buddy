namespace EventAndCommands.Events.Calendar;

[ExcludeFromCodeCoverage]
public class CheckCalendarAvailabilityEvent : INotification
{
    public required string Email { get; set; }
}
