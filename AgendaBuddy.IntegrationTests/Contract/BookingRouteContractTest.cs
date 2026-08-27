using System.Net;

namespace AgendaBuddy.IntegrationTests.Contract;

/// <summary>
/// AC-5, Booking: one real HTTP request through the real pipeline, asserting the status code
/// only.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately status-only (ADR-017).</b> <c>DataResponse&lt;T&gt;</c>
/// changes every response envelope by design. A test asserting the envelope here would have to be
/// rewritten whenever that envelope changes regardless of whether the change broke anything, which makes it useless as a
/// regression signal for that work. Asserting only the status code lets this test keep meaning: if
/// it goes red after an envelope change, the status code itself changed, which is the one thing such a change is not
/// supposed to touch.
/// </para>
/// <para>
/// <b>Route chosen: <c>POST /api/v1/booking/appointments</c></b> (`Booking/Program.cs`) — the only
/// creation route on the service, and the one <c>01-api-surface.md</c> calls out as the sole way in
/// (there is no <c>GET</c> on Booking at all). It carries <c>.RequireAuthorization()</c>, so an
/// anonymous caller never reaches <c>MiniValidator</c> or the handler body — the pipeline's
/// authentication middleware answers before any request-body concern, which is why this test needs no
/// body and no <see cref="AgendaBuddy.IntegrationTests.Harness.TokenFactory"/>. That is the contract
/// being pinned: today, anonymously, this route is <b>401</b>, not 400 for a missing body.
/// </para>
/// </remarks>
[Collection(Harness.HarnessCollection.Name)]
public class BookingRouteContractTest(Harness.ServiceHostFixture<BookingAnchor> host)
    : IClassFixture<Harness.ServiceHostFixture<BookingAnchor>>
{
    [Fact]
    public async Task PostAppointments_Anonymously_Returns401()
    {
        using var service = host.StartService();

        var response = await service.Client.PostAsync("api/v1/booking/appointments", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
