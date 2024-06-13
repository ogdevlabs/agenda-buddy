namespace EventAndCommands.Queries.Calendar;

public class CheckCalendarAvailabilityQuery : IRequest<List<AppointmentEntity>>
{
    public string? Email { get; set; }
}