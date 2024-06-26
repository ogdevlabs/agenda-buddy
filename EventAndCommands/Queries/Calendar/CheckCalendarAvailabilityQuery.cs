namespace EventAndCommands.Queries.Calendar;

[ExcludeFromCodeCoverage]
public class CheckCalendarAvailabilityQuery : IRequest<List<DateTime>>
{
    public string? Email { get; set; }
}