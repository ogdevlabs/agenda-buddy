namespace AgendaBuddy.MobileApp.Routing;

public static class CalendarRouteBuilder
{
    /// <summary>
    /// A PROVIDER's free slots — <c>GET /api/v1/calendar/availability/{email}</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="providerEmail"/> is deliberately not the caller: this route is no longer
    /// ownership-guarded (2026-08-29), because a customer has to read a provider's availability in order
    /// to book it. Its sibling <see cref="Appointments"/> IS still owner-only.
    /// </para>
    /// <para>
    /// <paramref name="serviceName"/> sizes each slot to that service's duration server-side, so a
    /// 90-minute service is never offered a start time that would run into the next appointment. Omitted
    /// when null/blank, which falls back to a 60-minute grid rather than returning nothing.
    /// </para>
    /// <para>
    /// There is no <c>from</c> parameter. The previous builder sent one and the server never read it —
    /// the window always starts now — so sending it implied a control that did not exist.
    /// </para>
    /// </remarks>
    public static RouteSpec Availability(string providerEmail, int days, string? serviceName = null)
    {
        var path = $"api/v1/calendar/availability/{Uri.EscapeDataString(providerEmail)}?days={days}";

        if (!string.IsNullOrWhiteSpace(serviceName))
            path += $"&service={Uri.EscapeDataString(serviceName)}";

        return new RouteSpec(HttpMethod.Get, path);
    }

    /// <summary>
    /// Also does duty for <see cref="Services.BookingApiService"/>'s "today's appointments" and
    /// "one appointment" reads — see the deviation note on <see cref="BookingRouteBuilder"/>. Calendar owns
    /// the only real GET for an appointment's data; Booking owns none. Still owner-only server-side.
    /// </summary>
    public static RouteSpec Appointments(string email) =>
        new(HttpMethod.Get, $"api/v1/calendar/appointments/{Uri.EscapeDataString(email)}");
}
