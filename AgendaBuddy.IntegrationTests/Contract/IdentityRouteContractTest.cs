using System.Net;
using System.Net.Http.Json;

namespace AgendaBuddy.IntegrationTests.Contract;

/// <summary>
/// Identity: one real HTTP request through the real pipeline, asserting the status code
/// only. See <see cref="BookingRouteContractTest"/> for why status-only is the deliberate design
/// (ADR-017, see <c>DataResponse&lt;T&gt;</c>).
/// </summary>
/// <remarks>
/// <para>
/// Route chosen: <c>POST /api/v1/auth/register</c> (`AgendaBuddy.Identity/Program.cs`) — the one route in this
/// inventory that is anonymous by design and, for a valid unique account, is not an auth-refusal.
/// </para>
/// Registration creates a pending credential and sends confirmation email; it does not mint a session,
/// so no private signing key is needed on this route.
/// </remarks>
[Collection(Harness.HarnessCollection.Name)]
public class IdentityRouteContractTest(Harness.ServiceHostFixture<IdentityAnchor> host)
    : IClassFixture<Harness.ServiceHostFixture<IdentityAnchor>>
{
    [Fact]
    public async Task PostRegister_WithAValidNewAccount_Returns202PendingVerification()
    {
        using var service = host.StartService();

        var response = await service.Client.PostAsJsonAsync("api/v1/auth/register", new
        {
            Email = $"contract-{Guid.NewGuid():N}@example.com",
            Password = "correct-horse-battery-staple",
            Role = "Customer",
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    /// <summary>
    /// <c>DELETE /device-token</c> is mapped and requires a token.
    /// </summary>
    /// <remarks>
    /// Both halves matter, and one assertion covers them: an <b>unmapped</b> path answers 404, so a 401 proves
    /// the route exists <i>and</i> that it is behind <c>RequireAuthorization</c>. The account comes from the
    /// caller's own claim and there is no body, so an unauthenticated caller has nothing to substitute — this is
    /// the whole authorisation model of the route, and the reason it can release a device registration without
    /// naming an address.
    /// <para>
    /// Reachable through the Gateway because its <c>/device-token</c> allowlist entry matches on path with no
    /// <c>Methods</c> restriction, so every verb on that path forwards. Path-only matching is asserted by the
    /// Gateway's own tests; this asserts the service half.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task DeleteDeviceToken_WithNoToken_Returns401_NotFound()
    {
        using var service = host.StartService();

        var response = await service.Client.DeleteAsync("device-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
