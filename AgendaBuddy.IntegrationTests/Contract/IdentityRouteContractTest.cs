using System.Net;
using System.Net.Http.Json;

namespace AgendaBuddy.IntegrationTests.Contract;

/// <summary>
/// F-018-T11 AC-5, Identity: one real HTTP request through the real pipeline, asserting the status code
/// only. See <see cref="BookingRouteContractTest"/> for why status-only is the deliberate design
/// (ADR-017, F-019's <c>DataResponse&lt;T&gt;</c>).
/// </summary>
/// <remarks>
/// <para>
/// Route chosen: <c>POST /api/v1/auth/register</c> (`AgendaBuddy.Identity/Program.cs`) — the one route in this
/// inventory that is anonymous by design and, for a valid unique account, is not an auth-refusal.
/// </para>
/// <para>
/// <b>The pinned status is 500, not 201 — run for real before writing this assertion, per this task's
/// own instructions.</b> A valid registration reaches <c>IdentityService.RegisterAsync</c>, which mints a
/// token pair signed with <c>JWT_PRIVATE_KEY</c> — an environment variable
/// <see cref="Harness.CryptoSessionFixture"/> deliberately never materialises in this public repository
/// (F-016 AC-3: no private key may ever exist as a loggable/serialisable string). Every route that mints
/// a token therefore 500s under this harness today, and that is a harness limitation, not a production
/// defect — the exact precedent already pinned by
/// <see cref="Harness.LogoutTest.Refresh_WithAValidUnexpiredToken_MatchesTheCredential"/> and documented
/// in <see cref="Harness.AuthRateLimitTest"/>'s class remarks. Pinning anything else here would assert
/// what this harness wishes were true rather than what it actually returns.
/// </para>
/// </remarks>
[Collection(Harness.HarnessCollection.Name)]
public class IdentityRouteContractTest(Harness.ServiceHostFixture<IdentityAnchor> host)
    : IClassFixture<Harness.ServiceHostFixture<IdentityAnchor>>
{
    [Fact]
    public async Task PostRegister_WithAValidNewAccount_Returns500_TokenSigningKeyIsNotAvailableInThisHarness()
    {
        using var service = host.StartService();

        var response = await service.Client.PostAsJsonAsync("api/v1/auth/register", new
        {
            Email = $"contract-{Guid.NewGuid():N}@example.com",
            Password = "correct-horse-battery-staple",
            Role = "Customer",
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
