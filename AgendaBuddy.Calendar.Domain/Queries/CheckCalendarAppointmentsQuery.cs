namespace AgendaBuddy.Calendar.Domain.Queries;

[ExcludeFromCodeCoverage]
public class CheckCalendarAppointmentsQuery : IRequest<Result<List<AppointmentEntity>>>
{
    public required string Email { get; set; }
}
