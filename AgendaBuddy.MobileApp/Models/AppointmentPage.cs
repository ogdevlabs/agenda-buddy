namespace AgendaBuddy.MobileApp.Models;

public enum AppointmentPageSegment
{
    Scheduled,
    Done,
    Cancelled
}

public sealed record AppointmentPage(
    List<AppointmentDetail> Items,
    long TotalCount,
    int Page,
    int PageSize)
{
    public static AppointmentPage Empty(int page, int pageSize) =>
        new([], 0, page, pageSize);
}