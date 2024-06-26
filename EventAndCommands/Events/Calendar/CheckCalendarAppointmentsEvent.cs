namespace EventAndCommands.Events.Calendar;

[ExcludeFromCodeCoverage]
public class CheckCalendarAppointmentsEvent : INotification
{
    public required string Email { get; set; }
}