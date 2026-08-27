using AgendaBuddy.MobileApp.Routing;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Routing;

public class CustomerRouteBuilderTests
{
    // The real backend route is GET api/v1/customers (paginated, ADR-023).
    [Fact]
    public void Customers_BuildsGet()
    {
        var route = CustomerRouteBuilder.Customers();

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/customers", route.Path);
    }
}
