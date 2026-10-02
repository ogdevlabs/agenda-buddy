using System.Text.Json;
using AgendaBuddy.MobileApp.Routing;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Routing;

public class CalendarFeedRouteBuilderTests
{
    [Fact]
    public void EveryVerbTargetsTheCallersOwnFeed()
    {
        Assert.Equal(new RouteSpec(HttpMethod.Get, "api/v1/calendar/feed"), CalendarFeedRouteBuilder.Status());
        Assert.Equal(new RouteSpec(HttpMethod.Post, "api/v1/calendar/feed"), CalendarFeedRouteBuilder.Enable());
        Assert.Equal(new RouteSpec(HttpMethod.Delete, "api/v1/calendar/feed"), CalendarFeedRouteBuilder.Disable());
    }

    [Fact]
    public void TheEnablePayloadCarriesOnlyTheLanguage()
    {
        var json = JsonSerializer.Serialize(
            CalendarFeedRouteBuilder.BuildEnablePayload("es-MX"), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal("""{"language":"es-MX"}""", json);
    }
}
