namespace EventAndCommands.Queries.Calendar;

public class CheckAvailabilityQuery : IRequest<List<AppointmentEntity>>
{
    public string? Email { get; set; }
}