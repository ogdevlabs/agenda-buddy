using AgendaBuddy.MobileApp.Routing;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Routing;

public class NotificationRouteBuilderTests
{
    // The real backend route is hosted by Customer under a top-level /api/v1/notifications group
    // (ADR D-2).
    [Fact]
    public void Notifications_BuildsGet()
    {
        var route = NotificationRouteBuilder.Notifications();

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/notifications", route.Path);
    }

    // POST, not PATCH — the real route is notifications.MapPost("/{id}/read", …).
    [Fact]
    public void MarkRead_BuildsPostById()
    {
        var route = NotificationRouteBuilder.MarkRead("n1");

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("api/v1/notifications/n1/read", route.Path);
    }
}
