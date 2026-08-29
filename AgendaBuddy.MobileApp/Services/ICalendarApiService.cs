using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

public interface ICalendarApiService
{
    Task<List<CalendarDaySummary>> GetAvailabilityAsync(int days = 30, CancellationToken ct = default);

    /// <summary>
    /// A PROVIDER's bookable start times for one service, grouped by date — what the booking flow offers
    /// the customer instead of asking them to type a date and time.
    /// </summary>
    /// <remarks>
    /// Fetched once for the whole window and grouped client-side, so moving between dates in the calendar
    /// is instant and needs no round trip. Only dates that actually have a free slot appear, which is what
    /// lets the calendar grey out the rest rather than offering a day that turns out to be full.
    /// </remarks>
    Task<ProviderAvailability> GetProviderAvailabilityAsync(
        string providerEmail, string? serviceName, int days = 90, CancellationToken ct = default);

    /// <summary>
    /// All appointments for the caller's own email (api-contracts.md §2's
    /// <c>CalendarApiService.GetAppointmentsAsync</c> row). Also the real read path
    /// <see cref="BookingApiService"/> composes with for "today's appointments" / "one appointment" — see
    /// the deviation note on <see cref="AgendaBuddy.MobileApp.Routing.BookingRouteBuilder"/>.
    /// </summary>
    Task<List<AppointmentDetail>> GetAppointmentsAsync(CancellationToken ct = default);
}
