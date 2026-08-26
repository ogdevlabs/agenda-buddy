namespace EventAndCommands.Commands.Calendar;

[ExcludeFromCodeCoverage]
public class BookCalendarCommand : IRequest<bool>
{
    public string? Email { get; set; }
    public DateTime DateTime { get; set; }
}
