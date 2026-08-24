using Library.Entities;

namespace MobileApp.Routing;

/// <summary>
/// Route-building logic extracted from <see cref="Services.BookingApiService"/> (F-015-T06).
/// Pure refactor: these routes are the ones currently produced by BookingApiService, not the
/// corrected ones — F-015-T07 fixes the strings themselves in this same class.
/// </summary>
public static class BookingRouteBuilder
{
    public static RouteSpec TodayAppointments(DateOnly date) =>
        new(HttpMethod.Get, $"booking?date={date:yyyy-MM-dd}");

    public static RouteSpec Appointment(string id) =>
        new(HttpMethod.Get, $"booking/{id}");

    public static RouteSpec UpdateAppointmentStatus(string id) =>
        new(HttpMethod.Put, $"booking/{id}");

    public static object BuildUpdateStatusPayload(AppointmentStatus status) =>
        new { status = status.ToString() };
}
