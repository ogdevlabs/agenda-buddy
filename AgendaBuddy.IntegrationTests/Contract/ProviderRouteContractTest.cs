using System.Net;

namespace AgendaBuddy.IntegrationTests.Contract;

/// <summary>
/// F-018-T11 AC-5, Provider: one real HTTP request through the real pipeline, asserting the status code
/// only. See <see cref="BookingRouteContractTest"/> for why status-only is the deliberate design
/// (ADR-017, F-019's <c>DataResponse&lt;T&gt;</c>).
/// </summary>
/// <remarks>
/// Route chosen: <c>POST /api/v1/providers</c> (`Provider/Program.cs`) — the central creation route,
/// and the one F-016's headline fix (role + owner check) landed on. It carries
/// <c>.RequireAuthorization()</c>, so an anonymous caller is refused before the role/ownership checks or
/// <c>MiniValidator</c> ever run — today, anonymously, this is <b>401</b>.
/// </remarks>
[Collection(Harness.HarnessCollection.Name)]
public class ProviderRouteContractTest(Harness.ServiceHostFixture<ProviderAnchor> host)
    : IClassFixture<Harness.ServiceHostFixture<ProviderAnchor>>
{
    [Fact]
    public async Task PostProviders_Anonymously_Returns401()
    {
        using var service = host.StartService();

        var response = await service.Client.PostAsync("api/v1/providers", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
