using AgendaBuddy.MobileApp.Routing;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Routing;

public class CustomerRouteBuilderTests
{
    // F-015-T07: corrected to the real backend route — GET api/v1/customers (paginated, F-016/ADR-023).
    [Fact]
    public void Customers_BuildsGet()
    {
        var route = CustomerRouteBuilder.Customers();

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/customers", route.Path);
    }
}
