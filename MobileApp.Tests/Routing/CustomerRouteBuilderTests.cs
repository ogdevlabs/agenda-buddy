using MobileApp.Routing;
using Xunit;

namespace MobileApp.Tests.Routing;

public class CustomerRouteBuilderTests
{
    // Pins CustomerApiService.GetCustomersAsync's current route: GET "customer" — no api/v1 prefix.
    [Fact]
    public void Customers_BuildsGet()
    {
        var route = CustomerRouteBuilder.Customers();

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("customer", route.Path);
    }
}
