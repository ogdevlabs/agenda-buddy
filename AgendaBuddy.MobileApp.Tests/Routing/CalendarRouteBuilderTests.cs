using AgendaBuddy.MobileApp.Routing;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Routing;

public class CalendarRouteBuilderTests
{
    // {email} is required: both Calendar routes are ownership-guarded on the caller's own claim.
    [Fact]
    public void Availability_BuildsGetWithEmailFromAndDaysQueryParams()
    {
        var route = CalendarRouteBuilder.Availability("alice@example.com", new DateOnly(2026, 8, 1), 30);

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/calendar/availability/alice@example.com?from=2026-08-01&days=30", route.Path);
    }

    [Fact]
    public void Availability_HonoursNonDefaultDaysValue()
    {
        var route = CalendarRouteBuilder.Availability("alice@example.com", new DateOnly(2026, 8, 1), 7);

        Assert.Equal("api/v1/calendar/availability/alice@example.com?from=2026-08-01&days=7", route.Path);
    }

    // The real read path BookingApiService.GetTodayAppointmentsAsync / GetAppointmentAsync compose with
    // this, since Booking itself has no GET route for an appointment (see the deviation note on
    // BookingRouteBuilder).
    [Fact]
    public void Appointments_BuildsGetByEmail()
    {
        var route = CalendarRouteBuilder.Appointments("alice@example.com");

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/calendar/appointments/alice@example.com", route.Path);
    }
}
