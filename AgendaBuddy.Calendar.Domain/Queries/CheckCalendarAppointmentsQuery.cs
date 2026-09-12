namespace AgendaBuddy.Calendar.Domain.Queries;

using AgendaBuddy.Library.Dtos;

[ExcludeFromCodeCoverage]
public class CheckCalendarAppointmentsQuery : IRequest<Result<List<AppointmentEntity>>>
{
    public required string Email { get; set; }
}

public enum AppointmentListSegment
{
    Scheduled,
    Done,
    Cancelled
}

[ExcludeFromCodeCoverage]
public class GetAppointmentsPageQuery : IRequest<Result<PagedResponse<AppointmentEntity>>>
{
    public required string Email { get; set; }
    public required AppointmentListSegment Segment { get; set; }
    public required PageRequest Page { get; set; }
    public required DateTime NowUtc { get; set; }
}
