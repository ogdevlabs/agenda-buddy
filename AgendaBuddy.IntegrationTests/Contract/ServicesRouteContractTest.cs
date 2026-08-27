using System.Net;

namespace AgendaBuddy.IntegrationTests.Contract;

/// <summary>
/// Services: one real HTTP request through the real pipeline, asserting the status code
/// only. See <see cref="BookingRouteContractTest"/> for why status-only is the deliberate design
/// (ADR-017, see <c>DataResponse&lt;T&gt;</c>).
/// </summary>
/// <remarks>
/// Route chosen: <c>GET /api/v1/services/{email}</c> (`Services/Program.cs`) — the service's only read
/// route, and one of the five PII-bearing GETs. It carries
/// <c>.RequireAuthorization()</c>, so an anonymous caller is refused before the handler ever runs —
/// today, anonymously, this is <b>401</b>.
/// </remarks>
[Collection(Harness.HarnessCollection.Name)]
public class ServicesRouteContractTest(Harness.ServiceHostFixture<ServicesAnchor> host)
    : IClassFixture<Harness.ServiceHostFixture<ServicesAnchor>>
{
    [Fact]
    public async Task GetServices_Anonymously_Returns401()
    {
        using var service = host.StartService();

        var response = await service.Client.GetAsync("api/v1/services/caller@example.com");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
