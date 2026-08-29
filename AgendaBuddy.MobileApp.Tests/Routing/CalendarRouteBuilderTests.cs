using AgendaBuddy.MobileApp.Routing;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Routing;

public class CalendarRouteBuilderTests
{
    // {email} here is the PROVIDER being browsed, not the caller: availability is no longer
    // ownership-guarded, because a customer has to read a provider's slots to book one. Appointments still is.
    [Fact]
    public void Availability_BuildsGetWithProviderEmailAndDays()
    {
        var route = CalendarRouteBuilder.Availability("alice@example.com", 90);

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/calendar/availability/alice%40example.com?days=90", route.Path);
    }

    [Fact]
    public void Availability_HonoursNonDefaultDaysValue()
    {
        var route = CalendarRouteBuilder.Availability("alice@example.com", 7);

        Assert.Equal("api/v1/calendar/availability/alice%40example.com?days=7", route.Path);
    }

    // The service sizes each slot server-side, so a 90-minute service is never offered a start time that
    // would run into the next appointment.
    [Fact]
    public void Availability_AppendsTheServiceWhenGiven_UrlEncoded()
    {
        var route = CalendarRouteBuilder.Availability("alice@example.com", 90, "Personal Training Session");

        Assert.Contains("days=90", route.Path);
        Assert.Contains("service=Personal%20Training%20Session", route.Path);
        Assert.DoesNotContain(" ", route.Path);
    }

    // Blank must be OMITTED rather than sent empty: the server falls back to a default-length grid, and
    // "service=" would name a service no provider has.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Availability_OmitsTheServiceWhenBlank(string? serviceName)
    {
        var route = CalendarRouteBuilder.Availability("alice@example.com", 90, serviceName);

        Assert.DoesNotContain("service", route.Path);
    }

    // The server never read `from` — the window always starts now — so sending it implied a control that
    // did not exist.
    [Fact]
    public void Availability_DoesNotSendAFromParameter()
    {
        Assert.DoesNotContain("from=", CalendarRouteBuilder.Availability("alice@example.com", 90).Path);
    }

    // The real read path BookingApiService.GetTodayAppointmentsAsync / GetAppointmentAsync compose with
    // this, since Booking itself has no GET route for an appointment (see the deviation note on
    // BookingRouteBuilder).
    [Fact]
    public void Appointments_BuildsGetByEmail()
    {
        var route = CalendarRouteBuilder.Appointments("alice@example.com");

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/calendar/appointments/alice%40example.com", route.Path);
    }

    // Emails are user data in a path segment, and a plus-addressed one is perfectly legal. Encoding them
    // matches what ServicesRouteBuilder already does for service names; %40 and %2B decode back to @ and +
    // server-side, so routing is unaffected.
    [Fact]
    public void BothRoutes_UrlEncodeAPlusAddressedEmail()
    {
        Assert.Contains("alice%2Btag%40example.com",
            CalendarRouteBuilder.Appointments("alice+tag@example.com").Path);
        Assert.Contains("alice%2Btag%40example.com",
            CalendarRouteBuilder.Availability("alice+tag@example.com", 90).Path);
    }
}
