namespace EventAndCommands.Queries.Calendar;

public class CheckCalendarAppointmentsQuery : IRequest<List<AppointmentEntity>>
{
    public string? Email { get; set; }
}