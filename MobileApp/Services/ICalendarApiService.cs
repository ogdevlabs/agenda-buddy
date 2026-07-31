using MobileApp.Models;

namespace MobileApp.Services;

public interface ICalendarApiService
{
    Task<List<CalendarDaySummary>> GetAvailabilityAsync(int days = 30, CancellationToken ct = default);
}
