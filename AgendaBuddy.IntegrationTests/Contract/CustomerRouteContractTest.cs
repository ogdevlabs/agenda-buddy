using System.Net;

namespace AgendaBuddy.IntegrationTests.Contract;

/// <summary>
/// F-018-T11 AC-5, Customer: one real HTTP request through the real pipeline, asserting the status code
/// only. See <see cref="BookingRouteContractTest"/> for why status-only is the deliberate design
/// (ADR-017, F-019's <c>DataResponse&lt;T&gt;</c>).
/// </summary>
/// <remarks>
/// Route chosen: <c>POST /api/v1/customers</c> (`Customer/Program.cs`) — the central creation route
/// for the service. It carries <c>.RequireAuthorization()</c> (F-016 closed the anonymous-write gap),
/// so an anonymous caller is refused before <c>MiniValidator</c> or the handler ever run — today,
/// anonymously, this is <b>401</b>.
/// </remarks>
[Collection(Harness.HarnessCollection.Name)]
public class CustomerRouteContractTest(Harness.ServiceHostFixture<CustomerAnchor> host)
    : IClassFixture<Harness.ServiceHostFixture<CustomerAnchor>>
{
    [Fact]
    public async Task PostCustomers_Anonymously_Returns401()
    {
        using var service = host.StartService();

        var response = await service.Client.PostAsync("api/v1/customers", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
