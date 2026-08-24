using MobileApp.Routing;
using Xunit;

namespace MobileApp.Tests.Routing;

public class NotificationRouteBuilderTests
{
    // F-015-T07: corrected to the real backend route — hosted by Customer under a top-level
    // /api/v1/notifications group (F-014, ADR D-2).
    [Fact]
    public void Notifications_BuildsGet()
    {
        var route = NotificationRouteBuilder.Notifications();

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/notifications", route.Path);
    }

    // F-015-T07: POST, not PATCH — the real route is notifications.MapPost("/{id}/read", …).
    [Fact]
    public void MarkRead_BuildsPostById()
    {
        var route = NotificationRouteBuilder.MarkRead("n1");

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("api/v1/notifications/n1/read", route.Path);
    }
}
