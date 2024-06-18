namespace EventAndCommands.Events.Calendar;

public class CheckCalendarAppointmentsEvent : INotification
{
    public required string Email { get; set; }
}