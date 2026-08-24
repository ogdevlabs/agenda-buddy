using MobileApp.Routing;
using Xunit;

namespace MobileApp.Tests.Routing;

public class NotificationRouteBuilderTests
{
    // Pins NotificationApiService.GetNotificationsAsync's current route: GET "notifications".
    [Fact]
    public void Notifications_BuildsGet()
    {
        var route = NotificationRouteBuilder.Notifications();

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("notifications", route.Path);
    }

    // Pins NotificationApiService.MarkReadAsync's current route: PATCH "notifications/{id}/read".
    [Fact]
    public void MarkRead_BuildsPatchById()
    {
        var route = NotificationRouteBuilder.MarkRead("n1");

        Assert.Equal(HttpMethod.Patch, route.Method);
        Assert.Equal("notifications/n1/read", route.Path);
    }
}
