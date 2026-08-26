using System.Net;

namespace AgendaBuddy.IntegrationTests.Contract;

/// <summary>
/// F-018-T11 AC-5, Profession: one real HTTP request through the real pipeline, asserting the status
/// code only. See <see cref="BookingRouteContractTest"/> for why status-only is the deliberate design
/// (ADR-017, F-019's <c>DataResponse&lt;T&gt;</c>).
/// </summary>
/// <remarks>
/// Route chosen: <c>GET /api/v1/professions</c> (`Profession/Program.cs`) — deliberately anonymous by
/// design (ADR-025): professions are seeded reference data with no PII, so this is the one service in
/// the inventory where the pinned contract is a <b>200</b>, not a 401. Picking a 401-shaped route for
/// every service would understate what "Tier 1" is for — pinning whatever the pipeline actually returns
/// today, not a uniform assumption about auth.
/// </remarks>
[Collection(Harness.HarnessCollection.Name)]
public class ProfessionRouteContractTest(Harness.ServiceHostFixture<ProfessionAnchor> host)
    : IClassFixture<Harness.ServiceHostFixture<ProfessionAnchor>>
{
    [Fact]
    public async Task GetProfessions_Anonymously_Returns200()
    {
        using var service = host.StartService();

        var response = await service.Client.GetAsync("api/v1/professions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
