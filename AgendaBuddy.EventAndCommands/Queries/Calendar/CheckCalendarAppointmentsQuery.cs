namespace AgendaBuddy.EventAndCommands.Queries.Calendar;

[ExcludeFromCodeCoverage]
public class CheckCalendarAppointmentsQuery : IRequest<List<AppointmentEntity>>
{
    public string? Email { get; set; }
}
