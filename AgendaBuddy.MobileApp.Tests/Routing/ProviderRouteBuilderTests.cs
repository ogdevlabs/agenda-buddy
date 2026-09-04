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

    [Fact]
    public void WorkHours_BuildsPutOnItsOwnSubResource()
    {
        var route = ProviderRouteBuilder.WorkHours("provider@example.com");

        Assert.Equal(HttpMethod.Put, route.Method);
        Assert.Equal("api/v1/providers/provider@example.com/work-hours", route.Path);
    }

    [Fact]
    public void WorkHours_IsNotThePlainProviderPutWhichReplacesTheWholeDocument()
    {
        Assert.NotEqual(
            ProviderRouteBuilder.UpdateProvider("provider@example.com").Path,
            ProviderRouteBuilder.WorkHours("provider@example.com").Path);
    }

    [Fact]
    public void WorkHoursPayload_NamesTheFieldsTheApiValidates()
    {
        var payload = ProviderRouteBuilder.BuildWorkHoursPayload(8, 17);
        var json = System.Text.Json.JsonSerializer.Serialize(payload);

        Assert.Equal("{\"startHour\":8,\"endHour\":17}", json);
    }
}
