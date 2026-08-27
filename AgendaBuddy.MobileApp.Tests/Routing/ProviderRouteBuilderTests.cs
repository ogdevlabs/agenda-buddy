using AgendaBuddy.MobileApp.Routing;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Routing;

public class ProviderRouteBuilderTests
{
    [Fact]
    public void Report_BuildsGetByEmail()
    {
        var route = ProviderRouteBuilder.Report("provider@example.com");

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/providers/provider@example.com/report", route.Path);
    }

    [Fact]
    public void Deactivate_BuildsPostByEmail()
    {
        var route = ProviderRouteBuilder.Deactivate("provider@example.com");

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("api/v1/providers/provider@example.com/deactivate", route.Path);
    }
}
