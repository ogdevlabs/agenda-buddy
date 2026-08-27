namespace AgendaBuddy.MobileApp.Routing;

/// <summary>
/// Route-building logic extracted from <see cref="Services.CalendarApiService"/> (F-015-T06), corrected to
/// the real backend contract (F-015-T07, api-contracts.md §2). Both Calendar routes are keyed by the
/// caller's own email (ownership-guarded server-side since F-016), so both builders take it explicitly
/// rather than relying on a hidden ambient session.
/// </summary>
public static class CalendarRouteBuilder
{
    public static RouteSpec Availability(string email, DateOnly from, int days) =>
        new(HttpMethod.Get, $"api/v1/calendar/availability/{email}?from={from:yyyy-MM-dd}&days={days}");

    /// <summary>
    /// Also does duty for <see cref="Services.BookingApiService"/>'s "today's appointments" and
    /// "one appointment" reads — see the deviation note on <see cref="BookingRouteBuilder"/>. Calendar owns
    /// the only real GET for an appointment's data; Booking owns none.
    /// </summary>
    public static RouteSpec Appointments(string email) =>
        new(HttpMethod.Get, $"api/v1/calendar/appointments/{email}");
}
