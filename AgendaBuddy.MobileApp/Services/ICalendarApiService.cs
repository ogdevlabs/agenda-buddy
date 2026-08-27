using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

public interface ICalendarApiService
{
    Task<List<CalendarDaySummary>> GetAvailabilityAsync(int days = 30, CancellationToken ct = default);

    /// <summary>
    /// All appointments for the caller's own email (F-015-T07; new, api-contracts.md §2's
    /// <c>CalendarApiService.GetAppointmentsAsync</c> row). Also the real read path
    /// <see cref="BookingApiService"/> composes with for "today's appointments" / "one appointment" — see
    /// the deviation note on <see cref="AgendaBuddy.MobileApp.Routing.BookingRouteBuilder"/>.
    /// </summary>
    Task<List<AppointmentDetail>> GetAppointmentsAsync(CancellationToken ct = default);
}
