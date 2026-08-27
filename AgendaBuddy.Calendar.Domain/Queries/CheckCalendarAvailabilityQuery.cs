namespace AgendaBuddy.Calendar.Domain.Queries;

[ExcludeFromCodeCoverage]
public class CheckCalendarAvailabilityQuery : IRequest<Result<List<DateTime>>>
{
    public required string Email { get; set; }
}
