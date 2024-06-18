namespace EventAndCommands.Queries.Calendar;

public class CheckCalendarAvailabilityQuery : IRequest<List<DateTime>>
{
    public string? Email { get; set; }
}