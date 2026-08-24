using MobileApp.Routing;
using Xunit;

namespace MobileApp.Tests.Routing;

public class ProviderRouteBuilderTests
{
    // F-015-T07: new — F-014's provider report route, never called by the client before this task.
    [Fact]
    public void Report_BuildsGetByEmail()
    {
        var route = ProviderRouteBuilder.Report("provider@example.com");

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/providers/provider@example.com/report", route.Path);
    }

    // F-015-T07: new — F-014's provider deactivation route, never called by the client before this task.
    [Fact]
    public void Deactivate_BuildsPostByEmail()
    {
        var route = ProviderRouteBuilder.Deactivate("provider@example.com");

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("api/v1/providers/provider@example.com/deactivate", route.Path);
    }
}
