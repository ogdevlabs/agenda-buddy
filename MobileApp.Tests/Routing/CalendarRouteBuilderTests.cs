using MobileApp.Routing;
using Xunit;

namespace MobileApp.Tests.Routing;

public class CalendarRouteBuilderTests
{
    // Pins CalendarApiService.GetAvailabilityAsync's current route:
    // $"calendar?from={DateTime.UtcNow:yyyy-MM-dd}&days={days}" — no api/v1 prefix.
    [Fact]
    public void Availability_BuildsGetWithFromAndDaysQueryParams()
    {
        var route = CalendarRouteBuilder.Availability(new DateOnly(2026, 8, 1), 30);

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("calendar?from=2026-08-01&days=30", route.Path);
    }

    [Fact]
    public void Availability_HonoursNonDefaultDaysValue()
    {
        var route = CalendarRouteBuilder.Availability(new DateOnly(2026, 8, 1), 7);

        Assert.Equal("calendar?from=2026-08-01&days=7", route.Path);
    }
}
