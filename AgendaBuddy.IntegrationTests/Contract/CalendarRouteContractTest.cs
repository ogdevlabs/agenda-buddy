using System.Net;

namespace AgendaBuddy.IntegrationTests.Contract;

/// <summary>
/// F-018-T11 AC-5, Calendar: one real HTTP request through the real pipeline, asserting the status code
/// only. See <see cref="BookingRouteContractTest"/> for why status-only is the deliberate design
/// (ADR-017, F-019's <c>DataResponse&lt;T&gt;</c>).
/// </summary>
/// <remarks>
/// Route chosen: <c>GET /api/v1/calendar/availability/{email}</c> (`Calendar/Program.cs`) — the
/// simpler of Calendar's two routes (no request body, a single path parameter). Both Calendar routes
/// carry <c>.RequireAuthorization()</c> ahead of the ownership guard (F-016), so an anonymous caller is
/// refused before the cache read or the ownership check ever run — today, anonymously, this is
/// <b>401</b>.
/// </remarks>
[Collection(Harness.HarnessCollection.Name)]
public class CalendarRouteContractTest(Harness.ServiceHostFixture<CalendarAnchor> host)
    : IClassFixture<Harness.ServiceHostFixture<CalendarAnchor>>
{
    [Fact]
    public async Task GetAvailability_Anonymously_Returns401()
    {
        using var service = host.StartService();

        var response = await service.Client.GetAsync("api/v1/calendar/availability/caller@example.com");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
